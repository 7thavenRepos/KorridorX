using KorridorX.Models.Fx;
using KorridorX.Models.Instant;
using KorridorX.Models.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class FxProviderRateConfigurationMapping : IEntityTypeConfiguration<FxProviderRateConfiguration>
{
    public void Configure(EntityTypeBuilder<FxProviderRateConfiguration> b)
    {
        b.ToTable("FxProviderRateConfigurations", t =>
        {
            t.HasCheckConstraint("CK_FxProviderRateConfigurations_Pair", "(\"InstantPairId\" IS NOT NULL) <> (\"MarketplacePairId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_FxProviderRateConfigurations_Markup", "\"MarkupPercentage\" >= 0 AND \"MarkupPercentage\" < 100");
        });
        b.HasOne<InstantPair>().WithMany().HasForeignKey(x => x.InstantPairId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MarketplacePair>().WithMany().HasForeignKey(x => x.MarketplacePairId).OnDelete(DeleteBehavior.Restrict);
        // Duplicate master-catalog entries for the same direction cannot create competing active prices.
        b.HasIndex(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode }).IsUnique()
            .HasFilter("\"IsActive\" = TRUE AND \"IsDeleted\" = FALSE")
            .HasDatabaseName("UX_FxProviderRateConfigurations_ActiveDirection");
        b.HasIndex(x => new { x.IsActive, x.NextRefreshAtUtc });
        b.Property(x => x.SourceCurrencyCode).HasMaxLength(20);
        b.Property(x => x.DestinationCurrencyCode).HasMaxLength(20);
        b.Property(x => x.ProviderCode).HasMaxLength(50);
        b.Property(x => x.ProviderRateId).HasMaxLength(150);
        b.Property(x => x.MarkupPercentage).HasPrecision(9, 6);
        b.Property(x => x.MinimumCustomerRate).HasPrecision(18, 8);
        b.Property(x => x.MaximumCustomerRate).HasPrecision(18, 8);
        b.Property(x => x.LastProviderRate).HasPrecision(18, 8);
        b.Property(x => x.LastCustomerRate).HasPrecision(18, 8);
        b.Property(x => x.ActivatedConnectionKey).HasMaxLength(64);
        b.Property(x => x.ScheduleMode).HasMaxLength(16);
        b.Property(x => x.TimeZoneId).HasMaxLength(100);
        b.Property(x => x.DailyTimes).HasMaxLength(150);
        b.Property(x => x.LastError).HasMaxLength(500);
        b.Property(x => x.Revision).IsConcurrencyToken();
    }
}

public sealed class FxRateObservationMapping : IEntityTypeConfiguration<FxRateObservation>
{
    public void Configure(EntityTypeBuilder<FxRateObservation> b)
    {
        b.HasOne<FxProviderRateConfiguration>().WithMany().HasForeignKey(x => x.ConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConfigurationId, x.ReceivedAtUtc });
        b.Property(x => x.SourceCurrencyCode).HasMaxLength(20);
        b.Property(x => x.DestinationCurrencyCode).HasMaxLength(20);
        b.Property(x => x.ProviderCode).HasMaxLength(50);
        b.Property(x => x.ProviderRateId).HasMaxLength(150);
        b.Property(x => x.ConnectionKey).HasMaxLength(64);
        b.Property(x => x.Purpose).HasMaxLength(32);
        b.Property(x => x.ProviderRate).HasPrecision(18, 8);
        b.Property(x => x.CustomerRate).HasPrecision(18, 8);
        b.Property(x => x.MarkupPercentage).HasPrecision(9, 6);
    }
}
