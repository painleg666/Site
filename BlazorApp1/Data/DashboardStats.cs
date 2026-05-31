namespace MyBlazorSite.Data;

public class DashboardStats
{
    public int InProgressCount { get; set; }
    public int NewRequestsCount { get; set; }
    public decimal AverageCheck { get; set; }

    public int TotalRequestsCount { get; set; }
    public int CompletedCount { get; set; }
    public decimal TotalAmount { get; set; }
}