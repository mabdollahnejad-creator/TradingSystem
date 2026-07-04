public class StrategyDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IndicatorsJson { get; set; }
        = "[]";

    public DateTime CreatedDate { get; set; }
        = DateTime.UtcNow;
}