using KorridorX.Models.Recipients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.CountryCode);

        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.MiddleName).HasMaxLength(100);
        builder.Property(x => x.Nickname).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.RelationshipToSender).HasMaxLength(100);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecipientBankAccountConfiguration : IEntityTypeConfiguration<RecipientBankAccount>
{
    public void Configure(EntityTypeBuilder<RecipientBankAccount> builder)
    {
        builder.HasIndex(x => x.RecipientId);
        builder.HasIndex(x => new { x.CountryCode, x.CurrencyCode });

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
        builder.Property(x => x.ProviderRecipientId).HasMaxLength(150);
        builder.Property(x => x.ProviderBankAccountId).HasMaxLength(150);

        builder.HasOne(x => x.Recipient)
            .WithMany(x => x.BankAccounts)
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecipientMobileWalletConfiguration : IEntityTypeConfiguration<RecipientMobileWallet>
{
    public void Configure(EntityTypeBuilder<RecipientMobileWallet> builder)
    {
        builder.HasIndex(x => x.RecipientId);
        builder.HasIndex(x => new { x.CountryCode, x.CurrencyCode });

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderName).HasMaxLength(150);
        builder.Property(x => x.WalletNumber).HasMaxLength(100);
        builder.Property(x => x.AccountName).HasMaxLength(200);
        builder.Property(x => x.ProviderRecipientId).HasMaxLength(150);
        builder.Property(x => x.ProviderWalletId).HasMaxLength(150);

        builder.HasOne(x => x.Recipient)
            .WithMany(x => x.MobileWallets)
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}