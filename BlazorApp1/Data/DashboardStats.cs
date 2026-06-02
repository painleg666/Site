namespace MyBlazorSite.Data;

public class DashboardStats
{
    public int NewRequestsCount { get; set; }

    public int InProgressCount { get; set; }

    public int EstimatedCount { get; set; }

    public int CompletedCount { get; set; }

    public int TotalRequestsCount { get; set; }

    public decimal AverageCheck { get; set; }

    public decimal TotalAmount { get; set; }

    public int[] StatusCounts => new[]
    {
        NewRequestsCount,
        InProgressCount,
        EstimatedCount,
        CompletedCount
    };
}