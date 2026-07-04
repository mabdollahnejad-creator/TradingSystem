namespace TradingSystem.Application.Abstractions;

public interface INobitexSymbolService
{
    Task<HashSet<string>> GetAvailableSymbolsAsync();
}