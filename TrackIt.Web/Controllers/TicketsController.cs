using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackIt.Core;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;
using TrackIt.Web.Models;

namespace TrackIt.Web.Controllers;

[Authorize]
public class TicketsController : AppController
{
    private readonly ITicketService _tickets;
    private readonly IProjectRepository _projects;
    private readonly IUserDirectory _users;

    public TicketsController(ITicketService tickets, IProjectRepository projects, IUserDirectory users)
    {
        _tickets = tickets;
        _projects = projects;
        _users = users;
    }

    // GET /Tickets?status=Open&priority=High&page=2
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TicketFilter filter)
    {
        var me = Me;
        var result = await _tickets.SearchAsync(filter, me);

        var vm = new TicketListViewModel
        {
            Filter = filter,
            Result = result,
            IsAdmin = me.Role == Roles.Admin,
            CanCreate = me.Role is Roles.Admin or Roles.Reporter,
            Projects = ToItems(await _projects.GetAllAsync(activeOnly: false), p => p.ProjectId.ToString(), p => p.Name),
            Developers = me.Role == Roles.Admin
                ? ToItems(await _users.GetDevelopersAsync(), u => u.Id, u => u.FullName)
                : new(),
        };
        return View(vm);
    }

    [HttpGet, Authorize(Roles = $"{Roles.Reporter},{Roles.Admin}")]
    public async Task<IActionResult> Create() => View(await BuildCreateModelAsync(new CreateTicketViewModel()));

    [HttpPost, Authorize(Roles = $"{Roles.Reporter},{Roles.Admin}")]
    public async Task<IActionResult> Create(CreateTicketViewModel model)
    {
        if (ModelState.IsValid)
        {
            var newId = 0;
            var ok = await TryAsync(async () => newId = await _tickets.CreateTicketAsync(new CreateTicketRequest
            {
                Title = model.Title,
                Description = model.Description,
                ProjectId = model.ProjectId,
                Priority = model.Priority,
            }, Me), "Ticket logged.");

            if (ok) return RedirectToAction(nameof(Details), new { id = newId });
        }
        return View(await BuildCreateModelAsync(model));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var me = Me;
        try
        {
            var ticket = await _tickets.GetTicketAsync(id, me);

            var next = TicketWorkflow.AllowedNextStatuses(ticket.Status, me.Role);
            // A developer may only work on tickets assigned to them.
            if (me.Role == Roles.Developer && ticket.AssigneeId != me.Id) next = Array.Empty<string>();

            var vm = new TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = await _tickets.GetCommentsAsync(id, me),
                Audit = await _tickets.GetAuditAsync(id, me),
                NextStatuses = next,
                CanAssign = me.Role == Roles.Admin && ticket.Status != TicketStatus.Closed,
                Developers = me.Role == Roles.Admin
                    ? ToItems(await _users.GetDevelopersAsync(), u => u.Id, u => u.FullName)
                    : new(),
            };
            return View(vm);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Assign(int id, string assigneeId)
    {
        if (string.IsNullOrWhiteSpace(assigneeId))
            TempData["Error"] = "Choose a developer.";
        else
            await TryAsync(() => _tickets.AssignAsync(id, assigneeId, Me), "Ticket assigned.");

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        await TryAsync(() => _tickets.ChangeStatusAsync(id, status, Me), $"Status changed to {status}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(int id, string text, int? parentId)
    {
        await TryAsync(() => _tickets.AddCommentAsync(id, text, parentId, Me), "Comment added.");
        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    private async Task<CreateTicketViewModel> BuildCreateModelAsync(CreateTicketViewModel model)
    {
        model.Projects = ToItems(await _projects.GetAllAsync(activeOnly: true), p => p.ProjectId.ToString(), p => p.Name);
        return model;
    }
}
