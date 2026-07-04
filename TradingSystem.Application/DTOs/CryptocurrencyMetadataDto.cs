namespace TradingSystem.Application.DTOs;

// ✅ DTO مخصوص export/import متادیتا (بدون کندل‌ها و بدون computed properties)
public class CryptocurrencyMetadataDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CoinGeckoId { get; set; }
    public bool IsAvailableInBinance { get; set; }
    public bool IsAvailableInNobitex { get; set; }
    public bool IsAvailableInWallex { get; set; }
    public int? MarketCapRank { get; set; }
    public decimal MarketCap { get; set; }
    public decimal Volume24h { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }

    // ✅ اصلاح: DateTime? برای سازگاری با Entity
    public DateTime? LastUpdated { get; set; }
}