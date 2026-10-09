using System.Data;
using Dapper;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Infrastructure.Repositories;

public class DashboardRepository : IDashboardRepository
{
    // Row shapes returned by the stored procedures. SUM() over zero rows is NULL, hence int?.
    private sealed class SummaryTotalsRow
    {
        public int? OpenCount { get; set; }
        public int? ClosedCount { get; set; }
        public int? SlaBreaches { get; set; }
    }

    private sealed class WeeklyTotalsRow
    {
        public int Opened { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public int StillOpen { get; set; }
        public int SlaBreaches { get; set; }
    }

    private readonly ISqlConnectionFactory _factory;

    public DashboardRepository(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        await using var conn = _factory.Create();
        // usp_GetDashboardSummary returns 4 result sets: status, priority, totals, aging.
        using var grid = await conn.QueryMultipleAsync("dbo.usp_GetDashboardSummary",
            commandType: CommandType.StoredProcedure);

        var byStatus = (await grid.ReadAsync<LabelCount>()).ToList();
        var byPriority = (await grid.ReadAsync<LabelCount>()).ToList();
        var totals = await grid.ReadSingleAsync<SummaryTotalsRow>();
        var aging = (await grid.ReadAsync<LabelCount>()).ToList();

        return new DashboardSummary
        {
            OpenCount = totals.OpenCount ?? 0,
            ClosedCount = totals.ClosedCount ?? 0,
            SlaBreaches = totals.SlaBreaches ?? 0,
            ByStatus = byStatus,
            ByPriority = byPriority,
            Aging = aging,
        };
    }

    public async Task<IReadOnlyList<DeveloperStat>> GetDeveloperStatsAsync()
    {
        await using var conn = _factory.Create();
        return (await conn.QueryAsync<DeveloperStat>("dbo.usp_GetResolutionStatsByDeveloper",
            commandType: CommandType.StoredProcedure)).ToList();
    }

    public async Task<IReadOnlyList<DeveloperRank>> GetLeaderboardAsync()
    {
        await using var conn = _factory.Create();
        return (await conn.QueryAsync<DeveloperRank>(
            "SELECT DeveloperId, DeveloperName, ClosedCount, Rnk FROM dbo.vw_DeveloperMonthlyRank ORDER BY Rnk, DeveloperName"))
            .ToList();
    }

    public async Task<WeeklySummary> GetWeeklySummaryAsync(DateTime weekStart, DateTime weekEnd)
    {
        await using var conn = _factory.Create();
        using var grid = await conn.QueryMultipleAsync("dbo.usp_GetWeeklyDefectSummary",
            new { WeekStart = weekStart, WeekEnd = weekEnd },
            commandType: CommandType.StoredProcedure);

        var totals = await grid.ReadSingleAsync<WeeklyTotalsRow>();
        var byPriority = (await grid.ReadAsync<LabelCount>()).ToList();
        var byProject = (await grid.ReadAsync<LabelCount>()).ToList();
        var byDeveloper = (await grid.ReadAsync<LabelCount>()).ToList();

        return new WeeklySummary
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Opened = totals.Opened,
            Resolved = totals.Resolved,
            Closed = totals.Closed,
            StillOpen = totals.StillOpen,
            SlaBreaches = totals.SlaBreaches,
            ByPriority = byPriority,
            ByProject = byProject,
            ResolvedByDeveloper = byDeveloper,
        };
    }
}
