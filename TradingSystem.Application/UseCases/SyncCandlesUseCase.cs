using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Application.Services;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.UseCases;

public class SyncCandlesUseCase
{
    private readonly IBinanceDataVisionService _binanceService;
    private readonly INobitexExchangeService _nobitexService;
    private readonly IWallexExchangeService _wallexService;
    private readonly ICsvExportService _csvExporter;
    private readonly IServiceProvider _serviceProvider;
    private readonly IBinanceSymbolMapper _symbolMapper;
    private readonly IBinanceFileListService _fileListService;
    private readonly string _logFile;

    public SyncCandlesUseCase(
        IBinanceDataVisionService binanceService,
        INobitexExchangeService nobitexService,
        IWallexExchangeService wallexService,
        ICsvExportService csvExporter,
        IServiceProvider serviceProvider,
        IBinanceSymbolMapper symbolMapper,
        IBinanceFileListService fileListService)
    {
        _binanceService = binanceService;
        _nobitexService = nobitexService;
        _wallexService = wallexService;
        _csvExporter = csvExporter;
        _serviceProvider = serviceProvider;
        _symbolMapper = symbolMapper;
        _fileListService = fileListService;
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_candles_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    // ✅ متد تست اصلاح شده - دانلود کل بازه زمانی
    public async Task<bool> TestBinanceBnbAsync()
    {
        Log("═══════════════════════════════════════════════════════");
        Log("[TEST] ═══ TEST BINANCE BNB - FULL RANGE DOWNLOAD ═══");
        Log("═══════════════════════════════════════════════════════");

        try
        {
            // مرحله 1: بررسی Mapper
            Log("[TEST] Step 1: Testing BinanceSymbolMapper");
            var mappedSymbol = _symbolMapper.GetBinanceSymbol("BNB");
            Log($"[TEST] Mapper result: BNB -> {mappedSymbol}");

            if (mappedSymbol != "BNBUSDT")
            {
                Log($"[TEST] ❌ ERROR: Expected BNBUSDT but got {mappedSymbol}");
                return false;
            }

            // مرحله 2: دریافت لیست تاریخ‌ها
            Log("[TEST] Step 2: Getting available dates");
            var tfFolder = "1h";
            var availableDates = await _fileListService.GetAvailableDatesAsync(mappedSymbol, tfFolder);
            Log($"[TEST] Available dates count: {availableDates.Count}");

            if (availableDates.Count == 0)
            {
                Log("[TEST] ❌ ERROR: No dates found");
                return false;
            }

            var first5 = availableDates.Take(5).Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            var last5 = availableDates.TakeLast(5).Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Log($"[TEST] First 5 dates: {string.Join(", ", first5)}");
            Log($"[TEST] Last 5 dates: {string.Join(", ", last5)}");

            // مرحله 3: تعریف بازه زمانی تست (2020-01-01 تا 2022-12-31)
            Log("[TEST] Step 3: Defining test date range");
            var from = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2022, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            Log($"[TEST] From: {from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
            Log($"[TEST] To: {to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");

            // مرحله 4: فیلتر کردن تاریخ‌های موجود در بازه
            Log("[TEST] Step 4: Filtering dates in range");
            var availableDateStrings = availableDates
                .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToHashSet();

            var datesToProcess = new List<DateTime>();
            var currentDate = from.Date;
            var endDate = to.Date.AddDays(1);

            while (currentDate < endDate)
            {
                var dateStr = currentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (availableDateStrings.Contains(dateStr))
                {
                    datesToProcess.Add(currentDate);
                }
                currentDate = currentDate.AddDays(1);
            }

            Log($"[TEST] Dates to process: {datesToProcess.Count}");

            if (datesToProcess.Count == 0)
            {
                Log("[TEST] ❌ ERROR: No dates found in range");
                return false;
            }

            Log($"[TEST] First date to download: {datesToProcess.First().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            Log($"[TEST] Last date to download: {datesToProcess.Last().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

            // مرحله 5: دانلود و ذخیره فایل‌ها
            Log("[TEST] Step 5: Downloading and saving files");
            var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "binance", mappedSymbol, tfFolder);
            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
                Log($"[TEST] Created cache directory: {cachePath}");
            }

            int downloadedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            foreach (var date in datesToProcess)
            {
                var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var cacheFile = Path.Combine(cachePath, $"{mappedSymbol}-{tfFolder}-{dateStr}.zip");

                if (File.Exists(cacheFile))
                {
                    Log($"[TEST] ⏭️ Skip (exists): {dateStr}");
                    skippedCount++;
                    continue;
                }

                try
                {
                    var url = $"https://data.binance.vision/data/spot/daily/klines/{mappedSymbol}/{tfFolder}/{mappedSymbol}-{tfFolder}-{dateStr}.zip";
                    Log($"[TEST] ⬇️ Downloading: {dateStr}");

                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(2);

                    var response = await httpClient.GetAsync(url);
                    Log($"[TEST] HTTP Status: {(int)response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        var zipBytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(cacheFile, zipBytes);
                        Log($"[TEST] ✅ Saved: {dateStr} ({zipBytes.Length} bytes)");
                        downloadedCount++;

                        // Parse و نمایش نمونه
                        var candles = await ParseBinanceZipAsync(zipBytes, dateStr);
                        if (candles.Count > 0)
                        {
                            Log($"[TEST] Parsed {candles.Count} candles");
                        }
                    }
                    else
                    {
                        Log($"[TEST] ❌ HTTP Error: {(int)response.StatusCode} for {dateStr}");
                        errorCount++;
                    }

                    // Delay برای جلوگیری از rate limiting
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Log($"[TEST] ❌ Error: {ex.Message} for {dateStr}");
                    errorCount++;
                }
            }

            // مرحله 6: خلاصه نتایج
            Log("[TEST] ═══ SUMMARY ═══");
            Log($"[TEST] Total dates in range: {datesToProcess.Count}");
            Log($"[TEST] Downloaded: {downloadedCount}");
            Log($"[TEST] Skipped (already exists): {skippedCount}");
            Log($"[TEST] Errors: {errorCount}");

            if (downloadedCount > 0 || skippedCount > 0)
            {
                Log("[TEST] ✅ TEST PASSED");
                Log("═══════════════════════════════════════════════════════");
                return true;
            }
            else
            {
                Log("[TEST] ❌ TEST FAILED - No files downloaded");
                Log("═══════════════════════════════════════════════════════");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log($"[TEST] ❌ CRITICAL ERROR: {ex.Message}");
            Log($"[TEST] StackTrace: {ex.StackTrace}");
            Log("═══════════════════════════════════════════════════════");
            return false;
        }
    }

    public async Task<(bool exists, List<string> availableExchanges, bool hasMetadata)> CheckSymbolAvailabilityAsync(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant().Trim();
        var availableExchanges = new List<string>();

        try
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(upperSymbol, @"^[A-Z0-9]{1,20}$"))
                return (false, availableExchanges, false);

            using var scope = _serviceProvider.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<ICoinGeckoMetadataService>();
            var metadata = await metadataService.SearchBySymbolAsync(upperSymbol);

            if (metadata != null)
            {
                availableExchanges.AddRange(new[] { "Binance", "Nobitex", "Wallex" });
                return (true, availableExchanges, true);
            }
            return (false, availableExchanges, false);
        }
        catch { return (false, availableExchanges, false); }
    }

    public async Task<int> ExecuteAsync(
        string symbol, DataSource source, string exchange, Timeframe timeframe,
        DateTime from, DateTime to, CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        var tfFolder = GetTimeframeFolder(timeframe);

        Log($"═══════════════════════════════════════════════════════");
        Log($"[Sync] START: {upperSymbol} | {exchange} | {timeframe}");
        Log($"[Sync] Input dates: from={from.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (Year={from.Year}) to={to.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (Year={to.Year})");

        // ✅ تبدیل تاریخ‌ها به میلادی
        from = ConvertToGregorian(from);
        to = ConvertToGregorian(to);

        Log($"[Sync] Converted dates: from={from.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} to={to.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");

        using var scope = _serviceProvider.CreateScope();
        var candleRepo = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
        var cryptoRepo = scope.ServiceProvider.GetRequiredService<ICryptoRepository>();

        var crypto = await cryptoRepo.GetBySymbolAsync(upperSymbol);
        if (crypto == null)
        {
            crypto = await cryptoRepo.AddAsync(new Cryptocurrency
            {
                Symbol = upperSymbol,
                Name = upperSymbol,
                IsActive = true
            });
            await cryptoRepo.SaveChangesAsync();
        }

        List<Candle> candles = new();

        if (source == DataSource.Global && exchange == "Binance")
        {
            if (upperSymbol == "USDT")
            {
                Log($"[Sync] [Binance] Skipping USDT");
                return 0;
            }

            var binanceSymbol = _symbolMapper.GetBinanceSymbol(upperSymbol);
            Log($"[Sync] [Binance] Mapper: {upperSymbol} -> {binanceSymbol}");

            var exists = await _fileListService.SymbolExistsAsync(binanceSymbol, tfFolder);
            if (!exists)
            {
                Log($"[Sync] [Binance] Symbol {binanceSymbol} not found");
                return 0;
            }

            var firstAvailable = await _fileListService.GetFirstAvailableDateAsync(binanceSymbol, tfFolder);
            if (firstAvailable.HasValue && firstAvailable.Value > from)
            {
                Log($"[Sync] [Binance] Adjusting from {from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to {firstAvailable.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
                from = firstAvailable.Value;
            }

            if (from > to)
            {
                Log($"[Sync] [Binance] ERROR: from ({from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}) > to ({to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})");
                return 0;
            }

            candles = await DownloadFromBinanceAsync(binanceSymbol, upperSymbol, crypto.Id, timeframe, from, to, cancellationToken, progressCallback);
        }
        else if (source == DataSource.Iranian && exchange == "Nobitex")
        {
            Log($"[Sync] [Nobitex] Processing {upperSymbol} {timeframe}");
            var gaps = FindDataGaps(upperSymbol, exchange, tfFolder, from, to, timeframe);

            if (gaps.Count == 0)
            {
                Log($"[Sync] [Nobitex] No gaps found");
                return 0;
            }

            var allDtoList = new List<CandleDto>();
            foreach (var gap in gaps)
            {
                var dtoList = await _nobitexService.FetchCandlesAsync(upperSymbol, timeframe, gap.From, gap.To);
                allDtoList.AddRange(dtoList);
            }

            candles = MapToCandles(allDtoList, crypto.Id, source, exchange, timeframe);
        }
        else if (source == DataSource.Iranian && exchange == "Wallex")
        {
            if (upperSymbol == "USDT")
            {
                Log($"[Sync] [Wallex] Skipping USDT");
                return 0;
            }

            Log($"[Sync] [Wallex] Processing {upperSymbol} {timeframe}");
            var gaps = FindDataGaps(upperSymbol, exchange, tfFolder, from, to, timeframe);

            if (gaps.Count == 0)
            {
                Log($"[Sync] [Wallex] No gaps found");
                return 0;
            }

            var allDtoList = new List<CandleDto>();
            foreach (var gap in gaps)
            {
                var dtoList = await _wallexService.FetchCandlesAsync(upperSymbol, timeframe, gap.From, gap.To);
                allDtoList.AddRange(dtoList);
            }

            candles = MapToCandles(allDtoList, crypto.Id, source, exchange, timeframe);
        }

        if (!candles.Any()) return 0;

        var existingTimes = await candleRepo.GetExistingOpenTimesAsync(crypto.Id, source, exchange, timeframe, from, to);
        var existingTimeSet = new HashSet<DateTime>(existingTimes);
        var newCandles = candles.Where(c => !existingTimeSet.Contains(c.OpenTime)).ToList();

        if (!newCandles.Any()) return 0;

        await candleRepo.AddRangeAsync(newCandles);
        await candleRepo.SaveChangesAsync();
        await cryptoRepo.UpdateExchangeAvailabilityAsync(upperSymbol, exchange, true);

        await _csvExporter.ExportCandlesAsync("Data", upperSymbol, timeframe, source, exchange, newCandles);

        return newCandles.Count;
    }

    private DateTime ConvertToGregorian(DateTime date)
    {
        try
        {
            // ✅ اگر سال بین 1300 تا 1500 باشد، احتمالاً شمسی است
            if (date.Year >= 1300 && date.Year <= 1500)
            {
                var persianCalendar = new PersianCalendar();
                var gregorianDate = persianCalendar.ToDateTime(
                    date.Year, 
                    date.Month, 
                    date.Day, 
                    date.Hour, 
                    date.Minute, 
                    date.Second, 
                    date.Millisecond);
                
                Log($"[Sync] Converted Persian {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to Gregorian {gregorianDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
                return gregorianDate;
            }
            
            return date;
        }
        catch (Exception ex)
        {
            Log($"[Sync] Error converting date: {ex.Message}");
            return date;
        }
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
            Log($"[Sync] Error checking CSV: {ex.Message}");
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

    private async Task<List<Candle>> DownloadFromBinanceAsync(
        string binanceSymbol, string originalSymbol, int cryptoId, Timeframe timeframe,
        DateTime from, DateTime to, CancellationToken cancellationToken, Action<int, int, string>? progressCallback = null)
    {
        Log($"[Sync] [Binance] DownloadFromBinanceAsync START");

        var allCandles = new List<Candle>();
        var lockObj = new object();

        var currentDate = from.Date;
        var endDate = to.Date.AddDays(1);
        var totalDays = (int)(endDate - currentDate).TotalDays;
        var currentDay = 0;

        var tfFolder = GetTimeframeFolder(timeframe);

        var availableDates = await _fileListService.GetAvailableDatesAsync(binanceSymbol, tfFolder);
        var availableDateStrings = availableDates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToHashSet();

        var possibleCachePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "binance", binanceSymbol, tfFolder),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "binance", originalSymbol, tfFolder)
        };

        string cachePath = possibleCachePaths.FirstOrDefault(Directory.Exists) ?? possibleCachePaths[0];
        if (!Directory.Exists(cachePath)) Directory.CreateDirectory(cachePath);

        var datesToProcess = new List<DateTime>();
        var temp = currentDate;
        while (temp < endDate)
        {
            var dateStr = temp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (availableDateStrings.Contains(dateStr))
                datesToProcess.Add(temp);
            temp = temp.AddDays(1);
        }

        Log($"[Sync] [Binance] Dates to process: {datesToProcess.Count}");

        if (datesToProcess.Count == 0) return allCandles;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(datesToProcess, options, async (date, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var cacheFile = Path.Combine(cachePath, $"{binanceSymbol}-{tfFolder}-{dateStr}.zip");
            List<CandleDto> dtoList = new();

            if (File.Exists(cacheFile))
            {
                var zipBytes = await File.ReadAllBytesAsync(cacheFile, ct);
                dtoList = await ParseBinanceZipAsync(zipBytes, dateStr);
            }
            else
            {
                try
                {
                    var url = $"https://data.binance.vision/data/spot/daily/klines/{binanceSymbol}/{tfFolder}/{binanceSymbol}-{tfFolder}-{dateStr}.zip";
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(5);

                    var response = await httpClient.GetAsync(url, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var zipBytes = await response.Content.ReadAsByteArrayAsync(ct);
                        await File.WriteAllBytesAsync(cacheFile, zipBytes, ct);
                        dtoList = await ParseBinanceZipAsync(zipBytes, dateStr);
                    }
                }
                catch { }
            }

            if (dtoList.Any())
            {
                var candles = MapToCandles(dtoList, cryptoId, DataSource.Global, "Binance", timeframe);
                lock (lockObj) { allCandles.AddRange(candles); }
            }

            var completedDay = Interlocked.Increment(ref currentDay);
            progressCallback?.Invoke(completedDay, datesToProcess.Count, dateStr);
        });

        return allCandles.Where(c => c.OpenTime >= from && c.OpenTime < to).ToList();
    }

    private async Task<List<CandleDto>> ParseBinanceZipAsync(byte[] zipBytes, string dateStr)
    {
        var candles = new List<CandleDto>();
        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var csvEntry = zipArchive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv"));
            if (csvEntry == null) return candles;

            using var csvStream = csvEntry.Open();
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            await reader.ReadLineAsync();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var parts = line.Split(',');
                    if (parts.Length < 7) continue;
                    candles.Add(new CandleDto
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[0], CultureInfo.InvariantCulture)).UtcDateTime,
                        Open = decimal.Parse(parts[1], CultureInfo.InvariantCulture),
                        High = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                        Low = decimal.Parse(parts[3], CultureInfo.InvariantCulture),
                        Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture),
                        Volume = decimal.Parse(parts[5], CultureInfo.InvariantCulture)
                    });
                }
                catch { }
            }
        }
        catch { }
        return candles;
    }

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
        Timeframe.M1 => "1m", Timeframe.M5 => "5m", Timeframe.M15 => "15m", Timeframe.M30 => "30m",
        Timeframe.H1 => "1h", Timeframe.H4 => "4h", Timeframe.H6 => "6h", Timeframe.H12 => "12h",
        Timeframe.D1 => "1d", Timeframe.W1 => "1w", _ => "1h"
    };
}

public class DateRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}