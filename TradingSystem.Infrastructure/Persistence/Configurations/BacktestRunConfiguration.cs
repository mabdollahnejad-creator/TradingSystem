using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class BacktestRunConfiguration
    : IEntityTypeConfiguration<BacktestRun>
{
    public void Configure(
        EntityTypeBuilder<BacktestRun> builder)
    {
        builder.ToTable("BacktestRuns");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Strategy)
            .WithMany()
            .HasForeignKey(x => x.StrategyDefinitionId);

        builder.HasMany(x => x.Results)
            .WithOne(x => x.BacktestRun)
            .HasForeignKey(x => x.BacktestRunId);
    }
}