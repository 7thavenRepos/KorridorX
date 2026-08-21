using KorridorX.Models.EmbeddedFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class ApiApplicationConfiguration : IEntityTypeConfiguration<ApiApplication>
{
    public void Configure(EntityTypeBuilder<ApiApplication> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.AllowedIpRanges).HasMaxLength(4000);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.BusinessProfile).WithMany().HasForeignKey(x => x.BusinessProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ApiCredentialConfiguration : IEntityTypeConfiguration<ApiCredential>
{
    public void Configure(EntityTypeBuilder<ApiCredential> builder)
    {
        builder.HasIndex(x => x.KeyId).IsUnique();
        builder.HasIndex(x => new { x.ApiApplicationId, x.Status });
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.KeyId).HasMaxLength(40);
        builder.Property(x => x.SecretHash).HasMaxLength(64);
        builder.Property(x => x.SecretLastFour).HasMaxLength(4);
        builder.Property(x => x.LastUsedIpAddress).HasMaxLength(100);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.ApiApplication).WithMany(x => x.Credentials).HasForeignKey(x => x.ApiApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BusinessCustomerConfiguration : IEntityTypeConfiguration<BusinessCustomer>
{
    public void Configure(EntityTypeBuilder<BusinessCustomer> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.ExternalReference }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.BusinessProfileId, x.Status });
        builder.Property(x => x.ExternalReference).HasMaxLength(150);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.BusinessProfile).WithMany().HasForeignKey(x => x.BusinessProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectionAccountConfiguration : IEntityTypeConfiguration<CollectionAccount>
{
    public void Configure(EntityTypeBuilder<CollectionAccount> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.ExternalReference }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.BusinessCustomerId, x.AssetCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => x.FinancialAccountId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.ExternalReference).HasMaxLength(150);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.BusinessProfile).WithMany().HasForeignKey(x => x.BusinessProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BusinessCustomer).WithMany(x => x.CollectionAccounts).HasForeignKey(x => x.BusinessCustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProviderAccountMappingConfiguration : IEntityTypeConfiguration<ProviderAccountMapping>
{
    public void Configure(EntityTypeBuilder<ProviderAccountMapping> builder)
    {
        builder.HasIndex(x => new { x.CollectionAccountId, x.ProviderCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderAccountId })
            .IsUnique()
            .HasFilter("\"ProviderAccountId\" IS NOT NULL AND \"IsDeleted\" = false");
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderCustomerId).HasMaxLength(150);
        builder.Property(x => x.ProviderAccountId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.AccountNumber).HasMaxLength(150);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.BankName).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.CollectionAccount).WithMany(x => x.ProviderMappings).HasForeignKey(x => x.CollectionAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmbeddedApiIdempotencyRecordConfiguration : IEntityTypeConfiguration<EmbeddedApiIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<EmbeddedApiIdempotencyRecord> builder)
    {
        builder.HasIndex(x => new { x.ApiApplicationId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.RequestHash).HasMaxLength(64);
        builder.Property(x => x.ResourceType).HasMaxLength(100);
        builder.HasOne(x => x.ApiApplication).WithMany(x => x.IdempotencyRecords).HasForeignKey(x => x.ApiApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}


public sealed class BusinessWebhookEndpointConfiguration : IEntityTypeConfiguration<BusinessWebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<BusinessWebhookEndpoint> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.Url }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.ApiApplicationId, x.Status });
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.EventTypesCsv).HasMaxLength(4000);
        builder.Property(x => x.SigningSecretProtected).HasColumnType("text");
        builder.Property(x => x.SigningSecretLastFour).HasMaxLength(4);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.ApiApplication).WithMany().HasForeignKey(x => x.ApiApplicationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BusinessWebhookEventConfiguration : IEntityTypeConfiguration<BusinessWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BusinessWebhookEvent> builder)
    {
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.BusinessProfileId, x.OccurredAt });
        builder.HasIndex(x => new { x.BusinessProfileId, x.EventType, x.OccurredAt });
        builder.Property(x => x.EventId).HasMaxLength(80);
        builder.Property(x => x.EventType).HasMaxLength(150);
        builder.Property(x => x.PayloadJson).HasColumnType("text");
    }
}

public sealed class BusinessWebhookDeliveryConfiguration : IEntityTypeConfiguration<BusinessWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<BusinessWebhookDelivery> builder)
    {
        builder.HasIndex(x => new { x.BusinessWebhookEventId, x.BusinessWebhookEndpointId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => x.LockId);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.LastResponseBody).HasMaxLength(4000);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasOne(x => x.BusinessWebhookEvent).WithMany(x => x.Deliveries).HasForeignKey(x => x.BusinessWebhookEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BusinessWebhookEndpoint).WithMany(x => x.Deliveries).HasForeignKey(x => x.BusinessWebhookEndpointId).OnDelete(DeleteBehavior.Restrict);
    }
}
