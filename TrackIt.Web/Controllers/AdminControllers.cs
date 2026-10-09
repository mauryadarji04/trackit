using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackIt.Core;
using TrackIt.Core.Entities;
using TrackIt.Core.Interfaces;
using TrackIt.Web.Models;

namespace TrackIt.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class DashboardController : AppController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index() => View(await _dashboard.GetDashboardAsync());
}

[Authorize(Roles = Roles.Admin)]
public class ProjectsController : AppController
{
    private readonly IProjectRepository _projects;

    public ProjectsController(IProjectRepository projects) => _projects = projects;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _projects.GetAllAsync(activeOnly: false));

    [HttpGet]
    public IActionResult Create() => View("Edit", new ProjectFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _projects.GetByIdAsync(id);
        if (p is null) return NotFound();
        return View(new ProjectFormViewModel
        {
            ProjectId = p.ProjectId, Name = p.Name, Description = p.Description, IsActive = p.IsActive,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(ProjectFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        var name = model.Name.Trim();
        var existing = (await _projects.GetAllAsync(activeOnly: false))
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.ProjectId != model.ProjectId);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Name), "A project with this name already exists.");
            return View("Edit", model);
        }

        if (model.ProjectId == 0)
        {
            await _projects.AddAsync(new Project { Name = name, Description = model.Description, IsActive = model.IsActive });
            TempData["Success"] = "Project created.";
        }
        else
        {
            await _projects.UpdateAsync(new Project
            {
                ProjectId = model.ProjectId, Name = name, Description = model.Description, IsActive = model.IsActive,
            });
            TempData["Success"] = "Project updated.";
        }
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class UsersController : AppController
{
    private readonly IUserDirectory _users;

    public UsersController(IUserDirectory users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _users.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> SetRole(string id, string role)
    {
        if (id == Me.Id)
            TempData["Error"] = "You cannot change your own role.";
        else
            await TryAsync(() => _users.SetRoleAsync(id, role), "Role updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool isActive)
    {
        if (id == Me.Id)
            TempData["Error"] = "You cannot deactivate your own account.";
        else
            await TryAsync(() => _users.SetActiveAsync(id, isActive), isActive ? "User activated." : "User deactivated.");
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class ReportsController : AppController
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet]
    public IActionResult Index()
    {
        // Default to the Monday of the previous full week.
        var today = DateTime.UtcNow.Date;
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return View(new ReportViewModel { WeekStart = today.AddDays(-daysSinceMonday - 7) });
    }

    [HttpPost]
    public async Task<IActionResult> Weekly(ReportViewModel model)
    {
        var pdf = await _reports.BuildWeeklyReportAsync(model.WeekStart);
        return File(pdf, "application/pdf", $"trackit-weekly-{model.WeekStart:yyyy-MM-dd}.pdf");
    }
}
