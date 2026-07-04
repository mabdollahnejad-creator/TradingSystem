using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingSystem.Application.Abstractions;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Infrastructure.Services;

public class CsvExportService : ICsvExportService
{
    private readonly string _logFile;

    public CsvExportService()
    {
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "csv_export_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    // ✅ اصلاح: استفاده از List<Candle> به جای IEnumerable<Candle>
    public async Task ExportCandlesAsync(string basePath, string symbol, Timeframe timeframe, DataSource source, string exchange, List<Candle> candles)
    {
        if (candles == null || !candles.Any())
        {
            Log($"[CSV] No candles to export for {symbol} | {exchange} | {timeframe}");
            return;
        }

        var tfFolder = GetTimeframeFolder(timeframe);
        var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, basePath, exchange, tfFolder);
        var filePath = Path.Combine(folderPath, $"{symbol}_{tfFolder}.csv");

        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Log($"[CSV] Created folder: {folderPath}");
            }

            var candleList = candles.OrderBy(c => c.OpenTime).ToList();
            var newLines = new List<string>();

            foreach (var candle in candleList)
            {
                var line = $"{candle.OpenTime:yyyy-MM-dd HH:mm:ss},{candle.Open.ToString(CultureInfo.InvariantCulture)},{candle.High.ToString(CultureInfo.InvariantCulture)},{candle.Low.ToString(CultureInfo.InvariantCulture)},{candle.Close.ToString(CultureInfo.InvariantCulture)},{candle.Volume.ToString(CultureInfo.InvariantCulture)}";
                newLines.Add(line);
            }

            // ✅ اصلاح حیاتی: بررسی وجود فایل و append کردن
            if (File.Exists(filePath))
            {
                Log($"[CSV] File exists: {filePath} - appending {newLines.Count} new records");

                // ✅ خواندن تاریخ‌های موجود برای جلوگیری از duplicate
                var existingLines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
                var existingDates = new HashSet<string>();

                foreach (var line in existingLines.Skip(1)) // skip header
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 1)
                    {
                        existingDates.Add(parts[0].Trim());
                    }
                }

                // ✅ فقط رکوردهای جدید را اضافه کن
                var uniqueNewLines = newLines.Where(line =>
                {
                    var datePart = line.Split(',')[0];
                    return !existingDates.Contains(datePart);
                }).ToList();

                if (uniqueNewLines.Any())
                {
                    await File.AppendAllLinesAsync(filePath, uniqueNewLines, Encoding.UTF8);
                    Log($"[CSV] ✅ Appended {uniqueNewLines.Count} new records to {filePath}");
                }
                else
                {
                    Log($"[CSV] No new records to append (all {newLines.Count} records already exist)");
                }
            }
            else
            {
                Log($"[CSV] File does not exist: {filePath} - creating new file with {newLines.Count} records");

                var allLines = new List<string>
                {
                    "DateTime,Open,High,Low,Close,Volume" // header
                };
                allLines.AddRange(newLines);

                await File.WriteAllLinesAsync(filePath, allLines, Encoding.UTF8);
                Log($"[CSV] ✅ Created new file: {filePath} with {newLines.Count} records");
            }
        }
        catch (Exception ex)
        {
            Log($"[CSV] ❌ Error exporting {symbol} | {exchange} | {timeframe}: {ex.Message}");
            Log($"[CSV] StackTrace: {ex.StackTrace}");
        }
    }

    public async Task ExportAllToPathAsync(string targetPath)
    {
        Log($"[CSV] ExportAllToPathAsync: {targetPath}");
        await Task.CompletedTask;
    }

    public async Task ExportMetadataAsync(string targetPath)
    {
        Log($"[CSV] ExportMetadataAsync: {targetPath}");
        await Task.CompletedTask;
    }

    public async Task ImportMetadataAsync(string sourcePath)
    {
        Log($"[CSV] ImportMetadataAsync: {sourcePath}");
        await Task.CompletedTask;
    }

    public async Task<int> ImportAllFromCsvAsync(string sourcePath)
    {
        Log($"[CSV] ImportAllFromCsvAsync: {sourcePath}");
        return await Task.FromResult(0);
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