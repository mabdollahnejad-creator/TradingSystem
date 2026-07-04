using System.Globalization;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;

namespace TradingSystem.Infrastructure.Services;

public class BinanceGapAnalyzer : IBinanceGapAnalyzer
{
    private readonly IBinanceFileListService _fileListService;
    private readonly IDownloadIntegrityChecker _integrityChecker;
    private readonly ITradingLogger _logger;

    public BinanceGapAnalyzer(
        IBinanceFileListService fileListService,
        IDownloadIntegrityChecker integrityChecker,
        ITradingLogger logger)
    {
        _fileListService = fileListService;
        _integrityChecker = integrityChecker;
        _logger = logger;
    }

    public async Task<BinanceGapReport> AnalyzeGapsAsync(
        string binanceSymbol,
        string tfFolder,
        DateTime from,
        DateTime to,
        string cachePath)
    {
        _logger.LogInformation("Starting gap analysis for {Symbol}/{Timeframe} from {From} to {To}",
            binanceSymbol, tfFolder, from, to);

        var report = new BinanceGapReport
        {
            Symbol = binanceSymbol,
            Timeframe = tfFolder,
            From = from,
            To = to
        };

        try
        {
            var availableDates = await _fileListService.GetAvailableDatesAsync(binanceSymbol, tfFolder);
            var availableDateStrings = availableDates
                .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToHashSet();

            var expectedDates = new List<DateTime>();
            var currentDate = from.Date;
            var endDate = to.Date.AddDays(1);

            while (currentDate < endDate)
            {
                expectedDates.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }

            report.TotalExpectedDays = expectedDates.Count;

            foreach (var date in expectedDates)
            {
                var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var cacheFile = Path.Combine(cachePath, $"{binanceSymbol}-{tfFolder}-{dateStr}.zip");

                if (!availableDateStrings.Contains(dateStr))
                {
                    report.MissingDates.Add(date);
                }
                else if (File.Exists(cacheFile))
                {
                    var integrity = await _integrityChecker.CheckFileIntegrityAsync(cacheFile);
                    if (!integrity.IsValid)
                    {
                        report.IncompleteFiles.Add(cacheFile);
                        _logger.LogWarning("Incomplete file detected: {File} - {Error}", cacheFile, integrity.ErrorMessage);
                    }
                }
                else
                {
                    report.MissingDates.Add(date);
                }
            }

            report.AvailableDays = report.TotalExpectedDays - report.MissingDates.Count;
            report.MissingDays = report.MissingDates.Count;
            report.IncompleteFilesCount = report.IncompleteFiles.Count;
            report.CompletenessPercentage = report.TotalExpectedDays > 0
                ? (double)report.AvailableDays / report.TotalExpectedDays * 100
                : 0;

            _logger.LogInformation("Gap analysis completed. Completeness: {Completeness:F2}%", report.CompletenessPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error analyzing gaps for {Symbol}/{Timeframe}: {Error}", binanceSymbol, tfFolder, ex.Message);
        }

        return report;
    }
}