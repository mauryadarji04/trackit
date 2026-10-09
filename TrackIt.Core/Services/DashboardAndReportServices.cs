using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;

namespace TrackIt.Core.Services;

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;

    public DashboardService(IDashboardRepository repo) => _repo = repo;

    public async Task<DashboardData> GetDashboardAsync() => new()
    {
        Summary = await _repo.GetSummaryAsync(),
        DeveloperStats = await _repo.GetDeveloperStatsAsync(),
        Leaderboard = await _repo.GetLeaderboardAsync(),
    };
}

public class ReportService : IReportService
{
    private readonly IDashboardRepository _repo;
    private readonly IPdfReportGenerator _pdf;

    public ReportService(IDashboardRepository repo, IPdfReportGenerator pdf)
    {
        _repo = repo;
        _pdf = pdf;
    }

    public async Task<byte[]> BuildWeeklyReportAsync(DateTime weekStart)
    {
        var start = weekStart.Date;
        var summary = await _repo.GetWeeklySummaryAsync(start, start.AddDays(7));
        return _pdf.BuildWeeklyReport(summary);
    }
}
