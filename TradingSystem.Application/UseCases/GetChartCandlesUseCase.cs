using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.UseCases;

public class GetChartCandlesUseCase
{
    private readonly ICandleRepository _candleRepo;
    private readonly ICryptoRepository _cryptoRepo;

    public GetChartCandlesUseCase(ICandleRepository candleRepo, ICryptoRepository cryptoRepo)
    {
        _candleRepo = candleRepo;
        _cryptoRepo = cryptoRepo;
    }

    // اضافه کردن پارامترهای Source و Exchange با مقادیر پیش‌فرض
    public async Task<List<CandleDto>> ExecuteAsync(
        string symbol,
        string timeframeStr,
        int limit,
        DataSource source = DataSource.Global,
        string exchange = "Binance")
    {
        if (!Enum.TryParse<Timeframe>(timeframeStr, true, out var timeframe))
            throw new ArgumentException($"Invalid timeframe: {timeframeStr}");

        var crypto = await _cryptoRepo.GetBySymbolAsync(symbol.ToUpperInvariant());
        if (crypto == null) return new List<CandleDto>();

        var to = DateTime.UtcNow;
        var from = to.AddYears(-10);

        // ✅ فراخوانی با امضای جدید (۶ آرگومان)
        var candles = await _candleRepo.GetRangeAsync(
            crypto.Id,
            source,
            exchange,
            timeframe,
            from,
            to);

        return candles
            .OrderByDescending(c => c.OpenTime)
            .Take(limit)
            .OrderBy(c => c.OpenTime)
            .Select(c => new CandleDto
            {
                OpenTime = c.OpenTime,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                Volume = c.Volume
            })
            .ToList();
    }
}