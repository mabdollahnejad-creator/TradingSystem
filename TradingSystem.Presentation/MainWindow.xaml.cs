using System;
using System.Windows;
using System.Windows.Controls;
using TradingSystem.Presentation.ViewModels;
using TradingSystem.Presentation.Views;

namespace TradingSystem.Presentation
{
    public partial class MainWindow : Window
    {
        private readonly ChartViewModel _chartViewModel;
        private readonly DataViewModel _dataViewModel;
        private bool _isSidebarExpanded = true;

        public MainWindow(ChartViewModel chartViewModel, DataViewModel dataViewModel)
        {
            InitializeComponent();
            _chartViewModel = chartViewModel;
            _dataViewModel = dataViewModel;

            if (System.Windows.Application.Current.Resources["MainViewModel"] is MainViewModel mainVm)
            {
                DataContext = mainVm;
            }

            _dataViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DataViewModel.StatusMessage))
                {
                    StatusBarText.Text = _dataViewModel.StatusMessage ?? "System Ready.";
                }
                else if (e.PropertyName == nameof(DataViewModel.CurrentFileName))
                {
                    // ✅ نمایش نام فایل در Status Bar
                    if (!string.IsNullOrEmpty(_dataViewModel.CurrentFileName))
                    {
                        StatusBarText.Text = $" {_dataViewModel.CurrentFileName} | {_dataViewModel.StatusMessage}";
                    }
                }
            };

            // ✅ پیغام قبل از بستن برنامه
            Closing += MainWindow_Closing;

            NavigateTo(typeof(ChartPanel), _chartViewModel);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_dataViewModel.IsBusy)
            {
                var result = MessageBox.Show(
                    "A download operation is in progress. Are you sure you want to close the application?\n\nThe current download will be cancelled.",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // ✅ Cancel operation before closing
                _dataViewModel.CancelOperationCommand.Execute(null);
            }
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;
            SidebarColumn.Width = new GridLength(_isSidebarExpanded ? 220 : 50);
        }

        private void NavigateTo(Type viewType, object viewModel)
        {
            try
            {
                var view = (UserControl)Activator.CreateInstance(viewType, viewModel)!;
                MainContent.Content = view;
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Navigation Error: {ex.Message}";
            }
        }

        private void NavChart_Click(object sender, RoutedEventArgs e) => NavigateTo(typeof(ChartPanel), _chartViewModel);
        private void NavData_Click(object sender, RoutedEventArgs e) => NavigateTo(typeof(DataPanel), _dataViewModel);
        private void NavBacktest_Click(object sender, RoutedEventArgs e) { }
        private void NavWatchlist_Click(object sender, RoutedEventArgs e) { }
    }
}