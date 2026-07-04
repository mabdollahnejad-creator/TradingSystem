public class BacktestRun
{
    public int Id { get; set; }

    public int StrategyDefinitionId { get; set; }

    public DateTime RunDate { get; set; }
        = DateTime.UtcNow;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public StrategyDefinition Strategy
    {
        get;
        set;
    } = null!;

    public ICollection<BacktestResult>
        Results
    { get; set; }
            = new List<BacktestResult>();
}