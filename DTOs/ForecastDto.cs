namespace CourseCommander.DTOs;

public class ForecastDto
{
    public DateOnly Date { get; set; }
    public int PredictedRounds { get; set; }
    public decimal PredictedTotalRevenue { get; set; }
    public decimal PredictedRevenue
    {
        get => PredictedTotalRevenue;
        set => PredictedTotalRevenue = value;
    }
    public decimal PredictedFoodAndBeverageRevenue { get; set; }
    public int PredictedCartRentals { get; set; }
    public decimal PredictedGdd { get; set; }
    public string ConfidenceLevel { get; set; } = "Low";
    public string Explanation { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
