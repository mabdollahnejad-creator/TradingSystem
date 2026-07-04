using CommunityToolkit.Mvvm.ComponentModel;

namespace TradingSystem.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "سیستم آماده است.";

    [ObservableProperty]
    private bool _isBusy = false;

    // منطق پیچیده و شکسته دانلود فعلاً حذف شد تا پروژه کامپایل شود.
    // در مرحله ۴ با استفاده از UseCaseهای صحیح بازنویسی می‌شود.

    [ObservableProperty] private DataViewModel _dataViewModel;

    public MainViewModel(DataViewModel dataViewModel)
    {
        _dataViewModel = dataViewModel;
    }

}