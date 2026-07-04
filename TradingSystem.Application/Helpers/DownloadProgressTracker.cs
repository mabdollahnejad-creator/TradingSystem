using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace TradingSystem.Application.Helpers;

public class DownloadProgressTracker
{
    private readonly int _totalSteps;
    private readonly int _daysPerStep;
    private readonly DateTime _startTime;

    private int _completedSteps;
    private int _completedDays;

    private readonly ConcurrentDictionary<string, string> _activeDownloads = new();

    public event Action<int, int, string, TimeSpan>? ProgressChanged;

    public DownloadProgressTracker(int totalSteps, int daysPerStep)
    {
        _totalSteps = totalSteps;
        _daysPerStep = daysPerStep;
        _startTime = DateTime.Now;
    }

    public void StartStep(string stepKey, string stepDescription)
    {
        _activeDownloads[stepKey] = stepDescription;
    }

    public void CompleteStep(string stepKey)
    {
        _activeDownloads.TryRemove(stepKey, out _);
        Interlocked.Increment(ref _completedSteps);
    }

    public void ReportDayProgress(string stepKey, int day, int totalDays, string date)
    {
        var completedDays = Interlocked.Increment(ref _completedDays);
        var totalExpectedDays = _totalSteps * _daysPerStep;
        var overallProgress = (double)completedDays / totalExpectedDays * 100;

        // ✅ اصلاح: جلوگیری از تقسیم بر صفر
        if (overallProgress <= 0) return;

        var elapsed = DateTime.Now - _startTime;
        var estimatedTotal = elapsed / (overallProgress / 100);
        var remaining = estimatedTotal - elapsed;

        ProgressChanged?.Invoke(
            (int)overallProgress,
            completedDays,
            $"{completedDays}/{totalExpectedDays} days",
            remaining
        );
    }
    public int CompletedSteps => _completedSteps;
    public int CompletedDays => _completedDays;
    public TimeSpan Elapsed => DateTime.Now - _startTime;
}