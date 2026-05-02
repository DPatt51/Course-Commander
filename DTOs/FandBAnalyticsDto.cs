namespace CourseCommander.DTOs;

public class FandBAnalyticsDto
{
    public DateOnly Date { get; set; }
    public decimal? FoodAndBeverageRevenue { get; set; }
    public decimal? FandBRevenuePerRound { get; set; }
    public decimal? AlcoholRevenue { get; set; }
    public decimal? AlcoholSharePercent { get; set; }
    public decimal? RangeBallRevenue { get; set; }
    public decimal? RangeBallRevenuePerRound { get; set; }
    public decimal? AverageFandBRevenuePerRound { get; set; }
    public decimal? AverageAlcoholSharePercent { get; set; }
    public decimal? AverageRangeBallRevenuePerRound { get; set; }
}
