using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Application.Abstractions;

public interface INobitexExchangeService
{
    Task<List<CandleDto>> FetchCandlesAsync(string symbol, Timeframe timeframe, DateTime from, DateTime to);
}