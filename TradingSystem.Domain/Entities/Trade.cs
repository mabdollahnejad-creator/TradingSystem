using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

public class Trade
{
    public int Id { get; set; }

    public int BacktestRunId { get; set; }

    public int CryptocurrencyId { get; set; }

    public TradeDirection Direction { get; set; }

    public DateTime EntryTime { get; set; }

    public decimal EntryPrice { get; set; }

    public DateTime? ExitTime { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal Profit { get; set; }

    public string ExitReason { get; set; }
        = string.Empty;

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