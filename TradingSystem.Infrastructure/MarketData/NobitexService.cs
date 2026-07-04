using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Infrastructure.MarketData;

public class NobitexService : INobitexExchangeService
{
    private readonly HttpClient _http;
    private readonly string _logFile;

    public NobitexService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nobitex_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<CandleDto>> FetchCandlesAsync(string symbol, Timeframe timeframe, DateTime from, DateTime to)
    {
        Log($"[Nobitex] Fetching {symbol} | {timeframe} | {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

        var resolution = timeframe switch
        {
            Timeframe.M1 => "1",
            Timeframe.M5 => "5",
            Timeframe.M15 => "15",
            Timeframe.M30 => "30",
            Timeframe.H1 => "60",
            Timeframe.H4 => "240",
            Timeframe.D1 => "D",
            _ => "60"
        };

        // ✅ اصلاح: بدون خط تیره
        string nobitexSymbol;
        if (symbol.Equals("USDT", StringComparison.OrdinalIgnoreCase))
        {
            nobitexSymbol = "USDTIRT";
        }
        else
        {
            nobitexSymbol = $"{symbol.ToUpperInvariant()}USDT";  // ✅ HYPEUSDT نه HYPE-USDT
        }

        Log($"[Nobitex] Symbol: {nobitexSymbol}");

        var allCandles = new List<CandleDto>();
        var currentTo = to;
        const int maxCandlesPerRequest = 500;
        var timeframeDuration = GetTimeframeDuration(timeframe);

        var needsTimeAdjustment = timeframe == Timeframe.H1 || timeframe == Timeframe.H4;
        var adjustmentSeconds = needsTimeAdjustment ? -1800 : 0;

        while (currentTo > from)
        {
            var currentFrom = currentTo.AddSeconds(-maxCandlesPerRequest * timeframeDuration.TotalSeconds);
            if (currentFrom < from) currentFrom = from;

            var fromTimestamp = new DateTimeOffset(currentFrom).ToUnixTimeSeconds();
            var toTimestamp = new DateTimeOffset(currentTo).ToUnixTimeSeconds();

            var url = $"https://apiv2.nobitex.ir/market/udf/history?symbol={nobitexSymbol}&resolution={resolution}&from={fromTimestamp}&to={toTimestamp}";

            try
            {
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Log($"[Nobitex] HTTP Error: {response.StatusCode}");
                    break;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<NobitexHistoryResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null || result.s == "no_data" || result.t == null || result.t.Length == 0)
                {
                    Log($"[Nobitex] No data for {currentFrom:yyyy-MM-dd} to {currentTo:yyyy-MM-dd}");
                    currentTo = currentFrom.AddSeconds(-1);
                    continue;
                }

                if (result.s == "error")
                {
                    Log($"[Nobitex] API Error: {result.errmsg}");
                    break;
                }

                var batchCandles = new List<CandleDto>();
                for (int i = 0; i < result.t.Length; i++)
                {
                    if (i >= result.o.Length || i >= result.h.Length ||
                        i >= result.l.Length || i >= result.c.Length)
                        break;

                    var adjustedTimestamp = result.t[i] + adjustmentSeconds;
                    var openTime = DateTimeOffset.FromUnixTimeSeconds(adjustedTimestamp).UtcDateTime;

                    var candle = new CandleDto
                    {
                        OpenTime = openTime,
                        Open = result.o[i],
                        High = result.h[i],
                        Low = result.l[i],
                        Close = result.c[i],
                        Volume = result.v != null && i < result.v.Length ? result.v[i] : 0
                    };

                    if (candle.OpenTime >= from && candle.OpenTime <= to)
                    {
                        batchCandles.Add(candle);
                    }
                }

                allCandles.AddRange(batchCandles);
                Log($"[Nobitex] Batch: {batchCandles.Count} candles");

                if (result.t.Length < maxCandlesPerRequest)
                    break;

                currentTo = currentFrom.AddSeconds(-1);
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Log($"[Nobitex] Exception: {ex.Message}");
                break;
            }
        }

        Log($"[Nobitex] Total: {allCandles.Count} candles");
        return allCandles.OrderBy(c => c.OpenTime).ToList();
    }

    private TimeSpan GetTimeframeDuration(Timeframe timeframe) => timeframe switch
    {
        Timeframe.M1 => TimeSpan.FromMinutes(1),
        Timeframe.M5 => TimeSpan.FromMinutes(5),
        Timeframe.M15 => TimeSpan.FromMinutes(15),
        Timeframe.M30 => TimeSpan.FromMinutes(30),
        Timeframe.H1 => TimeSpan.FromHours(1),
        Timeframe.H4 => TimeSpan.FromHours(4),
        Timeframe.D1 => TimeSpan.FromDays(1),
        _ => TimeSpan.FromHours(1)
    };

    private class NobitexHistoryResponse
    {
        public string s { get; set; } = string.Empty;
        public string? errmsg { get; set; }
        public long[] t { get; set; } = Array.Empty<long>();
        public decimal[] o { get; set; } = Array.Empty<decimal>();
        public decimal[] h { get; set; } = Array.Empty<decimal>();
        public decimal[] l { get; set; } = Array.Empty<decimal>();
        public decimal[] c { get; set; } = Array.Empty<decimal>();
        public decimal[]? v { get; set; }
    }
}