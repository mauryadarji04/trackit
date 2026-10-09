namespace TrackIt.Core.Dtos;

/// <summary>Who is making the request (taken from the login cookie).</summary>
public record CurrentUser(string Id, string Role);

public class CreateTicketRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string Priority { get; set; } = TicketPriority.Medium;
}

public class TicketFilter
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? AssigneeId { get; set; }
    public int? ProjectId { get; set; }
    public string? ReporterId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Property names match the stored-procedure column names so Dapper can map them.
public class TicketListItem
{
    public int TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public int TotalCount { get; set; }
}

public class TicketDetail
{
    public int TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string? ReporterEmail { get; set; }
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeEmail { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsSlaBreached { get; set; }
}

public class CommentDto
{
    public int CommentId { get; set; }
    public int TicketId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}

public class AuditEntryDto
{
    public int AuditId { get; set; }
    public int TicketId { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? OldAssigneeName { get; set; }
    public string? NewAssigneeName { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class LabelCount
{
    public string Label { get; set; } = string.Empty;
    public int Total { get; set; }
}

public class DashboardSummary
{
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }
    public int SlaBreaches { get; set; }
    public List<LabelCount> ByStatus { get; set; } = new();
    public List<LabelCount> ByPriority { get; set; } = new();
    public List<LabelCount> Aging { get; set; } = new();
}

public class DeveloperStat
{
    public string DeveloperId { get; set; } = string.Empty;
    public string DeveloperName { get; set; } = string.Empty;
    public int ResolvedCount { get; set; }
    public double AvgResolutionHours { get; set; }
}

public class DeveloperRank
{
    public string DeveloperId { get; set; } = string.Empty;
    public string DeveloperName { get; set; } = string.Empty;
    public int ClosedCount { get; set; }
    public int Rnk { get; set; }
}

public class DashboardData
{
    public DashboardSummary Summary { get; set; } = new();
    public IReadOnlyList<DeveloperStat> DeveloperStats { get; set; } = Array.Empty<DeveloperStat>();
    public IReadOnlyList<DeveloperRank> Leaderboard { get; set; } = Array.Empty<DeveloperRank>();
}

public class WeeklySummary
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int Opened { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public int StillOpen { get; set; }
    public int SlaBreaches { get; set; }
    public List<LabelCount> ByPriority { get; set; } = new();
    public List<LabelCount> ByProject { get; set; } = new();
    public List<LabelCount> ResolvedByDeveloper { get; set; } = new();
}

public class UserSummary
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
