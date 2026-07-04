using TradingSystem.Domain.Entities;

namespace TradingSystem.Application.Abstractions;

public interface ICryptoRepository
{
    Task<Cryptocurrency?> GetBySymbolAsync(string symbol);
    Task<Cryptocurrency> AddAsync(Cryptocurrency crypto);
    Task<List<string>> GetActiveSymbolsAsync();
    Task SaveChangesAsync();
    Task DeleteBySymbolAsync(string symbol);

    // ✅ متد جدید: بروزرسانی فیلد IsAvailable برای یک صرافی خاص
    Task UpdateExchangeAvailabilityAsync(string symbol, string exchange, bool isAvailable);
    // ✅ متد جدید: دریافت همه رمزارزها برای backup
    Task<List<Cryptocurrency>> GetAllAsync();
    Task<List<Cryptocurrency>> GetTopActiveAsync(int count);

}