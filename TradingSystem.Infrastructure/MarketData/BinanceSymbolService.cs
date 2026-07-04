using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TradingSystem.Application.Abstractions;

namespace TradingSystem.Infrastructure.MarketData;

public class BinanceSymbolService : IBinanceSymbolService
{
    private readonly HttpClient _http;
    private HashSet<string>? _cachedSymbols;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly string _logFile;

    public BinanceSymbolService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromMinutes(2);
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "binance_symbols_log.txt");
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
        if (_cachedSymbols != null && (DateTime.UtcNow - _lastFetch).TotalHours < 24)
        {
            Log($"[Binance] Using cached symbols ({_cachedSymbols.Count} symbols)");
            return _cachedSymbols;
        }

        try
        {
            Log("[Binance] Fetching available symbols from data.binance.vision...");

            var allSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? nextMarker = null;
            int pageCount = 0;

            do
            {
                var url = "https://data.binance.vision/?prefix=data/spot/daily/klines/&delimiter=/";
                if (!string.IsNullOrEmpty(nextMarker))
                    url += $"&marker={Uri.EscapeDataString(nextMarker)}";

                Log($"[Binance] Fetching page {++pageCount}: {url}");

                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var xml = await response.Content.ReadAsStringAsync();
                Log($"[Binance] Response length: {xml.Length}");

                // ✅ استفاده از Regex به جای XML Parser (برای جلوگیری از خطای XML نامعتبر)
                var symbols = ExtractSymbolsWithRegex(xml);

                foreach (var symbol in symbols)
                    allSymbols.Add(symbol);

                // استخراج NextMarker با Regex
                var nextMarkerMatch = Regex.Match(xml, @"<NextMarker>([^<]+)</NextMarker>");
                nextMarker = nextMarkerMatch.Success ? nextMarkerMatch.Groups[1].Value : null;

                Log($"[Binance] Page {pageCount}: Found {symbols.Count} symbols");

                if (pageCount > 10) break;

            } while (!string.IsNullOrEmpty(nextMarker));

            _cachedSymbols = allSymbols;
            _lastFetch = DateTime.UtcNow;

            Log($"[Binance] ✅ Total symbols loaded: {allSymbols.Count}");
            return allSymbols;
        }
        catch (Exception ex)
        {
            Log($"[Binance] ❌ Error fetching symbols: {ex.Message}");
            Log($"[Binance] ❌ Stack: {ex.StackTrace}");
            return _cachedSymbols ?? new HashSet<string>();
        }
    }

    // ✅ استخراج نمادها با Regex (مقاوم در برابر XML نامعتبر)
    private List<string> ExtractSymbolsWithRegex(string xml)
    {
        var symbols = new List<string>();

        // پیدا کردن تمام Prefixها: <Prefix>data/spot/daily/klines/BTCUSDT/</Prefix>
        var matches = Regex.Matches(xml, @"<Prefix>data/spot/daily/klines/([^/]+)/</Prefix>");

        foreach (Match match in matches)
        {
            var symbolWithQuote = match.Groups[1].Value;

            // استخراج فقط بخش قبل از USDT
            if (symbolWithQuote.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
            {
                var symbol = symbolWithQuote[..^4].ToUpperInvariant();
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    public bool IsSymbolAvailable(string symbol, HashSet<string> availableSymbols)
    {
        return availableSymbols.Contains(symbol.ToUpperInvariant());
    }
}