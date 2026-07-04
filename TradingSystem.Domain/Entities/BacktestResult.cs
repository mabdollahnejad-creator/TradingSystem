using TradingSystem.Domain.Entities;

public class BacktestResult
{
    public int Id { get; set; }

    public int BacktestRunId { get; set; }

    public int CryptocurrencyId { get; set; }

    public decimal NetProfit { get; set; }

    public decimal WinRate { get; set; }

    public decimal ProfitFactor { get; set; }

    public decimal MaxDrawdown { get; set; }

    public int TotalTrades { get; set; }

    public double SharpeRatio { get; set; }

    public BacktestRun BacktestRun
    {
        get;
        set;
    } = null!;

    public Cryptocurrency Cryptocurrency
    {
        get;
        set;
    } = null!;
}