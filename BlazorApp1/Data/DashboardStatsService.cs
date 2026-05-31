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

        var requests = await db.RepairRequests
            .AsNoTracking()
            .ToListAsync();

        var totalRequests = requests.Count;

        var completedRequests = requests
            .Where(x => x.Status == "Завершена")
            .ToList();

        var paidOrCalculatedRequests = requests
            .Where(x => x.CalculatedCost > 0)
            .ToList();

        return new DashboardStats
        {
            InProgressCount = requests.Count(x => x.Status == "В обработке"),
            NewRequestsCount = requests.Count(x => x.Status == "Новая"),

            AverageCheck = paidOrCalculatedRequests.Count == 0
                ? 0
                : Math.Round(paidOrCalculatedRequests.Average(x => x.CalculatedCost), 0),

            TotalRequestsCount = totalRequests,
            CompletedCount = completedRequests.Count,

            TotalAmount = paidOrCalculatedRequests.Sum(x => x.CalculatedCost)
        };
    }
}