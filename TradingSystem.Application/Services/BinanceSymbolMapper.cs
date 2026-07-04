using System;
using System.Collections.Generic;

namespace TradingSystem.Application.Services;

public interface IBinanceSymbolMapper
{
    string GetBinanceSymbol(string symbol);
    bool TryGetBinanceSymbol(string symbol, out string binanceSymbol);
}

public class BinanceSymbolMapper : IBinanceSymbolMapper
{
    private static readonly Dictionary<string, string> SymbolMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "HYPE", "HYPERUSDT" },
        { "BTC", "BTCUSDT" },
        { "ETH", "ETHUSDT" },
        { "BNB", "BNBUSDT" },
        { "SOL", "SOLUSDT" },
        { "XRP", "XRPUSDT" },
        { "TRX", "TRXUSDT" },
        { "USDC", "USDCUSDT" },
        { "USDT", "USDTUSD" }
    };

    public string GetBinanceSymbol(string symbol)
    {
        if (TryGetBinanceSymbol(symbol, out var binanceSymbol))
        {
            return binanceSymbol;
        }
        return $"{symbol.ToUpperInvariant()}USDT";
    }

    public bool TryGetBinanceSymbol(string symbol, out string binanceSymbol)
    {
        return SymbolMap.TryGetValue(symbol.ToUpperInvariant().Trim(), out binanceSymbol!);
    }
}