using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Infrastructure.Repositories;

/// <summary>Dapper repository: ticket workflows call the stored procedures.</summary>
public class TicketRepository : ITicketRepository
{
    private readonly ISqlConnectionFactory _factory;

    public TicketRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(CreateTicketRequest request, string reporterId)
    {
        try
        {
            await using var conn = _factory.Create();
            return await conn.QuerySingleAsync<int>(
                "dbo.usp_CreateTicket",
                new
                {
                    Title = request.Title,
                    Description = request.Description,
                    ProjectId = request.ProjectId,
                    ReporterId = reporterId,
                    Priority = request.Priority,
                },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (SqlErrors.IsBusinessError(ex))
        {
            throw SqlErrors.ToDomainException(ex);
        }
    }

    public async Task AssignAsync(int ticketId, string assigneeId, string changedBy)
    {
        try
        {
            await using var conn = _factory.Create();
            await conn.ExecuteAsync(
                "dbo.usp_AssignTicket",
                new { TicketId = ticketId, AssigneeId = assigneeId, ChangedBy = changedBy },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (SqlErrors.IsBusinessError(ex))
        {
            throw SqlErrors.ToDomainException(ex);
        }
    }

    public async Task UpdateStatusAsync(int ticketId, string newStatus, string changedBy)
    {
        try
        {
            await using var conn = _factory.Create();
            await conn.ExecuteAsync(
                "dbo.usp_UpdateTicketStatus",
                new { TicketId = ticketId, NewStatus = newStatus, ChangedBy = changedBy },
                commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (SqlErrors.IsBusinessError(ex))
        {
            throw SqlErrors.ToDomainException(ex);
        }
    }

    public async Task<PagedResult<TicketListItem>> GetFilteredAsync(TicketFilter filter)
    {
        await using var conn = _factory.Create();
        var rows = (await conn.QueryAsync<TicketListItem>(
            "dbo.usp_GetTicketsFiltered",
            new
            {
                Status = Blank(filter.Status),
                Priority = Blank(filter.Priority),
                AssigneeId = Blank(filter.AssigneeId),
                ProjectId = filter.ProjectId,
                ReporterId = Blank(filter.ReporterId),
                Search = Blank(filter.Search),
                PageNumber = filter.Page,
                PageSize = filter.PageSize,
            },
            commandType: CommandType.StoredProcedure)).ToList();

        return new PagedResult<TicketListItem>
        {
            Items = rows,
            TotalCount = rows.Count > 0 ? rows[0].TotalCount : 0,
            Page = filter.Page,
            PageSize = filter.PageSize,
        };
    }

    public async Task<TicketDetail?> GetByIdAsync(int ticketId)
    {
        const string sql = @"
            SELECT t.TicketId, t.Title, t.Description, t.ProjectId, p.Name AS ProjectName,
                   t.ReporterId, rep.FullName AS ReporterName, rep.Email AS ReporterEmail,
                   t.AssigneeId, asg.FullName AS AssigneeName, asg.Email AS AssigneeEmail,
                   t.Priority, t.Status, t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.DueAt,
                   CAST(dbo.fn_IsSlaBreached(t.TicketId) AS BIT) AS IsSlaBreached
            FROM dbo.Tickets t
            JOIN dbo.Projects p      ON p.ProjectId = t.ProjectId
            JOIN dbo.AspNetUsers rep ON rep.Id = t.ReporterId
            LEFT JOIN dbo.AspNetUsers asg ON asg.Id = t.AssigneeId
            WHERE t.TicketId = @TicketId;";

        await using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<TicketDetail>(sql, new { TicketId = ticketId });
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int ticketId)
    {
        const string sql = @"
            SELECT a.AuditId, a.TicketId, a.OldStatus, a.NewStatus,
                   oa.FullName AS OldAssigneeName, na.FullName AS NewAssigneeName,
                   cb.FullName AS ChangedByName, a.ChangedAt
            FROM dbo.TicketAuditLog a
            LEFT JOIN dbo.AspNetUsers oa ON oa.Id = a.OldAssigneeId
            LEFT JOIN dbo.AspNetUsers na ON na.Id = a.NewAssigneeId
            LEFT JOIN dbo.AspNetUsers cb ON cb.Id = a.ChangedBy
            WHERE a.TicketId = @TicketId
            ORDER BY a.ChangedAt DESC, a.AuditId DESC;";

        await using var conn = _factory.Create();
        return (await conn.QueryAsync<AuditEntryDto>(sql, new { TicketId = ticketId })).ToList();
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
