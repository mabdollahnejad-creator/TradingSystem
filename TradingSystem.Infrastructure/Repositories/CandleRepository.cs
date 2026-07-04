using Microsoft.EntityFrameworkCore;
using TradingSystem.Application.Abstractions;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;
using TradingSystem.Infrastructure.Persistence;

namespace TradingSystem.Infrastructure.Repositories;

public class CandleRepository : ICandleRepository
{
    private readonly AppDbContext _db;

    public CandleRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(List<Candle> candles)
    {
        var candleList = candles.ToList();
        var uniqueCandles = new List<Candle>();

        // ✅ استفاده از ValueTuple به جای string (سریع‌تر و بهینه‌تر)
        var seenKeys = new HashSet<(int CryptoId, DataSource Source, string Exchange, Timeframe Timeframe, long Ticks)>();

        foreach (var candle in candleList)
        {
            var key = (candle.CryptocurrencyId, candle.Source, candle.Exchange, candle.Timeframe, candle.OpenTime.Ticks);
            if (seenKeys.Add(key))
            {
                uniqueCandles.Add(candle);
            }
        }

        _db.Candles.AddRange(uniqueCandles);  // ✅ اصلاح: _db به جای _context
    }

    public async Task<List<DateTime>> GetExistingOpenTimesAsync(
        int cryptocurrencyId,
        DataSource source,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to)
    {
        return await _db.Set<Candle>()
            .Where(c => c.CryptocurrencyId == cryptocurrencyId
                     && c.Source == source
                     && c.Exchange == exchange
                     && c.Timeframe == timeframe
                     && c.OpenTime >= from
                     && c.OpenTime <= to)
            .Select(c => c.OpenTime)
            .ToListAsync();
    }

    public async Task<List<Candle>> GetRangeAsync(
        int cryptocurrencyId,
        DataSource source,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to)
    {
        return await _db.Set<Candle>()
            .Where(c => c.CryptocurrencyId == cryptocurrencyId
                     && c.Source == source
                     && c.Exchange == exchange
                     && c.Timeframe == timeframe
                     && c.OpenTime >= from
                     && c.OpenTime <= to)
            .OrderBy(c => c.OpenTime)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}