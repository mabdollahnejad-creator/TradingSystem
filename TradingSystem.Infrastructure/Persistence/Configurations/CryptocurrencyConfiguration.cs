using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class CryptocurrencyConfiguration : IEntityTypeConfiguration<Cryptocurrency>
{
    public void Configure(EntityTypeBuilder<Cryptocurrency> builder)
    {
        builder.ToTable("Cryptocurrencies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Symbol)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CoinGeckoId)
            .HasMaxLength(100);

        builder.HasIndex(x => x.Symbol)
            .IsUnique();

        builder.HasIndex(x => x.CoinGeckoId);

        // ✅ فیلدهای جدید
        builder.Property(x => x.IsAvailableInBinance).HasDefaultValue(false);
        builder.Property(x => x.IsAvailableInNobitex).HasDefaultValue(false);
        builder.Property(x => x.IsAvailableInWallex).HasDefaultValue(false);

        builder.HasMany(x => x.Candles)
            .WithOne(x => x.Cryptocurrency)
            .HasForeignKey(x => x.CryptocurrencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}