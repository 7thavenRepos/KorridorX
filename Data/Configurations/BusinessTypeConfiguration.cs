using KorridorX.Models.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class BusinessTypeConfiguration : IEntityTypeConfiguration<BusinessType>
{
    public void Configure(EntityTypeBuilder<BusinessType> builder)
    {
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(64);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        // Migration-only seed: subsequent starts must retain administrator changes.
        builder.HasData(
            new BusinessType { Code = "corporation", Name = "Corporation", SortOrder = 10 },
            new BusinessType { Code = "government_entity", Name = "Government entity", SortOrder = 20 },
            new BusinessType { Code = "llc", Name = "Limited liability company (LLC)", SortOrder = 30 },
            new BusinessType { Code = "non_profit", Name = "Non-profit organization", SortOrder = 40 },
            new BusinessType { Code = "partnership", Name = "Partnership", SortOrder = 50 },
            new BusinessType { Code = "sole_proprietorship", Name = "Sole proprietorship", SortOrder = 60 },
            new BusinessType { Code = "other", Name = "Other", SortOrder = 70 });
    }
}
