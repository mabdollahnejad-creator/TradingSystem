namespace TradingSystem.Application.DTOs;

public class DownloadReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public List<SymbolDownloadInfo> Symbols { get; set; } = new();

    public string Summary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("Download Summary Report");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"Total Symbols: {Symbols.Count}");
            sb.AppendLine($"Total Tasks: {TotalTasks}");
            sb.AppendLine($"Completed: {CompletedTasks}");
            sb.AppendLine($"Failed: {FailedTasks}");
            sb.AppendLine($"Duration: {(EndTime - StartTime).TotalMinutes:F1} minutes");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            return sb.ToString();
        }
    }
}