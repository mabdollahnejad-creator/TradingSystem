namespace TradingSystem.Application.Abstractions;

public interface IDownloadReporter
{
    void AddGapReport(BinanceGapReport report);
    void AddDownloadStats(string symbol, string exchange, DateTime from, DateTime to, int totalCandles, int newCandles);
    void AddError(string symbol, string exchange, string error);
    Task SaveReportToFileAsync(string filePath);
    string GenerateSummary();
}

public class SymbolDownloadInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalCandles { get; set; }
    public int NewCandles { get; set; }
}