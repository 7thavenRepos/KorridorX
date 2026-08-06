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
        builder.HasIndex(x => new { x.ProviderCode, x.BusinessProfileId }).IsUnique();

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

        builder.Property(x => x.Amount).HasPrecision(18, 2);

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
public class ProviderBankConfiguration : IEntityTypeConfiguration<ProviderBank>
{
    public void Configure(EntityTypeBuilder<ProviderBank> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderBankId }).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.CountryCode, x.IsActive });
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Code);

        builder.Property(x => x.ProviderBankId).HasMaxLength(150);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(100);
        builder.Property(x => x.NationalBankCode).HasMaxLength(100);
        builder.Property(x => x.ProviderCountryId).HasMaxLength(150);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CountryName).HasMaxLength(150);
    }
}
