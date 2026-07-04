using System.Text;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;

namespace TradingSystem.Infrastructure.Services;

public class DownloadReporter : IDownloadReporter
{
    private readonly List<BinanceGapReport> _gapReports = new();
    private readonly List<SymbolDownloadInfo> _downloadStats = new();
    private readonly List<string> _errors = new();
    private readonly ITradingLogger _logger;
    private readonly object _lock = new();

    public DownloadReporter(ITradingLogger logger)
    {
        _logger = logger;
    }

    public void AddGapReport(BinanceGapReport report)
    {
        lock (_lock)
        {
            _gapReports.Add(report);
        }
    }

    public void AddDownloadStats(string symbol, string exchange, DateTime from, DateTime to, int totalCandles, int newCandles)
    {
        lock (_lock)
        {
            _downloadStats.Add(new SymbolDownloadInfo
            {
                Symbol = symbol,
                Exchange = exchange,
                FromDate = from,
                ToDate = to,
                TotalCandles = totalCandles,
                NewCandles = newCandles
            });
        }
    }

    public void AddError(string symbol, string exchange, string error)
    {
        lock (_lock)
        {
            _errors.Add($"[{symbol}/{exchange}] {error}");
        }
    }

    public async Task SaveReportToFileAsync(string filePath)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(GenerateSummary());

            foreach (var gapReport in _gapReports)
            {
                sb.AppendLine(gapReport.GenerateTextReport());
            }

            if (_errors.Any())
            {
                sb.AppendLine("\n═══════════════════════════════════════════════════════");
                sb.AppendLine("Errors:");
                sb.AppendLine("═══════════════════════════════════════════════════════");
                foreach (var error in _errors)
                {
                    sb.AppendLine(error);
                }
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            _logger.LogInformation("Report saved to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error saving report to {FilePath}: {Error}", filePath, ex.Message);
        }
    }

    public string GenerateSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("Download Summary Report");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine($"Total Symbols Downloaded: {_downloadStats.Count}");
        sb.AppendLine($"Total Candles: {_downloadStats.Sum(s => s.TotalCandles)}");
        sb.AppendLine($"New Candles: {_downloadStats.Sum(s => s.NewCandles)}");
        sb.AppendLine($"Total Errors: {_errors.Count}");
        sb.AppendLine($"Gap Reports: {_gapReports.Count}");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        return sb.ToString();
    }
}