using TrackIt.Core.Dtos;
using TrackIt.Core.Entities;
using TrackIt.Core.Interfaces;

namespace TrackIt.Tests;

/// <summary>Hand-written in-memory fakes. Simple enough to read in an interview.</summary>
public class FakeTicketRepository : ITicketRepository
{
    public Dictionary<int, TicketDetail> Tickets { get; } = new();
    public List<(int Id, string NewStatus, string By)> StatusCalls { get; } = new();
    public List<(int Id, string AssigneeId, string By)> AssignCalls { get; } = new();
    public TicketFilter? LastFilter { get; private set; }
    private int _nextId = 1;

    public Task<int> CreateAsync(CreateTicketRequest request, string reporterId)
    {
        var id = _nextId++;
        Tickets[id] = new TicketDetail
        {
            TicketId = id, Title = request.Title, Description = request.Description,
            ProjectId = request.ProjectId, ReporterId = reporterId, Priority = request.Priority, Status = "Open",
        };
        return Task.FromResult(id);
    }

    public Task AssignAsync(int ticketId, string assigneeId, string changedBy)
    {
        AssignCalls.Add((ticketId, assigneeId, changedBy));
        if (Tickets.TryGetValue(ticketId, out var t))
        {
            t.AssigneeId = assigneeId;
            t.AssigneeEmail = assigneeId + "@example.com";
        }
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(int ticketId, string newStatus, string changedBy)
    {
        StatusCalls.Add((ticketId, newStatus, changedBy));
        Tickets[ticketId].Status = newStatus;
        return Task.CompletedTask;
    }

    public Task<PagedResult<TicketListItem>> GetFilteredAsync(TicketFilter filter)
    {
        LastFilter = filter;
        return Task.FromResult(new PagedResult<TicketListItem> { Page = filter.Page, PageSize = filter.PageSize });
    }

    public Task<TicketDetail?> GetByIdAsync(int ticketId) =>
        Task.FromResult(Tickets.TryGetValue(ticketId, out var t) ? t : null);

    public Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int ticketId) =>
        Task.FromResult<IReadOnlyList<AuditEntryDto>>(Array.Empty<AuditEntryDto>());

    public TicketDetail Add(int id, string status, string reporterId, string? assigneeId = null, string priority = "Medium")
    {
        var t = new TicketDetail
        {
            TicketId = id, Title = "Test ticket", Description = "Body", ProjectId = 1,
            ReporterId = reporterId, ReporterEmail = reporterId + "@example.com",
            AssigneeId = assigneeId, AssigneeEmail = assigneeId is null ? null : assigneeId + "@example.com",
            Priority = priority, Status = status,
        };
        Tickets[id] = t;
        return t;
    }
}

public class FakeCommentRepository : ICommentRepository
{
    public List<CommentDto> Comments { get; } = new();
    public List<TicketComment> Added { get; } = new();

    public Task<IReadOnlyList<CommentDto>> GetByTicketAsync(int ticketId) =>
        Task.FromResult<IReadOnlyList<CommentDto>>(Comments.Where(c => c.TicketId == ticketId).ToList());

    public Task<CommentDto?> GetByIdAsync(int commentId) =>
        Task.FromResult(Comments.FirstOrDefault(c => c.CommentId == commentId));

    public Task<int> AddAsync(TicketComment comment)
    {
        Added.Add(comment);
        return Task.FromResult(Added.Count);
    }
}

public class FakeProjectRepository : IProjectRepository
{
    public List<Project> Projects { get; } = new()
    {
        new Project { ProjectId = 1, Name = "Portal", IsActive = true },
        new Project { ProjectId = 2, Name = "Retired", IsActive = false },
    };

    public Task<IReadOnlyList<Project>> GetAllAsync(bool activeOnly) =>
        Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => !activeOnly || p.IsActive).ToList());

    public Task<Project?> GetByIdAsync(int projectId) =>
        Task.FromResult(Projects.FirstOrDefault(p => p.ProjectId == projectId));

    public Task AddAsync(Project project) { Projects.Add(project); return Task.CompletedTask; }
    public Task UpdateAsync(Project project) => Task.CompletedTask;
}

public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject)> Sent { get; } = new();
    public bool ShouldFail { get; set; }

    public Task SendAsync(string toAddress, string subject, string htmlBody)
    {
        if (ShouldFail) throw new InvalidOperationException("SMTP is down");
        Sent.Add((toAddress, subject));
        return Task.CompletedTask;
    }
}

public class FakeWebhookService : IWebhookService
{
    public List<int> Notified { get; } = new();

    public Task NotifyCriticalTicketAsync(int ticketId, string title, string projectName)
    {
        Notified.Add(ticketId);
        return Task.CompletedTask;
    }
}
