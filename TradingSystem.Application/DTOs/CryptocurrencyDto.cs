namespace TradingSystem.Application.DTOs;

public class CryptocurrencyDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? MarketCapRank { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? MarketCap { get; set; }
    public decimal? Volume24h { get; set; }
}