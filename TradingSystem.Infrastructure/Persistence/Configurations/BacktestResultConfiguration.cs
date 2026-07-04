using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class BacktestResultConfiguration
    : IEntityTypeConfiguration<BacktestResult>
{
    public void Configure(
        EntityTypeBuilder<BacktestResult> builder)
    {
        builder.ToTable("BacktestResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NetProfit)
            .HasPrecision(18, 4);

        builder.Property(x => x.WinRate)
            .HasPrecision(10, 4);

        builder.Property(x => x.ProfitFactor)
            .HasPrecision(18, 4);

        builder.Property(x => x.MaxDrawdown)
            .HasPrecision(18, 4);

        builder.HasOne(x => x.Cryptocurrency)
            .WithMany()
            .HasForeignKey(x => x.CryptocurrencyId);
    }
}