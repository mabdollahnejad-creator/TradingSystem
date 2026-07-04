using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface IBinanceDataVisionService
{
    // ✅ متد قبلی (تکی)
    Task<List<CandleDto>> DownloadKlinesAsync(string symbol, Timeframe timeframe, DateTime date);

    // ✅ متد جدید (موازی)
    Task<List<CandleDto>> DownloadKlinesParallelAsync(
        string symbol,
        Timeframe timeframe,
        IEnumerable<DateTime> dates,
        IProgress<(int completed, int total, string date)>? progress = null);
}