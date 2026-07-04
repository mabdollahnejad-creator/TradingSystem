using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.DTOs;
using TradingSystem.Application.Services;
using TradingSystem.Application.UseCases;
using TradingSystem.Domain.Entities;
using TradingSystem.Domain.Enums;

namespace TradingSystem.Presentation.ViewModels;

public partial class DataViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBinanceSyncService _binanceSyncService;
    private readonly ICryptoRepository _cryptoRepo;
    private readonly ICandleRepository _candleRepo;
    private readonly ICsvExportService _csvExporter;
    private readonly string _logFile;

    private CancellationTokenSource? _cts;
    private readonly List<DownloadStatus> _activeDownloads = new();
    private readonly object _lockObj = new();
    private DownloadReport? _currentReport;

    private static readonly Timeframe[] AllTimeframes = { Timeframe.H1, Timeframe.H4, Timeframe.D1 };

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private string loadingMessage = "Processing...";

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private bool isCancellable;

    [ObservableProperty]
    private string currentFileName = "";

    [ObservableProperty]
    private DateTime fromDate = new DateTime(2020, 1, 1);

    [ObservableProperty]
    private DateTime toDate = DateTime.Now.Date;

    [ObservableProperty]
    private int selectedTopCount = 100;

    [ObservableProperty]
    private string customSymbol = "";

    [ObservableProperty]
    private string reportMessage = "";

    public ObservableCollection<int> TopCounts { get; } = new() { 10, 50, 100, 200 };
    public ObservableCollection<DownloadStatus> ActiveDownloadsList { get; } = new();

    public DataViewModel(
        IServiceProvider serviceProvider,
        IBinanceSyncService binanceSyncService,
        ICryptoRepository cryptoRepo,
        ICandleRepository candleRepo,
        ICsvExportService csvExporter)
    {
        _serviceProvider = serviceProvider;
        _binanceSyncService = binanceSyncService;
        _cryptoRepo = cryptoRepo;
        _candleRepo = candleRepo;
        _csvExporter = csvExporter;
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_viewmodel_log.txt");

        Log("[DataViewModel] Constructor initialized (v3.6.0)");
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var fullMessage = $"[{timestamp}] {message}";
        Debug.WriteLine(fullMessage);
        try { File.AppendAllText(_logFile, fullMessage + Environment.NewLine); } catch { }
    }

    private void RunOnUi(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            action();
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(action);
    }

    #region Sync Metadata

    [RelayCommand]
    private async Task SyncMetadataAsync()
    {
        if (IsBusy) return;

        RunOnUi(() =>
        {
            IsBusy = true;
            IsCancellable = true;
            StatusMessage = "Syncing metadata from CoinGecko...";
            LoadingMessage = "Syncing metadata";
            ProgressValue = 0;
            CurrentFileName = "";
        });

        _cts = new CancellationTokenSource();

        try
        {
            Log($"[SyncMetadata] Starting with TopCount={SelectedTopCount}");

            using var scope = _serviceProvider.CreateScope();
            var syncUseCase = scope.ServiceProvider.GetRequiredService<SyncMetadataUseCase>();

            await syncUseCase.ExecuteAsync(SelectedTopCount, _cts.Token);

            RunOnUi(() => StatusMessage = $"Metadata sync completed ({SelectedTopCount} coins)");
        }
        catch (OperationCanceledException)
        {
            RunOnUi(() => StatusMessage = "Metadata sync cancelled");
        }
        catch (Exception ex)
        {
            RunOnUi(() => StatusMessage = $"Error: {ex.Message}");
            Log($"[SyncMetadata] Error: {ex.Message}");
        }
        finally
        {
            RunOnUi(() =>
            {
                IsBusy = false;
                IsCancellable = false;
                CurrentFileName = "";
            });
            _cts?.Dispose();
            _cts = null;
        }
    }

    #endregion

    #region Download Top N (Binance Only)

    [RelayCommand]
    private async Task DownloadTopAsync()
    {
        if (IsBusy) return;

        RunOnUi(() =>
        {
            IsBusy = true;
            IsCancellable = true;
            StatusMessage = "Fetching top symbols from database...";
            LoadingMessage = $"Downloading Top {SelectedTopCount} from Binance";
            ProgressValue = 0;
            CurrentFileName = "";
            ReportMessage = "";
            ActiveDownloadsList.Clear();
        });

        _cts = new CancellationTokenSource();
        _currentReport = new DownloadReport
        {
            StartTime = DateTime.Now,
            TotalTasks = SelectedTopCount * AllTimeframes.Length
        };

        try
        {
            var symbols = await _cryptoRepo.GetTopActiveAsync(SelectedTopCount);
            var symbolList = symbols.Select(c => c.Symbol).ToList();

            Log($"[DownloadTop] Found {symbolList.Count} symbols in DB");
            Log($"[DownloadTop] Date range: {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}");
            Log($"[DownloadTop] Timeframes: {string.Join(", ", AllTimeframes)}");

            if (symbolList.Count == 0)
            {
                RunOnUi(() => StatusMessage = "No symbols found. Please sync metadata first.");
                return;
            }

            var tasks = BuildBinanceTasks(symbolList);
            _currentReport.TotalTasks = tasks.Count;
            Log($"[DownloadTop] Queue populated with {tasks.Count} tasks");

            await ProcessBinanceTasksAsync(tasks, FromDate, ToDate, 7, _cts.Token);

            _currentReport.EndTime = DateTime.Now;
            RunOnUi(() =>
            {
                StatusMessage = $"Download completed. {_currentReport.CompletedTasks}/{_currentReport.TotalTasks} tasks.";
                ReportMessage = _currentReport.Summary;
            });
            Log($"[DownloadTop] Report:\n{_currentReport.Summary}");
        }
        catch (OperationCanceledException)
        {
            RunOnUi(() => StatusMessage = "Download cancelled");
        }
        catch (Exception ex)
        {
            RunOnUi(() => StatusMessage = $"Error: {ex.Message}");
            Log($"[DownloadTop] Error: {ex.Message}");
            Log($"[DownloadTop] StackTrace: {ex.StackTrace}");
        }
        finally
        {
            RunOnUi(() =>
            {
                IsBusy = false;
                IsCancellable = false;
                CurrentFileName = "";
            });
            _cts?.Dispose();
            _cts = null;
        }
    }

    #endregion

    #region Download Custom Symbol (Binance Only)

    [RelayCommand]
    private async Task DownloadCustomAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(CustomSymbol))
        {
            RunOnUi(() => StatusMessage = "Please enter a symbol");
            return;
        }

        var symbol = CustomSymbol.Trim().ToUpperInvariant();

        RunOnUi(() =>
        {
            IsBusy = true;
            IsCancellable = true;
            StatusMessage = $"Downloading {symbol} from Binance...";
            LoadingMessage = $"Downloading {symbol} from Binance";
            ProgressValue = 0;
            CurrentFileName = "";
            ReportMessage = "";
            ActiveDownloadsList.Clear();
        });

        _cts = new CancellationTokenSource();
        _currentReport = new DownloadReport
        {
            StartTime = DateTime.Now,
            TotalTasks = AllTimeframes.Length
        };

        try
        {
            Log($"[DownloadCustom] Symbol: {symbol}");
            Log($"[DownloadCustom] Date range: {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}");

            var tasks = BuildBinanceTasks(new List<string> { symbol });
            _currentReport.TotalTasks = tasks.Count;
            Log($"[DownloadCustom] Queue populated with {tasks.Count} tasks");

            await ProcessBinanceTasksAsync(tasks, FromDate, ToDate, 7, _cts.Token);

            _currentReport.EndTime = DateTime.Now;
            RunOnUi(() =>
            {
                StatusMessage = $"Download completed for {symbol}";
                ReportMessage = _currentReport.Summary;
            });
        }
        catch (OperationCanceledException)
        {
            RunOnUi(() => StatusMessage = "Download cancelled");
        }
        catch (Exception ex)
        {
            RunOnUi(() => StatusMessage = $"Error: {ex.Message}");
            Log($"[DownloadCustom] Error: {ex.Message}");
        }
        finally
        {
            RunOnUi(() =>
            {
                IsBusy = false;
                IsCancellable = false;
                CurrentFileName = "";
            });
            _cts?.Dispose();
            _cts = null;
        }
    }

    #endregion

    #region Task Building & Processing (Binance Only)

    private List<DownloadTask> BuildBinanceTasks(List<string> symbols)
    {
        var tasks = new List<DownloadTask>();

        foreach (var symbol in symbols)
        {
            foreach (var tf in AllTimeframes)
            {
                tasks.Add(new DownloadTask
                {
                    Symbol = symbol,
                    Exchange = "Binance",
                    Source = DataSource.Global,
                    Timeframe = tf
                });
            }
        }

        return tasks;
    }

    private async Task ProcessBinanceTasksAsync(
        List<DownloadTask> tasks,
        DateTime from,
        DateTime to,
        int maxWorkers,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<DownloadTask>(tasks);
        var workers = new List<Task>();
        var step = 0;
        var totalTasks = tasks.Count;
        var completedTasks = 0;

        for (int i = 0; i < maxWorkers; i++)
        {
            var workerId = i + 1;
            workers.Add(Task.Run(async () =>
            {
                Log($"[BinanceWorker {workerId}] Started");

                while (true)
                {
                    DownloadTask? task = null;

                    lock (queue)
                    {
                        if (queue.Count > 0)
                        {
                            task = queue.Dequeue();
                            step++;
                        }
                    }

                    if (task == null) break;

                    cancellationToken.ThrowIfCancellationRequested();

                    var taskName = $"{task.Symbol}_Binance_{task.Timeframe}";
                    Log($"[BinanceWorker {workerId}] Processing: {taskName} (Step {step}/{totalTasks})");

                    RunOnUi(() => CurrentFileName = taskName);

                    var status = new DownloadStatus
                    {
                        Symbol = task.Symbol,
                        Exchange = task.Exchange,
                        Timeframe = task.Timeframe.ToString(),
                        Status = "Downloading..."
                    };

                    lock (_lockObj)
                    {
                        _activeDownloads.Add(status);
                        RunOnUi(() => ActiveDownloadsList.Add(status));
                    }

                    try
                    {
                        var timeframes = new List<Timeframe> { task.Timeframe };

                        var candles = await _binanceSyncService.SyncAllTimeframesAsync(
                            task.Symbol, from, to, timeframes, cancellationToken,
                            (current, total, st) =>
                            {
                                RunOnUi(() =>
                                {
                                    status.Status = st;
                                    status.LastProcessedDate = st;
                                });
                            });

                        Log($"[BinanceWorker {workerId}] {taskName}: Downloaded {candles.Count} candles");

                        // ✅ Auto-Parse: ذخیره در DB و CSV
                        if (candles.Count > 0)
                        {
                            var crypto = await _cryptoRepo.GetBySymbolAsync(task.Symbol);
                            if (crypto == null)
                            {
                                crypto = await _cryptoRepo.AddAsync(new Cryptocurrency
                                {
                                    Symbol = task.Symbol,
                                    Name = task.Symbol,
                                    IsActive = true
                                });
                                await _cryptoRepo.SaveChangesAsync();
                            }

                            var existingTimes = await _candleRepo.GetExistingOpenTimesAsync(
                                crypto.Id, task.Source, task.Exchange, task.Timeframe, from, to);
                            var existingSet = new HashSet<DateTime>(existingTimes);
                            var newCandles = candles.Where(c => !existingSet.Contains(c.OpenTime)).ToList();

                            if (newCandles.Count > 0)
                            {
                                await _candleRepo.AddRangeAsync(newCandles);
                                await _candleRepo.SaveChangesAsync();

                                RunOnUi(() => status.Status = $"✅ {newCandles.Count} saved");
                                Log($"[BinanceWorker {workerId}] {taskName}: Saved {newCandles.Count} to DB");

                                // ✅ Export به CSV (Sync)
                                try
                                {
                                    await _csvExporter.ExportCandlesAsync(
                                        "Data", task.Symbol, task.Timeframe, task.Source, task.Exchange, newCandles);
                                    Log($"[BinanceWorker {workerId}] {taskName}: Exported to CSV");
                                }
                                catch (Exception csvEx)
                                {
                                    Log($"[BinanceWorker {workerId}] {taskName}: CSV export error: {csvEx.Message}");
                                }

                                if (_currentReport != null)
                                {
                                    lock (_currentReport)
                                    {
                                        _currentReport.Symbols.Add(new SymbolDownloadInfo
                                        {
                                            Symbol = task.Symbol,
                                            Exchange = task.Exchange,
                                            FromDate = from,
                                            ToDate = to,
                                            TotalCandles = candles.Count,
                                            NewCandles = newCandles.Count
                                        });
                                    }
                                }
                            }
                            else
                            {
                                RunOnUi(() => status.Status = "⏭️ No new");
                            }
                        }
                        else
                        {
                            RunOnUi(() => status.Status = "⚠️ No data");
                        }

                        Log($"[BinanceWorker {workerId}] Completed: {taskName}");
                    }
                    catch (OperationCanceledException)
                    {
                        Log($"[BinanceWorker {workerId}] Cancelled: {taskName}");
                        RunOnUi(() => status.Status = "❌ Cancelled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log($"[BinanceWorker {workerId}] Error in {taskName}: {ex.Message}");
                        RunOnUi(() => status.Status = $"❌ {ex.Message}");

                        if (_currentReport != null)
                        {
                            lock (_currentReport)
                            {
                                _currentReport.FailedTasks++;
                            }
                        }
                    }
                    finally
                    {
                        // ✅ اصلاح: حذف از لیست با Dispatcher
                        RunOnUi(() =>
                        {
                            _activeDownloads.Remove(status);
                            ActiveDownloadsList.Remove(status);
                        });

                        var completed = Interlocked.Increment(ref completedTasks);

                        if (_currentReport != null)
                        {
                            lock (_currentReport)
                            {
                                _currentReport.CompletedTasks++;
                            }
                        }

                        // ✅ Progress تجمیعی
                        RunOnUi(() =>
                        {
                            ProgressValue = (double)completed / totalTasks * 100;
                            StatusMessage = $"Progress: {completed}/{totalTasks} tasks ({ProgressValue:F1}%)";
                        });
                    }

                }

                Log($"[BinanceWorker {workerId}] Finished");
            }));
        }

        await Task.WhenAll(workers);
    }

    #endregion

    #region Export/Import

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (IsBusy) return;
        RunOnUi(() => StatusMessage = "Exporting to CSV...");
        await _csvExporter.ExportAllToPathAsync("Data");
        RunOnUi(() => StatusMessage = "Export completed");
    }

    [RelayCommand]
    private async Task ImportAllAsync()
    {
        if (IsBusy) return;
        RunOnUi(() => StatusMessage = "Importing from CSV...");
        var count = await _csvExporter.ImportAllFromCsvAsync("Data");
        RunOnUi(() => StatusMessage = $"Imported {count} records");
    }

    #endregion

    #region Cancel

    [RelayCommand]
    private void CancelOperation()
    {
        Log("[CancelOperation] Cancel requested");
        _cts?.Cancel();
        RunOnUi(() => StatusMessage = "Cancelling...");
    }

    #endregion
}

#region Helper Classes

public class DownloadTask
{
    public string Symbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public DataSource Source { get; set; }
    public Timeframe Timeframe { get; set; }
}

public partial class DownloadStatus : ObservableObject
{
    [ObservableProperty]
    private string symbol = "";

    [ObservableProperty]
    private string exchange = "";

    [ObservableProperty]
    private string timeframe = "";

    [ObservableProperty]
    private string status = "";

    [ObservableProperty]
    private string lastProcessedDate = "";
}

#endregion