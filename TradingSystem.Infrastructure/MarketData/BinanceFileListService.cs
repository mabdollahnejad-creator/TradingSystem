using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TradingSystem.Application.Abstractions;

namespace TradingSystem.Infrastructure.MarketData;

public class BinanceFileListService : IBinanceFileListService
{
    private readonly HttpClient _http;
    private readonly string _logFile;
    private readonly Dictionary<string, List<DateTime>> _cache = new();
    private readonly Dictionary<string, DateTime?> _firstDateCache = new();

    public BinanceFileListService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(60);
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "binance_filelist_log.txt");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync(string binanceSymbol, string tfFolder)
    {
        var cacheKey = $"{binanceSymbol}_{tfFolder}";

        if (_cache.TryGetValue(cacheKey, out var cachedDates))
        {
            Log($"[BinanceFileList] Cache hit for {cacheKey}: {cachedDates.Count} dates");
            return cachedDates;
        }

        var dates = new List<DateTime>();
        string? marker = null;
        int pageCount = 0;
        int totalZipFiles = 0;
        int totalChecksumSkipped = 0;

        try
        {
            do
            {
                pageCount++;
                var url = $"https://s3-ap-northeast-1.amazonaws.com/data.binance.vision?delimiter=/&prefix=data/spot/daily/klines/{binanceSymbol}/{tfFolder}/&max-keys=1000";

                if (!string.IsNullOrEmpty(marker))
                {
                    url += $"&marker={Uri.EscapeDataString(marker)}";
                }

                Log($"[BinanceFileList] Page {pageCount}: {url}");

                var response = await _http.GetAsync(url);
                Log($"[BinanceFileList] HTTP Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    Log($"[BinanceFileList] HTTP Error: {response.StatusCode}");
                    break;
                }

                var xml = await response.Content.ReadAsStringAsync();
                Log($"[BinanceFileList] XML length: {xml.Length}");

                // ✅ الگوی بهبود یافته: گروه سوم مشخص می‌کند .zip است یا .zip.CHECKSUM
                var pattern = $@"<Key>([^<]*?{Regex.Escape(binanceSymbol)}-{Regex.Escape(tfFolder)}-(\d{{4}}-\d{{2}}-\d{{2}})(\.zip(?:\.CHECKSUM)?))</Key>";
                var matches = Regex.Matches(xml, pattern, RegexOptions.IgnoreCase);

                int pageZipCount = 0;
                int pageChecksumCount = 0;

                foreach (Match match in matches)
                {
                    var dateStr = match.Groups[2].Value;
                    var extension = match.Groups[3].Value;

                    // ✅ فقط فایل‌های .zip واقعی (نه .CHECKSUM)
                    if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // ✅ اصلاح حیاتی: استفاده از InvariantCulture برای parse تاریخ میلادی
                        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        {
                            dates.Add(date);
                            pageZipCount++;
                        }
                        else
                        {
                            Log($"[BinanceFileList] ⚠️ Failed to parse date: {dateStr}");
                        }
                    }
                    else
                    {
                        pageChecksumCount++;
                    }
                }

                totalZipFiles += pageZipCount;
                totalChecksumSkipped += pageChecksumCount;

                Log($"[BinanceFileList] Page {pageCount}: {pageZipCount} .zip files, {pageChecksumCount} .CHECKSUM skipped");

                // ✅ نمایش نمونه تاریخ‌ها در صفحه اول
                if (pageZipCount > 0 && pageCount == 1)
                {
                    var sampleDates = dates.Take(3).Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    Log($"[BinanceFileList] Sample dates: {string.Join(", ", sampleDates)}");
                }

                // بررسی IsTruncated با Regex
                var isTruncatedMatch = Regex.Match(xml, @"<IsTruncated>(true|false)</IsTruncated>", RegexOptions.IgnoreCase);
                var isTruncated = isTruncatedMatch.Success &&
                                  isTruncatedMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (isTruncated)
                {
                    var nextMarkerMatch = Regex.Match(xml, @"<NextMarker>([^<]+)</NextMarker>");
                    if (nextMarkerMatch.Success)
                    {
                        marker = nextMarkerMatch.Groups[1].Value;
                        Log($"[BinanceFileList] NextMarker: {marker}");
                    }
                    else
                    {
                        var lastKeyMatch = Regex.Matches(xml, @"<Key>([^<]+)</Key>").Cast<Match>().LastOrDefault();
                        if (lastKeyMatch != null)
                        {
                            marker = lastKeyMatch.Groups[1].Value;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    Log($"[BinanceFileList] IsTruncated=false, end of list");
                    break;
                }

                if (pageCount > 50)
                {
                    Log($"[BinanceFileList] Too many pages, stopping");
                    break;
                }

            } while (!string.IsNullOrEmpty(marker));

            dates = dates.Distinct().OrderBy(d => d).ToList();
            _cache[cacheKey] = dates;

            Log($"[BinanceFileList] === Summary for {binanceSymbol}/{tfFolder} ===");
            Log($"[BinanceFileList] Total .zip files: {totalZipFiles}");
            Log($"[BinanceFileList] Total .CHECKSUM skipped: {totalChecksumSkipped}");
            Log($"[BinanceFileList] Unique dates: {dates.Count}");

            if (dates.Count > 0)
            {
                _firstDateCache[cacheKey] = dates.First();
                Log($"[BinanceFileList] Date range: {dates.First():yyyy-MM-dd} to {dates.Last():yyyy-MM-dd}");
            }
            else
            {
                Log($"[BinanceFileList] No .zip files found");
            }
        }
        catch (Exception ex)
        {
            Log($"[BinanceFileList] Error: {ex.Message}");
            Log($"[BinanceFileList] StackTrace: {ex.StackTrace}");
            _cache[cacheKey] = dates;
        }

        return dates;
    }

    public async Task<DateTime?> GetFirstAvailableDateAsync(string binanceSymbol, string tfFolder)
    {
        var dates = await GetAvailableDatesAsync(binanceSymbol, tfFolder);
        return dates.FirstOrDefault();
    }

    public async Task<bool> SymbolExistsAsync(string binanceSymbol, string tfFolder)
    {
        var dates = await GetAvailableDatesAsync(binanceSymbol, tfFolder);
        return dates.Count > 0;
    }
}