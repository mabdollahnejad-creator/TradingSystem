using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Infrastructure.MarketData;

public class WallexService : IWallexExchangeService
{
    private readonly HttpClient _http;
    private readonly string _logFile;

    public WallexService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallex_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<CandleDto>> FetchCandlesAsync(string symbol, Timeframe timeframe, DateTime from, DateTime to)
    {
        Log($"[Wallex] 🚀 Fetching {symbol} | {timeframe} | {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

        // ✅ USDT در Wallex وجود ندارد
        if (symbol.Equals("USDT", StringComparison.OrdinalIgnoreCase))
        {
            Log($"[Wallex] ⚠️ Skipping USDT - not available in Wallex");
            return new List<CandleDto>();
        }

        var resolution = timeframe switch
        {
            Timeframe.M1 => "1",
            Timeframe.M15 => "15",
            Timeframe.H1 => "60",
            Timeframe.H4 => "240",
            Timeframe.H8 => "480",
            Timeframe.H12 => "720",
            Timeframe.D1 => "1D",
            Timeframe.D3 => "3D",
            _ => "60"
        };

        var wallexSymbol = $"{symbol.ToUpperInvariant()}USDT";
        var fromTimestamp = new DateTimeOffset(from).ToUnixTimeSeconds();
        var toTimestamp = new DateTimeOffset(to).ToUnixTimeSeconds();

        var url = $"https://api.wallex.ir/v1/udf/history?symbol={wallexSymbol}&resolution={resolution}&from={fromTimestamp}&to={toTimestamp}";

        Log($"[Wallex] 🌐 URL: {url}");

        try
        {
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Log($"[Wallex] ❌ HTTP Error: {response.StatusCode}");
                return new List<CandleDto>();
            }

            var json = await response.Content.ReadAsStringAsync();
            Log($"[Wallex] 📄 Response length: {json.Length}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("s", out var statusProp))
            {
                var status = statusProp.GetString();
                if (status != "ok")
                {
                    Log($"[Wallex] ⚠️ API returned status: {status}");
                    return new List<CandleDto>();
                }
            }

            if (!root.TryGetProperty("t", out var tProp) ||
                !root.TryGetProperty("o", out var oProp) ||
                !root.TryGetProperty("h", out var hProp) ||
                !root.TryGetProperty("l", out var lProp) ||
                !root.TryGetProperty("c", out var cProp))
            {
                Log($"[Wallex] ⚠️ Missing required fields");
                return new List<CandleDto>();
            }

            var tArray = tProp.EnumerateArray().Select(e => e.GetInt64()).ToArray();
            var oArray = ParseDecimalArray(oProp, "o");
            var hArray = ParseDecimalArray(hProp, "h");
            var lArray = ParseDecimalArray(lProp, "l");
            var cArray = ParseDecimalArray(cProp, "c");

            decimal[] vArray = Array.Empty<decimal>();
            if (root.TryGetProperty("v", out var vProp))
            {
                vArray = ParseDecimalArray(vProp, "v");
            }

            Log($"[Wallex] 📊 Parsed arrays: t={tArray.Length}, o={oArray.Length}, h={hArray.Length}, l={lArray.Length}, c={cArray.Length}");

            var candles = new List<CandleDto>();
            for (int i = 0; i < tArray.Length; i++)
            {
                if (i >= oArray.Length || i >= hArray.Length ||
                    i >= lArray.Length || i >= cArray.Length)
                    break;

                var candle = new CandleDto
                {
                    OpenTime = DateTimeOffset.FromUnixTimeSeconds(tArray[i]).UtcDateTime,
                    Open = oArray[i],
                    High = hArray[i],
                    Low = lArray[i],
                    Close = cArray[i],
                    Volume = i < vArray.Length ? vArray[i] : 0
                };

                if (candle.OpenTime >= from && candle.OpenTime <= to)
                {
                    candles.Add(candle);
                }
            }

            Log($"[Wallex] 📊 Parsed {candles.Count} candles (after UTC range filter)");
            if (candles.Count > 0)
            {
                Log($"[Wallex]  First: {candles.First().OpenTime:yyyy-MM-dd HH:mm:ss}");
                Log($"[Wallex] 📊 Last: {candles.Last().OpenTime:yyyy-MM-dd HH:mm:ss}");
            }

            return candles.OrderBy(c => c.OpenTime).ToList();
        }
        catch (Exception ex)
        {
            Log($"[Wallex] ❌ Exception: {ex.Message}");
            Log($"[Wallex] ❌ Stack: {ex.StackTrace}");
            return new List<CandleDto>();
        }
    }

    private decimal[] ParseDecimalArray(JsonElement arrayProp, string propertyName)
    {
        var result = new List<decimal>();

        foreach (var element in arrayProp.EnumerateArray())
        {
            try
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var strValue = element.GetString();
                    if (decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        result.Add(decimalValue);
                    }
                    else
                    {
                        Log($"[Wallex] ⚠️ Failed to parse {propertyName} value: '{strValue}'");
                    }
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    result.Add(element.GetDecimal());
                }
                else
                {
                    Log($"[Wallex] ⚠️ Unexpected value kind for {propertyName}: {element.ValueKind}");
                }
            }
            catch (Exception ex)
            {
                Log($"[Wallex] ⚠️ Error parsing {propertyName}[{result.Count}]: {ex.Message}");
            }
        }

        return result.ToArray();
    }
}