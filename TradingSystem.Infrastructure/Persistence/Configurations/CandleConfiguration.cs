using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class CandleConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> builder)
    {
        builder.ToTable("Candles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source)
            .HasConversion<int>();

        builder.Property(x => x.Exchange)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Open).HasPrecision(18, 8);
        builder.Property(x => x.High).HasPrecision(18, 8);
        builder.Property(x => x.Low).HasPrecision(18, 8);
        builder.Property(x => x.Close).HasPrecision(18, 8);
        builder.Property(x => x.Volume).HasPrecision(28, 8);

        // ✅ Index بهینه برای کوئری‌های چندمنبعی
        builder.HasIndex(x => new
        {
            x.CryptocurrencyId,
            x.Source,
            x.Exchange,
            x.Timeframe,
            x.OpenTime
        })
        .IsUnique()
        .HasDatabaseName("IX_Candles_Unique");

        builder.HasOne(x => x.Cryptocurrency)
            .WithMany(x => x.Candles)
            .HasForeignKey(x => x.CryptocurrencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}