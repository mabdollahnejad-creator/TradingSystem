using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingSystem.Domain.Entities;

namespace TradingSystem.Infrastructure.Persistence.Configurations;

public class StrategyDefinitionConfiguration : IEntityTypeConfiguration<StrategyDefinition>
{
    public void Configure(EntityTypeBuilder<StrategyDefinition> builder)
    {
        builder.ToTable("StrategyDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        // ✅ اصلاح: حذف HasColumnType("nvarchar(max)") 
        // برای متن‌های طولانی در SQLite نیازی به تعریف نوع نیست، EF Core خودش TEXT را انتخاب می‌کند
        builder.Property(x => x.IndicatorsJson);
    }
}