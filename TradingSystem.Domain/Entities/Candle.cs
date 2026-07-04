using TradingSystem.Domain.Enums;

namespace TradingSystem.Domain.Entities;

public class Candle
{
    public long Id { get; set; }
    public int CryptocurrencyId { get; set; }
    public Cryptocurrency Cryptocurrency { get; set; } = default!;

    // ✅ فیلدهای جدید برای پشتیبانی از چند منبع داده
    public DataSource Source { get; set; }
    public string Exchange { get; set; } = string.Empty;

    public Timeframe Timeframe { get; set; }
    public DateTime OpenTime { get; set; }

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}