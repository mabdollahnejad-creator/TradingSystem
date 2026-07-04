using System.Diagnostics;
using TradingSystem.Application.Abstractions;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Application.UseCases;

public class SyncMetadataUseCase
{
    private readonly ICoinGeckoMetadataService _metadataService;
    private readonly ICryptoRepository _cryptoRepo;
    private readonly IBinanceSymbolService _binanceSymbolService;
    private readonly INobitexSymbolService _nobitexSymbolService;
    private readonly IWallexSymbolService _wallexSymbolService;

    public SyncMetadataUseCase(
        ICoinGeckoMetadataService metadataService,
        ICryptoRepository cryptoRepo,
        IBinanceSymbolService binanceSymbolService,
        INobitexSymbolService nobitexSymbolService,
        IWallexSymbolService wallexSymbolService)
    {
        _metadataService = metadataService;
        _cryptoRepo = cryptoRepo;
        _binanceSymbolService = binanceSymbolService;
        _nobitexSymbolService = nobitexSymbolService;
        _wallexSymbolService = wallexSymbolService;

        Debug.WriteLine("[SyncMetadataUseCase] ✅ Constructor called");
    }

    public async Task ExecuteAsync(int topCount, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("═══════════════════════════════════════════════════════");
        Debug.WriteLine("[SyncMetadata] ═══ START (Top N) ═══");
        Debug.WriteLine($"[SyncMetadata] Top Count: {topCount}");

        try
        {
            Debug.WriteLine("[Step 1] Fetching available symbols from exchanges...");

            var binanceSymbols = await _binanceSymbolService.GetAvailableSymbolsAsync();
            Debug.WriteLine($"[Step 1a] ✅ Binance: {binanceSymbols.Count} symbols loaded");

            var nobitexSymbols = await _nobitexSymbolService.GetAvailableSymbolsAsync();
            Debug.WriteLine($"[Step 1b] ✅ Nobitex: {nobitexSymbols.Count} symbols loaded");

            var wallexSymbols = await _wallexSymbolService.GetAvailableSymbolsAsync();
            Debug.WriteLine($"[Step 1c] ✅ Wallex: {wallexSymbols.Count} symbols loaded");

            Debug.WriteLine($"\n[Step 2] Fetching top {topCount} cryptocurrencies from CoinGecko...");
            var topCryptos = await _metadataService.GetTopCryptocurrenciesAsync(topCount);
            Debug.WriteLine($"[Step 2] ✅ Received {topCryptos.Count} cryptocurrencies from CoinGecko");

            if (topCryptos.Count == 0)
            {
                Debug.WriteLine("[Step 2] ⚠️ CoinGecko returned empty list.");
                //return 0;
            }

            int added = 0;
            int updated = 0;

            Debug.WriteLine($"\n[Step 3] Processing {topCryptos.Count} cryptocurrencies...");

            foreach (var cryptoDto in topCryptos)
            {
                var isInBinance = binanceSymbols.Contains(cryptoDto.Symbol);
                var isInNobitex = nobitexSymbols.Contains(cryptoDto.Symbol);
                var isInWallex = wallexSymbols.Contains(cryptoDto.Symbol);

                var existing = await _cryptoRepo.GetBySymbolAsync(cryptoDto.Symbol);

                if (existing == null)
                {
                    var newCrypto = new Cryptocurrency
                    {
                        Symbol = cryptoDto.Symbol,
                        Name = cryptoDto.Name,
                        CoinGeckoId = cryptoDto.Id,
                        MarketCapRank = cryptoDto.MarketCapRank,
                        MarketCap = cryptoDto.MarketCap ?? 0,
                        Volume24h = cryptoDto.Volume24h ?? 0,
                        IsAvailableInBinance = isInBinance,
                        IsAvailableInNobitex = isInNobitex,
                        IsAvailableInWallex = isInWallex,
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow
                    };

                    await _cryptoRepo.AddAsync(newCrypto);
                    added++;

                    Debug.WriteLine($"[Step 3] ✅ Added: {cryptoDto.Symbol} | Binance: {isInBinance}, Nobitex: {isInNobitex}, Wallex: {isInWallex}");
                }
                else
                {
                    existing.Name = cryptoDto.Name;
                    existing.CoinGeckoId = cryptoDto.Id;
                    existing.MarketCapRank = cryptoDto.MarketCapRank;
                    existing.MarketCap = cryptoDto.MarketCap ?? 0;
                    existing.Volume24h = cryptoDto.Volume24h ?? 0;
                    existing.IsAvailableInBinance = isInBinance;
                    existing.IsAvailableInNobitex = isInNobitex;
                    existing.IsAvailableInWallex = isInWallex;
                    existing.LastUpdated = DateTime.UtcNow;
                    updated++;

                    Debug.WriteLine($"[Step 3] 🔄 Updated: {cryptoDto.Symbol}");
                }
            }

            Debug.WriteLine($"\n[Step 4] Saving changes to database...");
            await _cryptoRepo.SaveChangesAsync();
            Debug.WriteLine($"[Step 4] ✅ Database save completed");

            Debug.WriteLine("\n═══════════════════════════════════════════════════════");
            Debug.WriteLine($"[SyncMetadata] ═══ COMPLETED ═══ Added: {added}, Updated: {updated}");
            Debug.WriteLine("═══════════════════════════════════════════════════════");

            //return added;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"\n❌ [SyncMetadata] EXCEPTION: {ex.Message}");
            Debug.WriteLine($"❌ [SyncMetadata] Stack: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<bool> SyncSingleCryptoMetadataAsync(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant();
        Debug.WriteLine($"═══════════════════════════════════════════════════════");
        Debug.WriteLine($"[SyncMetadata] 🚀 SyncSingleCryptoMetadataAsync START for {upperSymbol}");

        try
        {
            // ۱. دریافت لیست نمادهای موجود در صرافی‌ها
            Debug.WriteLine("[Step 1] Checking symbol availability in exchanges...");

            var binanceSymbols = await _binanceSymbolService.GetAvailableSymbolsAsync();
            var nobitexSymbols = await _nobitexSymbolService.GetAvailableSymbolsAsync();
            var wallexSymbols = await _wallexSymbolService.GetAvailableSymbolsAsync();

            var isInBinance = binanceSymbols.Contains(upperSymbol);
            var isInNobitex = nobitexSymbols.Contains(upperSymbol);
            var isInWallex = wallexSymbols.Contains(upperSymbol);

            Debug.WriteLine($"[Step 1] ✅ Availability: Binance={isInBinance}, Nobitex={isInNobitex}, Wallex={isInWallex}");

            // ۲. جستجو در CoinGecko برای دریافت متادیتا
            Debug.WriteLine($"[Step 2] Searching CoinGecko for {upperSymbol}...");
            var cryptoDto = await _metadataService.SearchBySymbolAsync(upperSymbol);

            if (cryptoDto == null)
            {
                Debug.WriteLine($"[Step 2] ⚠️ Symbol not found in CoinGecko. Creating basic record...");

                var existing = await _cryptoRepo.GetBySymbolAsync(upperSymbol);
                Debug.WriteLine($"[Step 2] Existing record: {(existing != null ? "YES" : "NO")}");

                if (existing == null)
                {
                    Debug.WriteLine($"[Step 2] Creating new basic record...");
                    var newCrypto = new Cryptocurrency
                    {
                        Symbol = upperSymbol,
                        Name = upperSymbol,
                        CoinGeckoId = null,
                        IsAvailableInBinance = isInBinance,
                        IsAvailableInNobitex = isInNobitex,
                        IsAvailableInWallex = isInWallex,
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow
                    };

                    Debug.WriteLine($"[Step 2] Calling AddAsync...");
                    await _cryptoRepo.AddAsync(newCrypto);

                    Debug.WriteLine($"[Step 2] Calling SaveChangesAsync...");
                    await _cryptoRepo.SaveChangesAsync();
                    Debug.WriteLine($"[Step 2] ✅ Created basic record for {upperSymbol}");
                }
                else
                {
                    Debug.WriteLine($"[Step 2] Updating existing record...");
                    existing.IsAvailableInBinance = isInBinance;
                    existing.IsAvailableInNobitex = isInNobitex;
                    existing.IsAvailableInWallex = isInWallex;
                    existing.LastUpdated = DateTime.UtcNow;

                    Debug.WriteLine($"[Step 2] Calling SaveChangesAsync...");
                    await _cryptoRepo.SaveChangesAsync();
                    Debug.WriteLine($"[Step 2] 🔄 Updated availability for {upperSymbol}");
                }

                Debug.WriteLine($"═══════════════════════════════════════════════════════");
                return true;
            }

            Debug.WriteLine($"[Step 2] ✅ Found in CoinGecko: {cryptoDto.Name} (ID: {cryptoDto.Id})");
            Debug.WriteLine($"[Step 2] 📊 MarketCap: {cryptoDto.MarketCap}, Volume: {cryptoDto.Volume24h}, Rank: {cryptoDto.MarketCapRank}");

            // ۳. ذخیره یا بروزرسانی در دیتابیس
            Debug.WriteLine($"[Step 3] Checking if record exists in DB...");
            var existingCrypto = await _cryptoRepo.GetBySymbolAsync(upperSymbol);
            Debug.WriteLine($"[Step 3] Existing record: {(existingCrypto != null ? "YES" : "NO")}");

            if (existingCrypto == null)
            {
                Debug.WriteLine($"[Step 3] Creating new record with full metadata...");
                var newCrypto = new Cryptocurrency
                {
                    Symbol = upperSymbol,
                    Name = cryptoDto.Name,
                    CoinGeckoId = cryptoDto.Id,
                    MarketCapRank = cryptoDto.MarketCapRank,
                    MarketCap = cryptoDto.MarketCap ?? 0,
                    Volume24h = cryptoDto.Volume24h ?? 0,
                    IsAvailableInBinance = isInBinance,
                    IsAvailableInNobitex = isInNobitex,
                    IsAvailableInWallex = isInWallex,
                    IsActive = true,
                    LastUpdated = DateTime.UtcNow
                };

                Debug.WriteLine($"[Step 3] Calling AddAsync...");
                await _cryptoRepo.AddAsync(newCrypto);
                Debug.WriteLine($"[Step 3] ✅ Added: {upperSymbol} with full metadata");
            }
            else
            {
                Debug.WriteLine($"[Step 3] Updating existing record with full metadata...");
                existingCrypto.Name = cryptoDto.Name;
                existingCrypto.CoinGeckoId = cryptoDto.Id;
                existingCrypto.MarketCapRank = cryptoDto.MarketCapRank;
                existingCrypto.MarketCap = cryptoDto.MarketCap ?? 0;
                existingCrypto.Volume24h = cryptoDto.Volume24h ?? 0;
                existingCrypto.IsAvailableInBinance = isInBinance;
                existingCrypto.IsAvailableInNobitex = isInNobitex;
                existingCrypto.IsAvailableInWallex = isInWallex;
                existingCrypto.LastUpdated = DateTime.UtcNow;
                Debug.WriteLine($"[Step 3] 🔄 Updated: {upperSymbol} with full metadata");
            }

            Debug.WriteLine($"[Step 4] Calling SaveChangesAsync...");
            await _cryptoRepo.SaveChangesAsync();
            Debug.WriteLine($"[Step 4] ✅ Database save completed");

            Debug.WriteLine($"═══════════════════════════════════════════════════════");
            Debug.WriteLine($"[SyncMetadata] ═══ COMPLETED for {upperSymbol} ═══");
            Debug.WriteLine($"═══════════════════════════════════════════════════════");

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ [SyncMetadata] EXCEPTION for {upperSymbol}: {ex.Message}");
            Debug.WriteLine($"❌ [SyncMetadata] Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"❌ [SyncMetadata] Inner: {ex.InnerException.Message}");
            }
            return false;
        }
    }
}