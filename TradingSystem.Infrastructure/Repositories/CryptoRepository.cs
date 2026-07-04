using Microsoft.EntityFrameworkCore;
using TradingSystem.Application.Abstractions;
using TradingSystem.Domain.Entities;
using TradingSystem.Infrastructure.Persistence;

namespace TradingSystem.Infrastructure.Repositories;

public class CryptoRepository : ICryptoRepository
{
    private readonly AppDbContext _db;

    public CryptoRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Cryptocurrency?> GetBySymbolAsync(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        return await _db.Set<Cryptocurrency>()
            .FirstOrDefaultAsync(x => x.Symbol == upperSymbol);
    }

    public async Task<Cryptocurrency> AddAsync(Cryptocurrency crypto)
    {
        // ✅ بررسی وجود نماد قبل از اضافه کردن
        var existing = await GetBySymbolAsync(crypto.Symbol);
        if (existing != null)
        {
            return existing;
        }

        //_context.Cryptocurrencies.Add(crypto);
        await _db.Set<Cryptocurrency>().AddAsync(crypto);
        return crypto;

    }

    public async Task<List<string>> GetActiveSymbolsAsync()
    {
        return await _db.Set<Cryptocurrency>()
            .Where(x => x.IsActive)
            .Select(x => x.Symbol)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBySymbolAsync(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        var crypto = await _db.Set<Cryptocurrency>()
            .FirstOrDefaultAsync(x => x.Symbol == upperSymbol);

        if (crypto != null)
        {
            _db.Set<Cryptocurrency>().Remove(crypto);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateExchangeAvailabilityAsync(string symbol, string exchange, bool isAvailable)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        var crypto = await _db.Set<Cryptocurrency>()
            .FirstOrDefaultAsync(x => x.Symbol == upperSymbol);

        if (crypto == null) return;

        switch (exchange)
        {
            case "Binance":
                crypto.IsAvailableInBinance = isAvailable;
                break;
            case "Nobitex":
                crypto.IsAvailableInNobitex = isAvailable;
                break;
            case "Wallex":
                crypto.IsAvailableInWallex = isAvailable;
                break;
        }

        await _db.SaveChangesAsync();
    }   

    // ✅ پیاده‌سازی متد جدید
    public async Task<List<Cryptocurrency>> GetAllAsync()
    {
        return await _db.Set<Cryptocurrency>().ToListAsync();
    }

    // ✅ متد جدید: دریافت N نماد برتر فعال
    public async Task<List<Cryptocurrency>> GetTopActiveAsync(int count)
    {
        return await _db.Set<Cryptocurrency>()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.MarketCapRank ?? int.MaxValue)
            .ThenBy(c => c.Symbol)
            .Take(count)
            .ToListAsync();

    }



}