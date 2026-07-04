using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;

namespace TradingSystem.Infrastructure.MarketData;

public class CoinGeckoMetadataService : ICoinGeckoMetadataService
{
    private readonly HttpClient _http;
    private readonly string _logFile;

    public CoinGeckoMetadataService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);

        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "coingecko_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<CryptocurrencyDto>> GetTopCryptocurrenciesAsync(int count)
    {
        Log($"[CoinGecko] 🚀 GetTopCryptocurrenciesAsync STARTED (count={count})");

        if (count < 1 || count > 250)
            throw new ArgumentException("Count must be between 1 and 250");

        var url = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page={count}&page=1&sparkline=false";

        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var coins = JsonSerializer.Deserialize<List<CoinGeckoMarketResponse>>(json, options);

            if (coins == null || coins.Count == 0)
                return new List<CryptocurrencyDto>();

            var result = coins.Select(c => new CryptocurrencyDto
            {
                Id = c.Id,
                Symbol = c.Symbol.ToUpperInvariant(),
                Name = c.Name,
                MarketCapRank = c.MarketCapRank,
                ImageUrl = c.Image,
                MarketCap = c.MarketCap,
                Volume24h = c.TotalVolume
            }).ToList();

            Log($"[CoinGecko] ✅ Returning {result.Count} cryptocurrencies");
            return result;
        }
        catch (Exception ex)
        {
            Log($"[CoinGecko] ❌ Exception: {ex.Message}");
            throw new InvalidOperationException($"Error fetching metadata from CoinGecko: {ex.Message}", ex);
        }
    }

    public async Task<CryptocurrencyDto?> SearchBySymbolAsync(string symbol)
    {
        Log($"[CoinGecko] 🚀 SearchBySymbolAsync STARTED for '{symbol}'");

        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var upperSymbol = symbol.ToUpperInvariant();

        try
        {
            // ۱. جستجو با endpoint /search
            var searchUrl = $"https://api.coingecko.com/api/v3/search?query={Uri.EscapeDataString(upperSymbol)}";
            var searchResponse = await _http.GetAsync(searchUrl);

            if (!searchResponse.IsSuccessStatusCode)
                return null;

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<CoinGeckoSearchResponse>(searchJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (searchResult?.Coins == null || searchResult.Coins.Count == 0)
                return null;

            // ۲. پیدا کردن دقیق‌ترین match
            var match = searchResult.Coins
                .Where(c => c.Symbol.Equals(upperSymbol, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.MarketCapRank ?? int.MaxValue)
                .FirstOrDefault();

            if (match == null)
                return null;

            Log($"[CoinGecko] ✅ Found match: {match.Name} (ID: {match.Id})");

            // ۳. دریافت اطلاعات کامل
            var detailUrl = $"https://api.coingecko.com/api/v3/coins/{match.Id}?localization=false&tickers=false&market_data=true&community_data=false&developer_data=false&sparkline=false";
            var detailResponse = await _http.GetAsync(detailUrl);

            if (!detailResponse.IsSuccessStatusCode)
            {
                return new CryptocurrencyDto
                {
                    Id = match.Id,
                    Symbol = match.Symbol.ToUpperInvariant(),
                    Name = match.Name,
                    MarketCapRank = match.MarketCapRank
                };
            }

            var detailJson = await detailResponse.Content.ReadAsStringAsync();

            // ✅ استفاده از JsonDocument برای پارس دستی (CoinGecko ساختار پیچیده دارد)
            using var doc = JsonDocument.Parse(detailJson);
            var root = doc.RootElement;

            var result = new CryptocurrencyDto
            {
                Id = root.GetProperty("id").GetString() ?? match.Id,
                Symbol = root.GetProperty("symbol").GetString()?.ToUpperInvariant() ?? match.Symbol.ToUpperInvariant(),
                Name = root.GetProperty("name").GetString() ?? match.Name,
                MarketCapRank = root.TryGetProperty("market_cap_rank", out var rankProp) && rankProp.ValueKind == JsonValueKind.Number
                    ? rankProp.GetInt32()
                    : match.MarketCapRank
            };

            // ✅ استخراج MarketCap و Volume از market_data
            if (root.TryGetProperty("market_data", out var marketData))
            {
                // MarketCap
                if (marketData.TryGetProperty("market_cap", out var marketCap))
                {
                    if (marketCap.TryGetProperty("usd", out var usdCap) && usdCap.ValueKind == JsonValueKind.Number)
                    {
                        result.MarketCap = usdCap.GetDecimal();
                    }
                }

                // Volume
                if (marketData.TryGetProperty("total_volume", out var volume))
                {
                    if (volume.TryGetProperty("usd", out var usdVolume) && usdVolume.ValueKind == JsonValueKind.Number)
                    {
                        result.Volume24h = usdVolume.GetDecimal();
                    }
                }
            }

            Log($"[CoinGecko] ✅ Full metadata: Name={result.Name}, Rank={result.MarketCapRank}, MarketCap={result.MarketCap}, Volume={result.Volume24h}");

            return result;
        }
        catch (Exception ex)
        {
            Log($"[CoinGecko] ❌ Exception: {ex.Message}");
            return null;
        }
    }

    private class CoinGeckoMarketResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? MarketCapRank { get; set; }
        public string Image { get; set; } = string.Empty;
        public decimal? MarketCap { get; set; }
        public decimal? TotalVolume { get; set; }
    }

    private class CoinGeckoSearchResponse
    {
        public List<CoinGeckoSearchResult> Coins { get; set; } = new();
    }

    private class CoinGeckoSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? MarketCapRank { get; set; }
    }
}