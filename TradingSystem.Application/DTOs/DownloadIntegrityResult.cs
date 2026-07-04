namespace TradingSystem.Application.DTOs;

public class DownloadIntegrityResult
{
    public bool IsValid { get; set; }
    public bool FileExists { get; set; }
    public long FileSize { get; set; }
    public bool HasValidZipStructure { get; set; }
    public int CsvEntryCount { get; set; }
    public string? ErrorMessage { get; set; }
}