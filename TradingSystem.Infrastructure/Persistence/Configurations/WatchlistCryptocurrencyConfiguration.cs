using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class WatchlistCryptocurrencyConfiguration
    : IEntityTypeConfiguration<WatchlistCryptocurrency>
{
    public void Configure(
        EntityTypeBuilder<WatchlistCryptocurrency> builder)
    {
        builder.ToTable("WatchlistCryptocurrencies");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x =>
            new
            {
                x.WatchlistId,
                x.CryptocurrencyId
            })
            .IsUnique();

        builder.HasOne(x => x.Watchlist)
            .WithMany(x => x.WatchlistCryptocurrencies)
            .HasForeignKey(x => x.WatchlistId);

        builder.HasOne(x => x.Cryptocurrency)
            .WithMany()
            .HasForeignKey(x => x.CryptocurrencyId);
    }
}