using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class TradeConfiguration
    : IEntityTypeConfiguration<Trade>
{
    public void Configure(
        EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntryPrice)
            .HasPrecision(18, 8);

        builder.Property(x => x.ExitPrice)
            .HasPrecision(18, 8);

        builder.Property(x => x.Profit)
            .HasPrecision(18, 8);

        builder.Property(x => x.ExitReason)
            .HasMaxLength(200);

        builder.HasOne(x => x.BacktestRun)
            .WithMany()
            .HasForeignKey(x => x.BacktestRunId);

        builder.HasOne(x => x.Cryptocurrency)
            .WithMany()
            .HasForeignKey(x => x.CryptocurrencyId);

        builder.HasIndex(x => x.EntryTime);
    }
}