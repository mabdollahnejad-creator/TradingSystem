using TradingSystem.Application.DTOs;

namespace TradingSystem.Application.Abstractions;

public interface ICoinGeckoMetadataService
{
    Task<List<CryptocurrencyDto>> GetTopCryptocurrenciesAsync(int count);

    // ✅ متد جدید: جستجوی یک نماد خاص در CoinGecko
    Task<CryptocurrencyDto?> SearchBySymbolAsync(string symbol);
}