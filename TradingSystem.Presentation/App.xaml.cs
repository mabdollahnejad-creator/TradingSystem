using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TradingSystem.Application.UseCases;
using TradingSystem.Infrastructure;
using TradingSystem.Infrastructure.Persistence;
using TradingSystem.Presentation.ViewModels;

namespace TradingSystem.Presentation
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            // ✅ Global exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Critical Error: {ex?.Message}\n\nStackTrace: {ex?.StackTrace}",
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"UI Error: {args.Exception.Message}\n\nStackTrace: {args.Exception.StackTrace}",
                    "UI Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                MessageBox.Show($"Task Error: {args.Exception.Message}",
                    "Task Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.SetObserved();
            };

            // ✅ تنظیم culture
            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // ✅ راه‌اندازی Serilog
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "trading-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logPath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            base.OnStartup(e);

            var services = new ServiceCollection();

            // ✅ ثبت Serilog به عنوان ILogger provider
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: true);
            });

            // ۱. ثبت زیرساخت
            services.AddInfrastructure();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=trading.db"));

            // ۲. ثبت UseCaseها
            services.AddTransient<GetChartCandlesUseCase>();
            services.AddTransient<SyncCandlesUseCase>();
            services.AddTransient<SyncMetadataUseCase>();

            // ۳. ثبت ViewModelها
            services.AddTransient<ChartViewModel>();
            services.AddTransient<DataViewModel>();

            // ۴. ثبت پنجره اصلی
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // ۵. ایجاد خودکار دیتابیس
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated();
            }

            // ۶. نمایش پنجره اصلی
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}