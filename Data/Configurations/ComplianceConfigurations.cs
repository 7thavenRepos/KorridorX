using KorridorX.Models.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class KycProfileConfiguration : IEntityTypeConfiguration<KycProfile>
{
    public void Configure(EntityTypeBuilder<KycProfile> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProviderKycId);

        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderKycId).HasMaxLength(150);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class KycApplicationConfiguration : IEntityTypeConfiguration<KycApplication>
{
    public void Configure(EntityTypeBuilder<KycApplication> builder)
    {
        builder.HasIndex(x => x.KycProfileId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProviderApplicationId);

        builder.Property(x => x.ProviderApplicationId).HasMaxLength(150);
        builder.Property(x => x.IdentityType).HasMaxLength(50);
        builder.Property(x => x.IdentityNumberLastFour).HasMaxLength(4);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);

        builder.HasOne(x => x.KycProfile)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.KycProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class KycDocumentConfiguration : IEntityTypeConfiguration<KycDocument>
{
    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.HasIndex(x => x.KycApplicationId);
        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.ProviderFileId);
        builder.HasIndex(x => x.ProviderDocumentId);
        builder.HasIndex(x => new { x.KycApplicationId, x.DocumentType }).IsUnique();

        builder.Property(x => x.DocumentType).HasMaxLength(100);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.MimeType).HasMaxLength(100);
        builder.Property(x => x.StorageProvider).HasMaxLength(50);
        builder.Property(x => x.StorageKey).HasMaxLength(500);
        builder.Property(x => x.StorageUrl).HasMaxLength(1000);
        builder.Property(x => x.ProviderFileId).HasMaxLength(150);
        builder.Property(x => x.ProviderDocumentId).HasMaxLength(150);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.HasOne(x => x.KycApplication)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.KycApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComplianceLimitConfiguration : IEntityTypeConfiguration<ComplianceLimit>
{
    public void Configure(EntityTypeBuilder<ComplianceLimit> builder)
    {
        builder.HasIndex(x => new
        {
            x.CustomerType,
            x.CountryCode,
            x.CurrencyCode,
            x.IsActive
        });

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);

        builder.Property(x => x.DailyLimit).HasPrecision(18, 2);
        builder.Property(x => x.MonthlyLimit).HasPrecision(18, 2);
        builder.Property(x => x.PerTransferLimit).HasPrecision(18, 2);
    }
}

public class ComplianceCheckConfiguration : IEntityTypeConfiguration<ComplianceCheck>
{
    public void Configure(EntityTypeBuilder<ComplianceCheck> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.CheckType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CheckedAt);

        builder.Property(x => x.CheckType).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(100);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AmlFlagConfiguration : IEntityTypeConfiguration<AmlFlag>
{
    public void Configure(EntityTypeBuilder<AmlFlag> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.FlagType);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.IsResolved);

        builder.Property(x => x.FlagType).HasMaxLength(100);
        builder.Property(x => x.Severity).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ResolutionNote).HasMaxLength(1000);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
public class BusinessKybApplicationConfiguration : IEntityTypeConfiguration<BusinessKybApplication>
{
    public void Configure(EntityTypeBuilder<BusinessKybApplication> builder)
    {
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProviderApplicationId);

        builder.Property(x => x.ProviderApplicationId).HasMaxLength(150);
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessBeneficialOwnerConfiguration : IEntityTypeConfiguration<BusinessBeneficialOwner>
{
    public void Configure(EntityTypeBuilder<BusinessBeneficialOwner> builder)
    {
        builder.HasIndex(x => x.BusinessKybApplicationId);
        builder.HasIndex(x => x.ProviderOwnerId);
        builder.HasIndex(x => new { x.BusinessKybApplicationId, x.Email }).IsUnique();

        builder.Property(x => x.ProviderOwnerId).HasMaxLength(150);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.Nationality).HasMaxLength(10);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.OwnershipPercentage).HasPrecision(7, 4);
        builder.Property(x => x.IdDocumentType).HasMaxLength(50);
        builder.Property(x => x.IdentityNumberLastFour).HasMaxLength(4);
        builder.Property(x => x.IdentityNumberEncrypted).HasMaxLength(2000);
        builder.Property(x => x.IdDocumentCountry).HasMaxLength(10);
        builder.Property(x => x.IdentityFrontProviderFileId).HasMaxLength(150);
        builder.Property(x => x.IdentityBackProviderFileId).HasMaxLength(150);
        builder.Property(x => x.ProviderStatus).HasMaxLength(50);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);

        builder.HasOne(x => x.BusinessKybApplication)
            .WithMany(x => x.Owners)
            .HasForeignKey(x => x.BusinessKybApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BusinessKybDocumentConfiguration : IEntityTypeConfiguration<BusinessKybDocument>
{
    public void Configure(EntityTypeBuilder<BusinessKybDocument> builder)
    {
        builder.HasIndex(x => x.BusinessKybApplicationId);
        builder.HasIndex(x => x.ProviderFileId);
        builder.HasIndex(x => x.ProviderDocumentId);
        builder.HasIndex(x => new { x.BusinessKybApplicationId, x.DocumentType, x.Name });

        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.MimeType).HasMaxLength(100);
        builder.Property(x => x.StorageProvider).HasMaxLength(50);
        builder.Property(x => x.StorageKey).HasMaxLength(500);
        builder.Property(x => x.ProviderFileId).HasMaxLength(150);
        builder.Property(x => x.ProviderDocumentId).HasMaxLength(150);
        builder.Property(x => x.ProviderStatus).HasMaxLength(50);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);

        builder.HasOne(x => x.BusinessKybApplication)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.BusinessKybApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
