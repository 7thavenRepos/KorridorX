using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.BaseUrl).HasMaxLength(500);
    }
}

public class ProviderCustomerConfiguration : IEntityTypeConfiguration<ProviderCustomer>
{
    public void Configure(EntityTypeBuilder<ProviderCustomer> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderCustomerId }).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.CustomerProfileId }).IsUnique();

        builder.Property(x => x.ProviderCustomerId).HasMaxLength(150);
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProviderRequestLogConfiguration : IEntityTypeConfiguration<ProviderRequestLog>
{
    public void Configure(EntityTypeBuilder<ProviderRequestLog> builder)
    {
        builder.HasIndex(x => x.ProviderCode);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.RequestedAt);
        builder.HasIndex(x => x.RelatedTransferId);
        builder.HasIndex(x => x.RelatedCollectionId);
        builder.HasIndex(x => x.RelatedPayoutId);

        builder.Property(x => x.Endpoint).HasMaxLength(500);
        builder.Property(x => x.HttpMethod).HasMaxLength(20);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
    }
}

public class ProviderTransactionConfiguration : IEntityTypeConfiguration<ProviderTransaction>
{
    public void Configure(EntityTypeBuilder<ProviderTransaction> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderTransactionId }).IsUnique();
        builder.HasIndex(x => x.ProviderReference);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.CollectionId);
        builder.HasIndex(x => x.PayoutId);

        builder.Property(x => x.ProviderTransactionId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.TransactionType).HasMaxLength(100);
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);

        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.AmountWithoutFee).HasPrecision(36, 18);
        builder.Property(x => x.ProviderFeeAmount).HasPrecision(36, 18);
        builder.Property(x => x.ProviderFeeCurrencyCode).HasMaxLength(10);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Collection)
            .WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payout)
            .WithMany()
            .HasForeignKey(x => x.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayoutDestinationProviderMappingConfiguration :
    IEntityTypeConfiguration<PayoutDestinationProviderMapping>
{
    public void Configure(EntityTypeBuilder<PayoutDestinationProviderMapping> builder)
    {
        builder.HasIndex(x => new
        {
            x.DestinationType,
            x.DestinationId,
            x.ProviderCode
        })
        .IsUnique()
        .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(x => new
        {
            x.ProviderCode,
            x.ProviderPartyId
        });

        builder.HasIndex(x => new
        {
            x.ProviderCode,
            x.ProviderDestinationId
        });

        builder.HasIndex(x => x.IsVerified);

        builder.Property(x => x.ProviderBankId).HasMaxLength(150);
        builder.Property(x => x.ProviderPartyId).HasMaxLength(150);
        builder.Property(x => x.ProviderDestinationId).HasMaxLength(150);
        builder.Property(x => x.ProviderVerifiedAccountName).HasMaxLength(200);
        builder.Property(x => x.ProviderVerificationReference).HasMaxLength(150);
        builder.Property(x => x.LastVerificationError).HasMaxLength(1000);

        builder.Property(x => x.IsVerified).IsConcurrencyToken();
        builder.Property(x => x.IsActive).IsConcurrencyToken();
    }
}

