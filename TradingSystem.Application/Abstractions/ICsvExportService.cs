using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface ICsvExportService
{
    // ✅ اصلاح: اضافه کردن basePath به عنوان پارامتر
    Task ExportCandlesAsync(string basePath, string symbol, Timeframe timeframe, DataSource source, string exchange, List<Candle> candles);

    // ✅ متد جدید: Export به مسیر دلخواه
    Task ExportAllToPathAsync(string targetPath);

    // ✅ متد جدید: Export متادیتا
    Task ExportMetadataAsync(string targetPath);

    // ✅ متد جدید: Import از مسیر دلخواه
    Task<int> ImportAllFromCsvAsync(string sourcePath);

    // ✅ متد جدید: Import متادیتا
    Task ImportMetadataAsync(string sourcePath);
}