using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class WatchlistConfiguration
    : IEntityTypeConfiguration<Watchlist>
{
    public void Configure(
        EntityTypeBuilder<Watchlist> builder)
    {
        builder.ToTable("Watchlists");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();
    }
}