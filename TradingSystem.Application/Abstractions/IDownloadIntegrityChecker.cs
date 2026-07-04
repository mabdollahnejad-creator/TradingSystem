using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace TradingSystem.Application.Abstractions;

public interface IDownloadIntegrityChecker
{
    Task<DownloadIntegrityResult> CheckFileIntegrityAsync(string filePath);
    bool IsZipFileValid(string filePath);
}

public class DownloadIntegrityResult
{
    public bool IsValid { get; set; }
    public bool FileExists { get; set; }
    public long FileSize { get; set; }
    public bool HasValidZipStructure { get; set; }
    public int CsvEntryCount { get; set; }
    public string? ErrorMessage { get; set; }
}