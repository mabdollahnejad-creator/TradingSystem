using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradingSystem.Application.Abstractions;

namespace TradingSystem.Infrastructure.MarketData;

public class WallexSymbolService : IWallexSymbolService
{
    private readonly HttpClient _http;
    private HashSet<string>? _cachedSymbols;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly string _logFile;

    public WallexSymbolService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallex_symbols_log.txt");
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
            // ✅ endpoint صحیح: /v1/markets
            var url = "https://api.wallex.ir/v1/markets";
            Log($"[Wallex] Fetching symbols from: {url}");

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Log($"[Wallex] ❌ HTTP Error: {response.StatusCode}");
                return new HashSet<string>();
            }

            var json = await response.Content.ReadAsStringAsync();
            Log($"[Wallex] Response length: {json.Length}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("result", out var resultProp))
            {
                Log("[Wallex] ️ 'result' property not found");
                return new HashSet<string>();
            }

            var symbols = new HashSet<string>();

            // اگر result آرایه است
            if (resultProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var market in resultProp.EnumerateArray())
                {
                    if (market.TryGetProperty("symbol", out var symbolProp))
                    {
                        var symbol = symbolProp.GetString();
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            var extracted = ExtractSymbol(symbol);
                            if (!string.IsNullOrEmpty(extracted))
                                symbols.Add(extracted);
                        }
                    }
                }
            }
            // اگر result آبجکت است
            else if (resultProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var market in resultProp.EnumerateObject())
                {
                    var symbol = market.Name;
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        var extracted = ExtractSymbol(symbol);
                        if (!string.IsNullOrEmpty(extracted))
                            symbols.Add(extracted);
                    }
                }
            }

            _cachedSymbols = symbols;
            _lastFetch = DateTime.UtcNow;

            Log($"[Wallex] ✅ Loaded {symbols.Count} available symbols");
            return symbols;
        }
        catch (Exception ex)
        {
            Log($"[Wallex] ❌ Error fetching symbols: {ex.Message}");
            return _cachedSymbols ?? new HashSet<string>();
        }
    }

    private string? ExtractSymbol(string symbol)
    {
        if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
            return symbol[..^4].ToUpperInvariant();

        if (symbol.EndsWith("IRT", StringComparison.OrdinalIgnoreCase))
            return symbol[..^3].ToUpperInvariant();

        return symbol.ToUpperInvariant();
    }
}