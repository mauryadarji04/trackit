using TrackIt.Core.Dtos;
using TrackIt.Core.Entities;

namespace TrackIt.Core.Interfaces;

// ---------- Repositories (talk to SQL Server) ----------

public interface ITicketRepository
{
    Task<int> CreateAsync(CreateTicketRequest request, string reporterId);
    Task AssignAsync(int ticketId, string assigneeId, string changedBy);
    Task UpdateStatusAsync(int ticketId, string newStatus, string changedBy);
    Task<PagedResult<TicketListItem>> GetFilteredAsync(TicketFilter filter);
    Task<TicketDetail?> GetByIdAsync(int ticketId);
    Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int ticketId);
}

public interface ICommentRepository
{
    Task<IReadOnlyList<CommentDto>> GetByTicketAsync(int ticketId);
    Task<CommentDto?> GetByIdAsync(int commentId);
    Task<int> AddAsync(TicketComment comment);
}

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(bool activeOnly);
    Task<Project?> GetByIdAsync(int projectId);
    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
}

public interface IDashboardRepository
{
    Task<DashboardSummary> GetSummaryAsync();
    Task<IReadOnlyList<DeveloperStat>> GetDeveloperStatsAsync();
    Task<IReadOnlyList<DeveloperRank>> GetLeaderboardAsync();
    Task<WeeklySummary> GetWeeklySummaryAsync(DateTime weekStart, DateTime weekEnd);
}

/// <summary>Users and roles (implemented with ASP.NET Core Identity).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<UserSummary>> GetAllAsync();
    Task<IReadOnlyList<UserSummary>> GetDevelopersAsync();
    Task<UserSummary?> GetByIdAsync(string userId);
    Task SetRoleAsync(string userId, string role);
    Task SetActiveAsync(string userId, bool isActive);
}

// ---------- Integrations ----------

public interface IEmailService
{
    Task SendAsync(string toAddress, string subject, string htmlBody);
}

public interface IWebhookService
{
    Task NotifyCriticalTicketAsync(int ticketId, string title, string projectName);
}

public interface IPdfReportGenerator
{
    byte[] BuildWeeklyReport(WeeklySummary summary);
}

// ---------- Services (business rules, no SQL, no HTTP) ----------

public interface ITicketService
{
    Task<int> CreateTicketAsync(CreateTicketRequest request, CurrentUser user);
    Task AssignAsync(int ticketId, string assigneeId, CurrentUser user);
    Task ChangeStatusAsync(int ticketId, string newStatus, CurrentUser user);
    Task<PagedResult<TicketListItem>> SearchAsync(TicketFilter filter, CurrentUser user);
    Task<TicketDetail> GetTicketAsync(int ticketId, CurrentUser user);
    Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int ticketId, CurrentUser user);
    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(int ticketId, CurrentUser user);
    Task AddCommentAsync(int ticketId, string text, int? parentCommentId, CurrentUser user);
}

public interface IDashboardService
{
    Task<DashboardData> GetDashboardAsync();
}

public interface IReportService
{
    /// <summary>Report for the 7 days that start at <paramref name="weekStart"/>.</summary>
    Task<byte[]> BuildWeeklyReportAsync(DateTime weekStart);
}
