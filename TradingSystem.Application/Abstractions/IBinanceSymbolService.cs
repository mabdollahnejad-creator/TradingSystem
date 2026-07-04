namespace TradingSystem.Application.Abstractions;

public interface IBinanceSymbolService
{
    Task<HashSet<string>> GetAvailableSymbolsAsync();
    bool IsSymbolAvailable(string symbol, HashSet<string> availableSymbols);
}