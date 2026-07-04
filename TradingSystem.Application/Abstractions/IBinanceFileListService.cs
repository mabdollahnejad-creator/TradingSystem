using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradingSystem.Application.Abstractions;

public interface IBinanceFileListService
{
    Task<List<DateTime>> GetAvailableDatesAsync(string binanceSymbol, string tfFolder);
    Task<DateTime?> GetFirstAvailableDateAsync(string binanceSymbol, string tfFolder);
    Task<bool> SymbolExistsAsync(string binanceSymbol, string tfFolder);
}