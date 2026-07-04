using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradingSystem.Application.Abstractions;

namespace TradingSystem.Infrastructure.MarketData;

public class NobitexSymbolService : INobitexSymbolService
{
    private readonly HttpClient _http;
    private HashSet<string>? _cachedSymbols;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly string _logFile;

    public NobitexSymbolService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nobitex_symbols_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<HashSet<string>> GetAvailableSymbolsAsync()
    {
        if (_cachedSymbols != null && (DateTime.UtcNow - _lastFetch).TotalHours < 1)
            return _cachedSymbols;

        try
        {
            var url = "https://apiv2.nobitex.ir/market/stats";
            Log($"[Nobitex] Fetching symbols from: {url}");

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Log($"[Nobitex] Response length: {json.Length}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("stats", out var statsProp))
            {
                Log("[Nobitex] ️ 'stats' property not found");
                return new HashSet<string>();
            }

            var symbols = new HashSet<string>();

            // ✅ stats یک Object است، نه Array
            foreach (var stat in statsProp.EnumerateObject())
            {
                var market = stat.Name; // مثلاً "BTC-USDT" یا "BTC-IRT"
                if (!string.IsNullOrEmpty(market))
                {
                    var extracted = ExtractSymbolFromMarket(market);
                    if (!string.IsNullOrEmpty(extracted))
                        symbols.Add(extracted);
                }
            }

            _cachedSymbols = symbols;
            _lastFetch = DateTime.UtcNow;

            Log($"[Nobitex] ✅ Loaded {symbols.Count} available symbols");
            return symbols;
        }
        catch (Exception ex)
        {
            Log($"[Nobitex] ❌ Error fetching symbols: {ex.Message}");
            return _cachedSymbols ?? new HashSet<string>();
        }
    }

    private string? ExtractSymbolFromMarket(string market)
    {
        // فرمت نوبیتکس: "BTC-USDT" یا "BTC-IRT"
        var parts = market.Split('-');
        if (parts.Length >= 2)
        {
            var baseSymbol = parts[0].ToUpperInvariant();
            var quoteSymbol = parts[1].ToUpperInvariant();

            // فقط جفت‌های USDT و IRT را قبول کن
            if (quoteSymbol == "USDT" || quoteSymbol == "IRT")
                return baseSymbol;
        }
        return null;
    }
}