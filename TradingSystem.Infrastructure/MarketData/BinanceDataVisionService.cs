using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Infrastructure.MarketData;

public class BinanceDataVisionService : IBinanceDataVisionService
{
    private readonly HttpClient _http;
    private readonly string _logFile;
    private readonly SemaphoreSlim _semaphore;

    public BinanceDataVisionService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromMinutes(5);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "binance_download_log.txt");

        // ✅ محدود کردن به 5 درخواست همزمان (برای جلوگیری از rate limit)
        _semaphore = new SemaphoreSlim(5, 5);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<CandleDto>> DownloadKlinesAsync(string symbol, Timeframe timeframe, DateTime date)
    {
        var tfFolder = GetTimeframeFolder(timeframe);
        var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Log($"[Binance] 📥 Downloading {symbol} {tfFolder} for {dateStr}");

        try
        {
            var url = $"https://data.binance.vision/data/spot/daily/klines/{symbol}/{tfFolder}/{symbol}-{tfFolder}-{dateStr}.zip";
            Log($"[Binance] 🌐 URL: {url}");

            var response = await _http.GetAsync(url);
            Log($"[Binance] 📊 HTTP Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                Log($"[Binance] ❌ HTTP Error: {response.StatusCode}");
                return new List<CandleDto>();
            }

            var zipBytes = await response.Content.ReadAsByteArrayAsync();
            Log($"[Binance] ✅ Downloaded {zipBytes.Length} bytes");

            if (zipBytes.Length == 0)
            {
                Log($"[Binance] ⚠️ Empty response");
                return new List<CandleDto>();
            }

            using var zipStream = new MemoryStream(zipBytes);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var csvEntry = zipArchive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv"));
            if (csvEntry == null)
            {
                Log($"[Binance] ❌ No CSV file found in ZIP");
                return new List<CandleDto>();
            }

            Log($"[Binance] 📄 CSV Entry: {csvEntry.Name} (Size: {csvEntry.Length} bytes)");

            using var csvStream = csvEntry.Open();
            using var reader = new StreamReader(csvStream, Encoding.UTF8);

            var candles = new List<CandleDto>();
            var lineCount = 0;

            // Skip header
            await reader.ReadLineAsync();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                lineCount++;

                try
                {
                    var parts = line.Split(',');
                    if (parts.Length < 7) continue;

                    var candle = new CandleDto
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[0], CultureInfo.InvariantCulture)).UtcDateTime,
                        Open = decimal.Parse(parts[1], CultureInfo.InvariantCulture),
                        High = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                        Low = decimal.Parse(parts[3], CultureInfo.InvariantCulture),
                        Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture),
                        Volume = decimal.Parse(parts[5], CultureInfo.InvariantCulture)
                    };

                    candles.Add(candle);
                }
                catch (Exception ex)
                {
                    Log($"[Binance] ⚠️ Error parsing line {lineCount}: {ex.Message}");
                }
            }

            Log($"[Binance] 📊 Parsed {candles.Count} candles from {lineCount} lines");
            return candles;
        }
        catch (Exception ex)
        {
            Log($"[Binance] ❌ Exception: {ex.Message}");
            return new List<CandleDto>();
        }
    }

    // ✅ متد جدید: دانلود موازی چندین روز
    public async Task<List<CandleDto>> DownloadKlinesParallelAsync(
        string symbol,
        Timeframe timeframe,
        IEnumerable<DateTime> dates,
        IProgress<(int completed, int total, string date)>? progress = null)
    {
        var tfFolder = GetTimeframeFolder(timeframe);
        var dateList = dates.ToList();
        var totalDays = dateList.Count;
        var completedDays = 0;
        var allCandles = new List<CandleDto>();
        var lockObj = new object();

        Log($"[Binance]  Starting parallel download for {symbol} {tfFolder} ({totalDays} days)");

        // ✅ استفاده از Parallel.ForEachAsync با محدودیت concurrency
        await Parallel.ForEachAsync(
            dateList,
            new ParallelOptions { MaxDegreeOfParallelism = 5 }, // حداکثر 5 درخواست همزمان
            async (date, cancellationToken) =>
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    Log($"[Binance] 📥 [{Interlocked.Increment(ref completedDays)}/{totalDays}] Downloading {dateStr}");

                    var candles = await DownloadKlinesAsync(symbol, timeframe, date);

                    lock (lockObj)
                    {
                        allCandles.AddRange(candles);
                    }

                    progress?.Report((completedDays, totalDays, dateStr));
                }
                finally
                {
                    _semaphore.Release();
                }
            });

        Log($"[Binance] ✅ Parallel download completed. Total candles: {allCandles.Count}");
        return allCandles.OrderBy(c => c.OpenTime).ToList();
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