namespace CourseCommander.Entities;

public class DailySalesMetric
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string SourceSystemName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal FoodAndBeverageRevenue { get; set; }
    public decimal ProShopRevenue { get; set; }
    public decimal AlcoholRevenue { get; set; }
    public decimal RangeBallRevenue { get; set; }
    public int TransactionCount { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
