using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingSystem.Application.DTOs;
using TradingSystem.Application.UseCases;

namespace TradingSystem.Presentation.ViewModels;

public partial class ChartViewModel : ObservableObject
{
    private readonly GetChartCandlesUseCase _useCase;
    public ObservableCollection<CandleDto> Candles { get; } = new ObservableCollection<CandleDto>();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public IAsyncRelayCommand LoadCommand { get; }

    public ChartViewModel(GetChartCandlesUseCase useCase)
    {
        _useCase = useCase;
        LoadCommand = new AsyncRelayCommand(async () => await LoadAsync("bitcoin", "1h", 100));
    }

    public async Task LoadAsync(string symbol = "bitcoin", string timeframe = "1h", int limit = 100)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // ✅ اصلاح نام متد به ExecuteAsync
            var data = await _useCase.ExecuteAsync(symbol, timeframe, limit);

            Candles.Clear();
            foreach (var c in data) Candles.Add(c);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}