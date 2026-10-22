using KorridorX.Models.WalletWithdrawals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class FiatWalletWithdrawalConfiguration : IEntityTypeConfiguration<FiatWalletWithdrawal>
{
    public void Configure(EntityTypeBuilder<FiatWalletWithdrawal> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.OwnerType, x.OwnerId, x.IdempotencyKeyHash }).IsUnique();
        b.HasIndex(x => new { x.OwnerType, x.OwnerId, x.CreatedAt });
        b.HasIndex(x => new { x.CustomerProfileId, x.SourceCountryCode, x.CurrencyCode, x.CreatedAt });
        b.HasIndex(x => new { x.BusinessProfileId, x.SourceCountryCode, x.CurrencyCode, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.FinancialReservationId).IsUnique().HasFilter("\"FinancialReservationId\" IS NOT NULL");
        b.HasIndex(x => x.PayoutId).IsUnique().HasFilter("\"PayoutId\" IS NOT NULL");

        b.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinancialReservation).WithMany().HasForeignKey(x => x.FinancialReservationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.Reference).HasMaxLength(80).IsRequired();
        b.Property(x => x.IdempotencyKeyHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceCountryCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.BankName).HasMaxLength(150);
        b.Property(x => x.ProviderBankId).HasMaxLength(100);
        b.Property(x => x.ProtectedAccountNumber).HasMaxLength(2048);
        b.Property(x => x.AccountNumberLast4).HasMaxLength(4);
        b.Property(x => x.VerifiedAccountName).HasMaxLength(250);
        b.Property(x => x.RecipientEmail).HasMaxLength(254);
        b.Property(x => x.KorridorXFeePolicy).HasMaxLength(40).IsRequired();
        b.Property(x => x.OperationalHoldReason).HasMaxLength(1000);
        b.Property(x => x.WithdrawalAmount).HasPrecision(36, 18);
        b.Property(x => x.ProviderFee).HasPrecision(36, 18);
        b.Property(x => x.KorridorXFee).HasPrecision(36, 18);
        b.Property(x => x.TotalWalletDebit).HasPrecision(36, 18);
        b.Property(x => x.ExpectedRecipientAmount).HasPrecision(36, 18);
        b.Property(x => x.Status).IsConcurrencyToken();
    }
}
