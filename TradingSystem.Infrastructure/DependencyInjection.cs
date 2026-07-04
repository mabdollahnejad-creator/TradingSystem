using System;
using Microsoft.Extensions.DependencyInjection;
using TradingSystem.Application.Abstractions;
using TradingSystem.Application.Services;
using TradingSystem.Infrastructure.MarketData;
using TradingSystem.Infrastructure.Repositories;
using TradingSystem.Infrastructure.Services;

namespace TradingSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {

        // CoinGecko Metadata
        services.AddHttpClient<ICoinGeckoMetadataService, CoinGeckoMetadataService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Binance Data Vision
        services.AddHttpClient<IBinanceDataVisionService, BinanceDataVisionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // اضافه کردن به AddInfrastructure
        services.AddHttpClient<IBinanceSyncService, BinanceSyncService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Binance Symbol Service
        services.AddHttpClient<IBinanceSymbolService, BinanceSymbolService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Binance Symbol Mapper
        services.AddSingleton<IBinanceSymbolMapper, BinanceSymbolMapper>();

        services.AddScoped<IBinanceSyncService, BinanceSyncService>();

        // Logger
        services.AddSingleton<ITradingLogger, SerilogTradingLogger>();

        // Integrity Checker
        services.AddScoped<IDownloadIntegrityChecker, DownloadIntegrityChecker>();

        // Gap Analyzer
        services.AddScoped<IBinanceGapAnalyzer, BinanceGapAnalyzer>();

        // Download Reporter
        services.AddSingleton<IDownloadReporter, DownloadReporter>();

        // Binance File List Service
        services.AddHttpClient<IBinanceFileListService, BinanceFileListService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });


        // Iranian Exchange Sync Service
        services.AddScoped<IIranianExchangeSyncService, IranianExchangeSyncService>();

        // Nobitex Symbol Service
        services.AddHttpClient<INobitexSymbolService, NobitexSymbolService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Nobitex Exchange Service
        services.AddHttpClient<INobitexExchangeService, NobitexService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Wallex Symbol Service
        services.AddHttpClient<IWallexSymbolService, WallexSymbolService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Wallex Exchange Service
        services.AddHttpClient<IWallexExchangeService, WallexService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // Repositories
        services.AddScoped<ICryptoRepository, CryptoRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();

        // CSV Exporter
        services.AddScoped<ICsvExportService, CsvExportService>();

        return services;
    }
}