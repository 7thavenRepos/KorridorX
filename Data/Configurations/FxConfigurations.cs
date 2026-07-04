using KorridorX.Models.Fx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasIndex(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode, x.IsActive });

        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderRateId).HasMaxLength(150);

        builder.Property(x => x.ProviderRate).HasPrecision(18, 8);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.MarkupRate).HasPrecision(18, 8);
    }
}

public class TransferQuoteConfiguration : IEntityTypeConfiguration<TransferQuote>
{
    public void Configure(EntityTypeBuilder<TransferQuote> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.ProviderQuoteId);

        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.FeeCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderQuoteId).HasMaxLength(150);

        builder.Property(x => x.SourceAmount).HasPrecision(18, 2);
        builder.Property(x => x.DestinationAmount).HasPrecision(18, 2);
        builder.Property(x => x.ProviderRate).HasPrecision(18, 8);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalPayableAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransferFeeConfiguration : IEntityTypeConfiguration<TransferFee>
{
    public void Configure(EntityTypeBuilder<TransferFee> builder)
    {
        builder.HasIndex(x => new
        {
            x.SourceCountryCode,
            x.DestinationCountryCode,
            x.SourceCurrencyCode,
            x.DestinationCurrencyCode,
            x.TransferType,
            x.IsActive
        });

        builder.Property(x => x.SourceCountryCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCountryCode).HasMaxLength(10);
        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.FeeCurrencyCode).HasMaxLength(10);

        builder.Property(x => x.MinAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.FixedFee).HasPrecision(18, 2);
        builder.Property(x => x.PercentageFee).HasPrecision(9, 6);
    }
}