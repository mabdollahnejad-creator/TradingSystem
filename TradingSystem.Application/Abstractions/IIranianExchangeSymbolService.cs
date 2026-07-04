namespace TradingSystem.Application.Abstractions;

public interface IIranianExchangeSymbolService
{
    Task<HashSet<string>> GetAvailableSymbolsAsync();
}