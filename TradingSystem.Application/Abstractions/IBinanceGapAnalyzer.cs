namespace TradingSystem.Application.Abstractions;

public interface IBinanceGapAnalyzer
{
    Task<BinanceGapReport> AnalyzeGapsAsync(
        string binanceSymbol,
        string tfFolder,
        DateTime from,
        DateTime to,
        string cachePath);
}

public class BinanceGapReport
{
    public string Symbol { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalExpectedDays { get; set; }
    public int AvailableDays { get; set; }
    public int MissingDays { get; set; }
    public int IncompleteFilesCount { get; set; }
    public List<DateTime> MissingDates { get; set; } = new();
    public List<string> IncompleteFiles { get; set; } = new();
    public double CompletenessPercentage { get; set; }

    public string GenerateTextReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"═══════════════════════════════════════════════════════");
        sb.AppendLine($"Gap Analysis Report: {Symbol} | {Timeframe}");
        sb.AppendLine($"═══════════════════════════════════════════════════════");
        sb.AppendLine($"Date Range: {From:yyyy-MM-dd} to {To:yyyy-MM-dd}");
        sb.AppendLine($"Total Expected Days: {TotalExpectedDays}");
        sb.AppendLine($"Available Days: {AvailableDays}");
        sb.AppendLine($"Missing Days: {MissingDays}");
        sb.AppendLine($"Incomplete Files: {IncompleteFilesCount}");
        sb.AppendLine($"Completeness: {CompletenessPercentage:F2}%");

        if (MissingDates.Any())
        {
            sb.AppendLine($"\nMissing Dates:");
            foreach (var date in MissingDates.Take(10))
            {
                sb.AppendLine($"  - {date:yyyy-MM-dd}");
            }
            if (MissingDates.Count > 10)
                sb.AppendLine($"  ... and {MissingDates.Count - 10} more");
        }

        if (IncompleteFiles.Any())
        {
            sb.AppendLine($"\nIncomplete Files:");
            foreach (var file in IncompleteFiles.Take(10))
            {
                sb.AppendLine($"  - {file}");
            }
            if (IncompleteFiles.Count > 10)
                sb.AppendLine($"  ... and {IncompleteFiles.Count - 10} more");
        }

        sb.AppendLine($"═══════════════════════════════════════════════════════");
        return sb.ToString();
    }
}