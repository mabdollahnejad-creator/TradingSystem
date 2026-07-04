using System.IO.Compression;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;

namespace TradingSystem.Infrastructure.Services;

public class DownloadIntegrityChecker : IDownloadIntegrityChecker
{
    private const int MinZipSize = 1024;
    private readonly ITradingLogger _logger;

    public DownloadIntegrityChecker(ITradingLogger logger)
    {
        _logger = logger;
    }

    public async Task<DownloadIntegrityResult> CheckFileIntegrityAsync(string filePath)
    {
        var result = new DownloadIntegrityResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File does not exist";
                return result;
            }

            result.FileExists = true;

            var fileInfo = new FileInfo(filePath);
            result.FileSize = fileInfo.Length;

            if (result.FileSize < MinZipSize)
            {
                result.ErrorMessage = $"File too small: {result.FileSize} bytes (minimum: {MinZipSize})";
                return result;
            }

            using var fileStream = File.OpenRead(filePath);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            result.HasValidZipStructure = true;

            var csvEntries = zipArchive.Entries
                .Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();
            result.CsvEntryCount = csvEntries.Count;

            if (result.CsvEntryCount == 0)
            {
                result.ErrorMessage = "No CSV file found in ZIP";
                return result;
            }

            result.IsValid = true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking file integrity: {FilePath} - {Error}", filePath, ex.Message);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public bool IsZipFileValid(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < MinZipSize) return false;

            using var fileStream = File.OpenRead(filePath);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            return zipArchive.Entries.Any(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}