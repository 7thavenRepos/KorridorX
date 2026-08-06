using KorridorX.Models.BusinessBeneficiaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class BusinessBeneficiaryConfiguration : IEntityTypeConfiguration<BusinessBeneficiary>
{
    public void Configure(EntityTypeBuilder<BusinessBeneficiary> builder)
    {
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.CountryCode);
        builder.HasIndex(x => new { x.BusinessProfileId, x.Name });

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.ContactFirstName).HasMaxLength(100);
        builder.Property(x => x.ContactLastName).HasMaxLength(100);
        builder.Property(x => x.Nickname).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.RelationshipOrPurpose).HasMaxLength(200);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessBeneficiaryBankAccountConfiguration :
    IEntityTypeConfiguration<BusinessBeneficiaryBankAccount>
{
    public void Configure(EntityTypeBuilder<BusinessBeneficiaryBankAccount> builder)
    {
        builder.HasIndex(x => x.BusinessBeneficiaryId);
        builder.HasIndex(x => new { x.CountryCode, x.CurrencyCode });
        builder.HasIndex(x => x.ProviderBankId);

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.BankName).HasMaxLength(150);
        builder.Property(x => x.BankCode).HasMaxLength(100);
        builder.Property(x => x.BranchCode).HasMaxLength(100);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.AccountNumber).HasMaxLength(100);
        builder.Property(x => x.Iban).HasMaxLength(100);
        builder.Property(x => x.SwiftBic).HasMaxLength(100);
        builder.Property(x => x.RoutingNumber).HasMaxLength(100);
        builder.Property(x => x.SortCode).HasMaxLength(100);
        builder.Property(x => x.ProviderBankId).HasMaxLength(150);
        builder.Property(x => x.ProviderBeneficiaryId).HasMaxLength(150);
        builder.Property(x => x.ProviderBankAccountId).HasMaxLength(150);
        builder.Property(x => x.ProviderVerifiedAccountName).HasMaxLength(200);
        builder.Property(x => x.ProviderVerificationReference).HasMaxLength(150);
        builder.Property(x => x.LastVerificationError).HasMaxLength(1000);

        builder.HasOne(x => x.BusinessBeneficiary)
            .WithMany(x => x.BankAccounts)
            .HasForeignKey(x => x.BusinessBeneficiaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BusinessBeneficiaryMobileWalletConfiguration :
    IEntityTypeConfiguration<BusinessBeneficiaryMobileWallet>
{
    public void Configure(EntityTypeBuilder<BusinessBeneficiaryMobileWallet> builder)
    {
        builder.HasIndex(x => x.BusinessBeneficiaryId);
        builder.HasIndex(x => new { x.CountryCode, x.CurrencyCode });

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderName).HasMaxLength(150);
        builder.Property(x => x.WalletNumber).HasMaxLength(100);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.ProviderBeneficiaryId).HasMaxLength(150);
        builder.Property(x => x.ProviderWalletId).HasMaxLength(150);

        builder.HasOne(x => x.BusinessBeneficiary)
            .WithMany(x => x.MobileWallets)
            .HasForeignKey(x => x.BusinessBeneficiaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
