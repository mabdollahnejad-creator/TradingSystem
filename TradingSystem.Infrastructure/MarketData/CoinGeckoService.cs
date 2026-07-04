using System.Text.Json;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;

namespace TradingSystem.Infrastructure.MarketData;

public class CoinGeckoService : IMarketDataProvider
{
    private readonly HttpClient _http;

    public CoinGeckoService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);

        // ✅ حذف کامل headers قبلی و تنظیم User-Agent استاندارد مرورگر
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    public async Task<List<CandleDto>> FetchCandlesAsync(string symbol, int days)
    {
        if (days > 365) days = 365;
        if (days < 1) days = 1;

        var url = $"https://api.coingecko.com/api/v3/coins/{symbol}/ohlc?vs_currency=usd&days={days}";

        try
        {
            var response = await _http.GetAsync(url);

            // مدیریت دقیق خطاها
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"CoinGecko blocked this request (403 Forbidden). This usually means your IP is restricted. " +
                    $"Response: {content}");
            }

            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var ohlcData = JsonSerializer.Deserialize<List<List<decimal>>>(jsonString);

            if (ohlcData == null || ohlcData.Count == 0)
                return new List<CandleDto>();

            var candles = new List<CandleDto>();

            foreach (var item in ohlcData)
            {
                if (item.Count >= 5)
                {
                    candles.Add(new CandleDto
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)item[0]).UtcDateTime,
                        Open = item[1],
                        High = item[2],
                        Low = item[3],
                        Close = item[4],
                        Volume = 0
                    });
                }
            }

            return candles.OrderBy(c => c.OpenTime).ToList();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error fetching data for {symbol}: {ex.Message}", ex);
        }
    }
}