using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface IBinanceSyncService
{
    Task<List<Candle>> SyncAllTimeframesAsync(
        string symbol,
        DateTime from,
        DateTime to,
        List<Timeframe> timeframes,
        CancellationToken cancellationToken = default,
        Action<int, int, string>? progressCallback = null);
}