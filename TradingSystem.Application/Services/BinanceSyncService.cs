using System.Globalization;
using System.IO.Compression;
using System.Text;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Services;

public class BinanceSyncService : IBinanceSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IBinanceDataVisionService _binanceService;
    private readonly IBinanceSymbolMapper _symbolMapper;
    private readonly IBinanceFileListService _fileListService;
    private readonly IDownloadIntegrityChecker _integrityChecker;
    private readonly ITradingLogger _logger;

    private static readonly HashSet<string> Stablecoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "USDC", "BUSD", "DAI", "TUSD", "USDP", "FDUSD"
    };

    public BinanceSyncService(
        HttpClient httpClient,
        IBinanceDataVisionService binanceService,
        IBinanceSymbolMapper symbolMapper,
        IBinanceFileListService fileListService,
        IDownloadIntegrityChecker integrityChecker,
        ITradingLogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _binanceService = binanceService;
        _symbolMapper = symbolMapper;
        _fileListService = fileListService;
        _integrityChecker = integrityChecker;
        _logger = logger;
    }

    public async Task<List<Candle>> SyncAllTimeframesAsync(
        string symbol,
        DateTime from,
        DateTime to,
        List<Timeframe> timeframes,
        CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null)
    {
        var upperSymbol = symbol.ToUpperInvariant();

        if (Stablecoins.Contains(upperSymbol) || upperSymbol == "USDT")
        {
            _logger.LogWarning("Skipping stablecoin: {Symbol}", upperSymbol);
            return new List<Candle>();
        }

        var allCandles = new List<Candle>();
        var totalTasks = timeframes.Count;
        var completedTasks = 0;

        _logger.LogInformation("═══ Starting {Symbol} with {Count} timeframes ═══", upperSymbol, totalTasks);

        foreach (var tf in timeframes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var candles = await DownloadAsync(
                    upperSymbol, tf, from, to, cancellationToken,
                    (current, total, status) =>
                    {
                        progressCallback?.Invoke(current, total, $"{upperSymbol} {tf} - {status}");
                    });

                allCandles.AddRange(candles);
                _logger.LogInformation("✅ Completed {Timeframe}. Candles: {Count}", tf, candles.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("❌ Cancelled at {Timeframe}", tf);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Error in {Timeframe}", tf);
            }

            completedTasks++;
        }

        _logger.LogInformation("═══ All timeframes completed. Total candles: {Count} ═══", allCandles.Count);
        return allCandles;
    }

    private async Task<List<Candle>> DownloadAsync(
        string symbol,
        Timeframe timeframe,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null)
    {
        var tfFolder = GetTimeframeFolder(timeframe);
        _logger.LogInformation("Download: {Symbol} | {Timeframe} | {From} to {To}", symbol, timeframe, from, to);

        var binanceSymbol = _symbolMapper.GetBinanceSymbol(symbol);
        _logger.LogInformation("Mapped: {Symbol} -> {BinanceSymbol}", symbol, binanceSymbol);

        var exists = await _fileListService.SymbolExistsAsync(binanceSymbol, tfFolder);
        if (!exists)
        {
            _logger.LogWarning("Symbol {Symbol} not found for {Timeframe}", binanceSymbol, tfFolder);
            return new List<Candle>();
        }

        var firstAvailable = await _fileListService.GetFirstAvailableDateAsync(binanceSymbol, tfFolder);
        if (firstAvailable.HasValue && firstAvailable.Value > from)
        {
            _logger.LogInformation("Adjusting from {From} to {FirstAvailable}", from, firstAvailable.Value);
            from = firstAvailable.Value;
        }

        if (from > to)
        {
            _logger.LogError("ERROR: from ({From}) > to ({To})", from, to);
            return new List<Candle>();
        }

        return await DownloadFromBinanceAsync(binanceSymbol, symbol, timeframe, from, to, cancellationToken, progressCallback);
    }

    private async Task<List<Candle>> DownloadFromBinanceAsync(
        string binanceSymbol, string originalSymbol, Timeframe timeframe,
        DateTime from, DateTime to, CancellationToken cancellationToken,
        Action<int, int, string>? progressCallback = null)
    {
        var allCandles = new List<Candle>();
        var lockObj = new object();
        var currentDate = from.Date;
        var endDate = to.Date.AddDays(1);
        var currentDay = 0;
        var tfFolder = GetTimeframeFolder(timeframe);

        var availableDates = await _fileListService.GetAvailableDatesAsync(binanceSymbol, tfFolder);
        var availableDateStrings = availableDates
            .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToHashSet();

        var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "binance", binanceSymbol, tfFolder);
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

        _logger.LogInformation("Dates to process: {Count}", datesToProcess.Count);

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

            // بررسی integrity فایل موجود
            if (File.Exists(cacheFile))
            {
                var integrity = await _integrityChecker.CheckFileIntegrityAsync(cacheFile);
                if (integrity.IsValid)
                {
                    _logger.LogDebug("✅ Cache hit: {Date}", dateStr);
                    var zipBytes = await File.ReadAllBytesAsync(cacheFile, ct);
                    dtoList = await ParseBinanceZipAsync(zipBytes, dateStr);
                }
                else
                {
                    _logger.LogWarning("⚠️ Invalid cache file, deleting: {Date} - {Error}", dateStr, integrity.ErrorMessage);
                    File.Delete(cacheFile);
                }
            }

            // دانلود اگر فایل معتبر نیست
            if (!dtoList.Any())
            {
                _logger.LogDebug("⬇️ Downloading: {Date}", dateStr);
                try
                {
                    var url = $"https://data.binance.vision/data/spot/daily/klines/{binanceSymbol}/{tfFolder}/{binanceSymbol}-{tfFolder}-{dateStr}.zip";
                    var response = await _httpClient.GetAsync(url, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var zipBytes = await response.Content.ReadAsByteArrayAsync(ct);
                        await File.WriteAllBytesAsync(cacheFile, zipBytes, ct);
                        _logger.LogDebug("💾 Saved: {Date}", dateStr);
                        dtoList = await ParseBinanceZipAsync(zipBytes, dateStr);
                    }
                    else
                    {
                        _logger.LogError("❌ HTTP {StatusCode} for {Date}", (int)response.StatusCode, dateStr);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error downloading {Date}", dateStr);
                }
            }

            if (dtoList.Any())
            {
                var candles = MapToCandles(dtoList, 0, DataSource.Global, "Binance", timeframe);
                lock (lockObj) { allCandles.AddRange(candles); }
            }

            var completedDay = Interlocked.Increment(ref currentDay);
            progressCallback?.Invoke(completedDay, datesToProcess.Count, dateStr);
        });

        var result = allCandles.Where(c => c.OpenTime >= from && c.OpenTime < to).ToList();
        _logger.LogInformation("Total candles for {Symbol}/{Timeframe}: {Count}", binanceSymbol, tfFolder, result.Count);
        return result;
    }

    private async Task<List<CandleDto>> ParseBinanceZipAsync(byte[] zipBytes, string dateStr)
    {
        var candles = new List<CandleDto>();
        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var csvEntry = zipArchive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv"));

            if (csvEntry == null)
            {
                _logger.LogWarning("⚠️ No CSV entry found in ZIP for {Date}", dateStr);
                return candles;
            }

            using var csvStream = csvEntry.Open();
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            await reader.ReadLineAsync(); // Skip header

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
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Error parsing line: {Error}", ex.Message);
                }
            }

            _logger.LogDebug("✅ Parsed {Count} candles from {Date}", candles.Count, dateStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in ParseBinanceZipAsync for {Date}", dateStr);
        }
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