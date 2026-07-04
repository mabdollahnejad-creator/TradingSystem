using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface IMarketDataProvider
{
    Task<List<CandleDto>> FetchCandlesAsync(string symbol, int days);
}