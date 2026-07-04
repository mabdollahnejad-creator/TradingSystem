using Microsoft.EntityFrameworkCore;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Candle> Candles { get; set; } = null!;
    public DbSet<Cryptocurrency> Cryptocurrencies { get; set; } = null!;
    public DbSet<Watchlist> Watchlists { get; set; } = null!;
    public DbSet<WatchlistCryptocurrency> WatchlistCryptocurrencies { get; set; } = null!;
    public DbSet<StrategyDefinition> StrategyDefinitions { get; set; } = null!;
    public DbSet<BacktestRun> BacktestRuns { get; set; } = null!;
    public DbSet<BacktestResult> BacktestResults { get; set; } = null!;
    public DbSet<Trade> Trades { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}