namespace TradingSystem.Application.Abstractions;

public interface IWallexSymbolService
{
    Task<HashSet<string>> GetAvailableSymbolsAsync();
}