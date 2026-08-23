using KorridorX.Models.EmbeddedFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class BusinessPricingPolicyConfiguration :
    IEntityTypeConfiguration<BusinessPricingPolicy>
{
    public void Configure(EntityTypeBuilder<BusinessPricingPolicy> builder)
    {
        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.SourceAssetCode,
            x.DestinationAssetCode,
            x.IsActive,
            x.EffectiveFrom
        });

        builder.Property(x => x.SourceAssetCode).HasMaxLength(20);
        builder.Property(x => x.DestinationAssetCode).HasMaxLength(20);
        builder.Property(x => x.MarkupPercentage).HasPrecision(9, 6);
        builder.Property(x => x.AdjustmentValue).HasPrecision(18, 8);
        builder.Property(x => x.MinimumCustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.MaximumCustomerRate).HasPrecision(18, 8);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
