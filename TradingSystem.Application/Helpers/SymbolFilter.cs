using System.Collections.Generic;
using System.Linq;

namespace TradingSystem.Application.Helpers;

public static class SymbolFilter
{
    private static readonly HashSet<string> ExcludedStablecoins = new(System.StringComparer.OrdinalIgnoreCase)
{
    "USDC", "BUSD", "DAI", "TUSD", "USDP", "FRAX", "FDUSD", "PYUSD", "GUSD", "LUSD", "EURS", "EURT"  // ✅ حذف فاصله
};

    public static bool ShouldDownload(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        if (symbol.Equals("USDT", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return !ExcludedStablecoins.Contains(symbol);
    }

    public static bool IsStablecoin(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        return symbol.Equals("USDT", System.StringComparison.OrdinalIgnoreCase)
               || ExcludedStablecoins.Contains(symbol);
    }

    public static List<string> FilterSymbols(IEnumerable<string> symbols)
    {
        return symbols
            .Where(s => ShouldDownload(s))
            .ToList();
    }

    public static void AddToBlacklist(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            ExcludedStablecoins.Add(symbol.ToUpperInvariant());
        }
    }

    public static void RemoveFromBlacklist(string symbol)
    {
        ExcludedStablecoins.Remove(symbol.ToUpperInvariant());
    }
}