using Microsoft.EntityFrameworkCore;

namespace MyBlazorSite.Data;

public class DashboardStatsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DashboardStatsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var groupedByStatus = await db.RepairRequests
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = string.IsNullOrWhiteSpace(g.Key) ? "" : g.Key.Trim(),
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var calculatedCosts = await db.RepairRequests
            .AsNoTracking()
            .Where(x => x.CalculatedCost > 0)
            .Select(x => x.CalculatedCost)
            .ToListAsync();

        var totalRequests = await db.RepairRequests
            .AsNoTracking()
            .CountAsync();

        return new DashboardStats
        {
            NewRequestsCount = GetStatusCount(groupedByStatus, "Новая"),
            InProgressCount = GetStatusCount(groupedByStatus, "В обработке"),
            EstimatedCount = GetStatusCount(groupedByStatus, "Оценена"),
            CompletedCount = GetStatusCount(groupedByStatus, "Завершена"),

            TotalRequestsCount = totalRequests,

            AverageCheck = calculatedCosts.Count == 0
                ? 0
                : Math.Round(calculatedCosts.Average(), 0),

            TotalAmount = calculatedCosts.Sum()
        };
    }

    private static int GetStatusCount(Dictionary<string, int> statuses, string status)
    {
        return statuses.TryGetValue(status, out var count) ? count : 0;
    }
}
