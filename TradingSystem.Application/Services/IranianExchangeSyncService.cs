using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;
using TradingSystem.Application.UseCases;

namespace TradingSystem.Application.Services;

public interface IIranianExchangeSyncService
{
    Task<List<Candle>> DownloadAsync(
        string symbol,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null);
}

public class IranianExchangeSyncService : IIranianExchangeSyncService
{
    private readonly INobitexExchangeService _nobitexService;
    private readonly IWallexExchangeService _wallexService;
    private readonly string _logFile;

    public IranianExchangeSyncService(
        INobitexExchangeService nobitexService,
        IWallexExchangeService wallexService)
    {
        _nobitexService = nobitexService;
        _wallexService = wallexService;
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iranian_sync_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<Candle>> DownloadAsync(
        string symbol,
        string exchange,
        Timeframe timeframe,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        var tfFolder = GetTimeframeFolder(timeframe);

        Log($"[IranianSync] START: {upperSymbol} | {exchange} | {timeframe} | {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

        if (upperSymbol == "USDT")
        {
            Log($"[IranianSync] Skipping USDT for {exchange}");
            return new List<Candle>();
        }

        var gaps = FindDataGaps(upperSymbol, exchange, tfFolder, from, to, timeframe);
        Log($"[IranianSync] Found {gaps.Count} gap(s)");

        if (gaps.Count == 0)
        {
            Log($"[IranianSync] No gaps found");
            return new List<Candle>();
        }

        var allDtoList = new List<CandleDto>();
        int gapIndex = 0;
        foreach (var gap in gaps)
        {
            gapIndex++;
            Log($"[IranianSync] Fetching gap {gapIndex}/{gaps.Count}: {gap.From:yyyy-MM-dd} to {gap.To:yyyy-MM-dd}");

            var dtoList = exchange == "Nobitex"
                ? await _nobitexService.FetchCandlesAsync(upperSymbol, timeframe, gap.From, gap.To)
                : await _wallexService.FetchCandlesAsync(upperSymbol, timeframe, gap.From, gap.To);

            Log($"[IranianSync] API returned {dtoList.Count} candles");
            allDtoList.AddRange(dtoList);

            progressCallback?.Invoke(gapIndex, gaps.Count, gap.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var candles = MapToCandles(allDtoList, 0, DataSource.Iranian, exchange, timeframe);
        Log($"[IranianSync] Total candles: {candles.Count}");

        return candles;
    }

    private List<DateRange> FindDataGaps(string symbol, string exchange, string tfFolder, DateTime from, DateTime to, Timeframe timeframe)
    {
        var gaps = new List<DateRange>();
        var step = GetTimeframeDuration(timeframe);

        try
        {
            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", exchange, tfFolder, $"{symbol}_{tfFolder}.csv");

            if (!File.Exists(csvPath))
            {
                gaps.Add(new DateRange { From = from, To = to });
                return gaps;
            }

            var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                gaps.Add(new DateRange { From = from, To = to });
                return gaps;
            }

            var existingTicks = new HashSet<long>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(',');
                if (parts.Length >= 1)
                {
                    var dateStr = parts[0].Trim();
                    DateTime date;

                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                        DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                        DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        existingTicks.Add(date.Ticks);
                    }
                }
            }

            DateTime? gapStart = null;
            var currentTime = from;

            while (currentTime <= to)
            {
                if (!existingTicks.Contains(currentTime.Ticks))
                {
                    if (gapStart == null) gapStart = currentTime;
                }
                else
                {
                    if (gapStart != null)
                    {
                        gaps.Add(new DateRange { From = gapStart.Value, To = currentTime - step });
                        gapStart = null;
                    }
                }
                currentTime = currentTime.Add(step);
            }

            if (gapStart != null)
            {
                gaps.Add(new DateRange { From = gapStart.Value, To = to });
            }
        }
        catch (Exception ex)
        {
            Log($"[IranianSync] Error checking CSV: {ex.Message}");
            gaps.Add(new DateRange { From = from, To = to });
        }

        return gaps;
    }

    private TimeSpan GetTimeframeDuration(Timeframe timeframe) => timeframe switch
    {
        Timeframe.M1 => TimeSpan.FromMinutes(1),
        Timeframe.M5 => TimeSpan.FromMinutes(5),
        Timeframe.M15 => TimeSpan.FromMinutes(15),
        Timeframe.M30 => TimeSpan.FromMinutes(30),
        Timeframe.H1 => TimeSpan.FromHours(1),
        Timeframe.H4 => TimeSpan.FromHours(4),
        Timeframe.H6 => TimeSpan.FromHours(6),
        Timeframe.H12 => TimeSpan.FromHours(12),
        Timeframe.D1 => TimeSpan.FromDays(1),
        Timeframe.W1 => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(1)
    };

    private List<Candle> MapToCandles(List<CandleDto> dtos, int cryptoId, DataSource source, string exchange, Timeframe timeframe)
    {
        return dtos.Select(d => new Candle
        {
            CryptocurrencyId = cryptoId,
            Source = source,
            Exchange = exchange,
            Timeframe = timeframe,
            OpenTime = d.OpenTime,
            Open = d.Open,
            High = d.High,
            Low = d.Low,
            Close = d.Close,
            Volume = d.Volume
        }).ToList();
    }

    private string GetTimeframeFolder(Timeframe timeframe) => timeframe switch
    {
        Timeframe.M1 => "1m",
        Timeframe.M5 => "5m",
        Timeframe.M15 => "15m",
        Timeframe.M30 => "30m",
        Timeframe.H1 => "1h",
        Timeframe.H4 => "4h",
        Timeframe.H6 => "6h",
        Timeframe.H12 => "12h",
        Timeframe.D1 => "1d",
        Timeframe.W1 => "1w",
        _ => "1h"
    };
}
