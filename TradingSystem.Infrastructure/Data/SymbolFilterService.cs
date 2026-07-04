using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TradingSystem.Domain.Entities;
using TradingSystem.Infrastructure.Persistence;


namespace TradingSystem.Infrastructure.MarketData
{
    public class SymbolFilterService
    {
        private readonly HttpClient _httpClient;
        private static readonly HashSet<string> Tier1Exchanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Binance", "Coinbase Exchange", "Kraken", "OKX", "Bybit"
        };
        private static readonly HashSet<string> StablecoinSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USDT", "USDC", "BUSD", "DAI", "TUSD", "USDP", "FDUSD", "USTC", "USDE", "USDD",
            "GUSD", "PAXG", "XAUT"
        };

        public SymbolFilterService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradingSystem/1.0");
        }

        public async Task<List<Cryptocurrency>> FilterSymbolsAsync(
            int topCount = 250,
            long? minVolume24h = null,
            long? minMarketCap = null,
            double? minVolumeToMcapRatio = null,
            int minDaysSinceListed = 365,
            int maxSymbols = 20,
            bool requireTier1Exchange = false)
        {
            var url = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page={topCount}&page=1&sparkline=false";
            var json = await _httpClient.GetStringAsync(url);
            var data = JArray.Parse(json);

            var filtered = new List<Cryptocurrency>();
            foreach (var coin in data)
            {
                var volume = coin["total_volume"]?.Value<long>() ?? 0;
                if (minVolume24h.HasValue && volume < minVolume24h.Value) continue;

                var marketCap = coin["market_cap"]?.Value<long>() ?? 0;
                if (minMarketCap.HasValue && marketCap < minMarketCap.Value) continue;

                if (minVolumeToMcapRatio.HasValue && marketCap > 0)
                {
                    double ratio = (double)volume / marketCap;
                    if (ratio < minVolumeToMcapRatio.Value) continue;
                }

                var athDateStr = coin["ath_date"]?.Value<string>();
                if (!string.IsNullOrEmpty(athDateStr) && DateTime.TryParse(athDateStr, out var athDate))
                {
                    if ((DateTime.UtcNow - athDate).TotalDays < minDaysSinceListed) continue;
                }

                var symbol = coin["symbol"]?.Value<string>()?.ToUpper();
                if (IsStablecoin(symbol)) continue;

                var coinGeckoId = coin["id"]?.Value<string>();
                var name = coin["name"]?.Value<string>();

                if (requireTier1Exchange)
                {
                    if (string.IsNullOrEmpty(coinGeckoId)) continue;
                    var hasTier1 = await CheckTier1ExchangeAsync(coinGeckoId);
                    if (!hasTier1) continue;
                    await Task.Delay(250);
                }

                filtered.Add(new Cryptocurrency
                {
                    Symbol = symbol,
                    Name = name,
                    CoinGeckoId = coinGeckoId,
                    MarketCapRank = coin["market_cap_rank"]?.Value<int>() ?? null,
                    Volume24h = volume,
                    MarketCap = marketCap,
                    IsActive = true,
                    LastUpdated = DateTime.UtcNow
                });

                if (filtered.Count >= maxSymbols)
                    break;
            }

            return filtered;
        }

        private async Task<bool> CheckTier1ExchangeAsync(string coinGeckoId)
        {
            try
            {
                var url = $"https://api.coingecko.com/api/v3/coins/{coinGeckoId}/tickers";
                var json = await _httpClient.GetStringAsync(url);
                var tickers = JObject.Parse(json)["tickers"] as JArray;
                if (tickers == null) return false;
                foreach (var ticker in tickers)
                {
                    var marketName = ticker["market"]?["name"]?.Value<string>();
                    if (!string.IsNullOrEmpty(marketName) && Tier1Exchanges.Contains(marketName))
                        return true;
                }
            }
            catch { return false; }
            return false;
        }

        private bool IsStablecoin(string symbol) => StablecoinSymbols.Contains(symbol);
    }
}