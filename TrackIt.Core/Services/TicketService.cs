using System.Net;
using Microsoft.Extensions.Logging;
using TrackIt.Core.Dtos;
using TrackIt.Core.Entities;
using TrackIt.Core.Interfaces;

namespace TrackIt.Core.Services;

/// <summary>
/// Business rules for tickets. It depends only on interfaces (dependency inversion),
/// so it can be unit-tested with fakes and never touches SQL or HTTP directly.
/// </summary>
public class TicketService : ITicketService
{
    private readonly ITicketRepository _tickets;
    private readonly ICommentRepository _comments;
    private readonly IProjectRepository _projects;
    private readonly IEmailService _email;
    private readonly IWebhookService _webhook;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ITicketRepository tickets,
        ICommentRepository comments,
        IProjectRepository projects,
        IEmailService email,
        IWebhookService webhook,
        ILogger<TicketService> logger)
    {
        _tickets = tickets;
        _comments = comments;
        _projects = projects;
        _email = email;
        _webhook = webhook;
        _logger = logger;
    }

    public async Task<int> CreateTicketAsync(CreateTicketRequest request, CurrentUser user)
    {
        if (user.Role is not (Roles.Reporter or Roles.Admin))
            throw new ForbiddenException("Only reporters and admins can log tickets.");

        var title = request.Title?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;

        if (title.Length is 0 or > 200)
            throw new BusinessRuleException("Title is required and must be at most 200 characters.");
        if (description.Length == 0)
            throw new BusinessRuleException("Description is required.");
        if (!TicketPriority.All.Contains(request.Priority))
            throw new BusinessRuleException("Choose a valid priority.");

        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null || !project.IsActive)
            throw new BusinessRuleException("Choose an active project.");

        request.Title = title;
        request.Description = description;
        var ticketId = await _tickets.CreateAsync(request, user.Id);

        if (request.Priority == TicketPriority.Critical)
            await SafeAsync(() => _webhook.NotifyCriticalTicketAsync(ticketId, title, project.Name),
                "critical-ticket webhook");

        return ticketId;
    }

    public async Task AssignAsync(int ticketId, string assigneeId, CurrentUser user)
    {
        if (user.Role != Roles.Admin)
            throw new ForbiddenException("Only admins can assign tickets.");

        await _tickets.AssignAsync(ticketId, assigneeId, user.Id);

        var ticket = await _tickets.GetByIdAsync(ticketId);
        if (ticket?.AssigneeEmail is { Length: > 0 } to)
        {
            var body = $"<p>Ticket <b>#{ticket.TicketId}</b> was assigned to you.</p>" +
                       $"<p><b>{WebUtility.HtmlEncode(ticket.Title)}</b> ({ticket.Priority}, due {ticket.DueAt:u})</p>";
            await SafeAsync(() => _email.SendAsync(to, $"[TrackIt] Ticket #{ticket.TicketId} assigned to you", body),
                "assignment email");
        }
    }

    public async Task ChangeStatusAsync(int ticketId, string newStatus, CurrentUser user)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId)
                     ?? throw new NotFoundException("Ticket not found.");

        if (!TicketStatus.All.Contains(newStatus))
            throw new BusinessRuleException("Unknown status.");
        if (!TicketWorkflow.IsRoleAllowed(user.Role, newStatus))
            throw new ForbiddenException($"Your role cannot move a ticket to '{newStatus}'.");
        if (user.Role == Roles.Developer && ticket.AssigneeId != user.Id)
            throw new ForbiddenException("You can only change tickets assigned to you.");
        if (!TicketWorkflow.IsValidTransition(ticket.Status, newStatus))
            throw new BusinessRuleException($"A ticket cannot go from '{ticket.Status}' to '{newStatus}'.");

        await _tickets.UpdateStatusAsync(ticketId, newStatus, user.Id);

        if (newStatus == TicketStatus.Closed)
        {
            var body = $"<p>Ticket <b>#{ticket.TicketId}</b> ({WebUtility.HtmlEncode(ticket.Title)}) has been closed.</p>";
            foreach (var to in new[] { ticket.ReporterEmail, ticket.AssigneeEmail }
                         .Where(e => !string.IsNullOrWhiteSpace(e)).Distinct())
            {
                await SafeAsync(() => _email.SendAsync(to!, $"[TrackIt] Ticket #{ticket.TicketId} closed", body),
                    "closure email");
            }
        }
    }

    public Task<PagedResult<TicketListItem>> SearchAsync(TicketFilter filter, CurrentUser user)
    {
        // Row-level scope: reporters see their own tickets, developers see their assigned ones.
        if (user.Role == Roles.Reporter) filter.ReporterId = user.Id;
        else if (user.Role == Roles.Developer) filter.AssigneeId = user.Id;

        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 100) filter.PageSize = 10;

        return _tickets.GetFilteredAsync(filter);
    }

    public async Task<TicketDetail> GetTicketAsync(int ticketId, CurrentUser user)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId)
                     ?? throw new NotFoundException("Ticket not found.");

        var canSee = user.Role switch
        {
            Roles.Admin     => true,
            Roles.Developer => ticket.AssigneeId == user.Id || ticket.ReporterId == user.Id,
            _               => ticket.ReporterId == user.Id,
        };
        // Same message as "missing" so users cannot probe for other people's ticket numbers.
        if (!canSee) throw new NotFoundException("Ticket not found.");

        return ticket;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int ticketId, CurrentUser user)
    {
        await GetTicketAsync(ticketId, user);
        return await _tickets.GetAuditAsync(ticketId);
    }

    public async Task<IReadOnlyList<CommentDto>> GetCommentsAsync(int ticketId, CurrentUser user)
    {
        await GetTicketAsync(ticketId, user);
        var flat = await _comments.GetByTicketAsync(ticketId);
        return CommentThreading.Build(flat);
    }

    public async Task AddCommentAsync(int ticketId, string text, int? parentCommentId, CurrentUser user)
    {
        await GetTicketAsync(ticketId, user);

        text = text?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 2000)
            throw new BusinessRuleException("A comment must be between 1 and 2000 characters.");

        if (parentCommentId is { } parentId)
        {
            var parent = await _comments.GetByIdAsync(parentId);
            if (parent is null || parent.TicketId != ticketId)
                throw new BusinessRuleException("The comment you are replying to does not exist.");
        }

        await _comments.AddAsync(new TicketComment
        {
            TicketId = ticketId,
            UserId = user.Id,
            ParentCommentId = parentCommentId,
            CommentText = text,
        });
    }

    /// <summary>Notifications are best-effort: a mail or Slack outage must not undo a ticket change.</summary>
    private async Task SafeAsync(Func<Task> action, string what)
    {
        try { await action(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send {What}", what); }
    }
}
