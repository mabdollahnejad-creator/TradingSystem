namespace TradingSystem.Domain.Entities;

public class Cryptocurrency
{
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CoinGeckoId { get; set; }

    // ✅ فیلدهای جدید برای ردیابی موجود بودن در صرافی‌ها
    public bool IsAvailableInBinance { get; set; }
    public bool IsAvailableInNobitex { get; set; }
    public bool IsAvailableInWallex { get; set; }

    // ✅ فیلد محاسباتی: آیا در حداقل یک صرافی ایرانی قابل ترید است؟
    public bool IsTradableInIran => IsAvailableInNobitex || IsAvailableInWallex;

    public int? MarketCapRank { get; set; }
    public decimal MarketCap { get; set; }
    public decimal Volume24h { get; set; }
    public string? LogoUrl { get; set; }
    public byte[]? LogoBinary { get; set; }

    public bool IsActive { get; set; }
    public DateTime? LastUpdated { get; set; }

    // Navigation Property
    public ICollection<Candle> Candles { get; set; } = new List<Candle>();
}