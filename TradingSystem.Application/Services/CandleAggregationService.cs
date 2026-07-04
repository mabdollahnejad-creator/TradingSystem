using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Services;

public class CandleAggregationService
{
    public List<Candle> Aggregate(
        List<Candle> source,
        Timeframe targetTimeframe)
    {
        if (source == null || source.Count == 0)
            return new List<Candle>();

        var ordered = source
            .OrderBy(x => x.OpenTime)
            .ToList();

        var grouped = ordered
            .GroupBy(x => AlignToBucket(x.OpenTime, targetTimeframe))
            .ToList();

        var result = new List<Candle>();

        foreach (var group in grouped)
        {
            var candles = group.ToList();

            result.Add(new Candle
            {
                CryptocurrencyId = candles.First().CryptocurrencyId,
                Timeframe = targetTimeframe,
                OpenTime = group.Key,

                Open = candles.First().Open,
                Close = candles.Last().Close,

                High = candles.Max(x => x.High),
                Low = candles.Min(x => x.Low),

                Volume = candles.Sum(x => x.Volume)
            });
        }

        return result
            .OrderBy(x => x.OpenTime)
            .ToList();
    }

    private DateTime AlignToBucket(DateTime time, Timeframe tf)
    {
        return tf switch
        {
           // Timeframe.H1 => new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0),

          //  Timeframe.H4 =>
          //      new DateTime(time.Year, time.Month, time.Day, time.Hour - (time.Hour % 4), 0, 0),

         //   Timeframe.D1 => new DateTime(time.Year, time.Month, time.Day, 0, 0, 0),

         //   _ => throw new ArgumentOutOfRangeException(nameof(tf))
        };
    }
}