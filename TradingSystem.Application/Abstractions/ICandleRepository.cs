using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface ICandleRepository
{
    Task AddRangeAsync(List<Candle> candles);
    Task<List<DateTime>> GetExistingOpenTimesAsync(
        int cryptocurrencyId,
        DataSource source,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to);
    Task<List<Candle>> GetRangeAsync(
        int cryptocurrencyId,
        DataSource source,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to);
    Task SaveChangesAsync();
}