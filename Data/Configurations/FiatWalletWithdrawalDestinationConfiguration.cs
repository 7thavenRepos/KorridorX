using KorridorX.Models.WalletWithdrawals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class FiatWalletWithdrawalDestinationConfiguration : IEntityTypeConfiguration<FiatWalletWithdrawalDestination>
{
    public void Configure(EntityTypeBuilder<FiatWalletWithdrawalDestination> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.FinancialAccountId, x.OwnerType, x.OwnerId, x.IsActive });
        b.HasIndex(x => new { x.OwnerType, x.OwnerId, x.AssetCode });
        b.Property(x => x.AssetCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.DestinationCountryCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.ProviderBankId).HasMaxLength(100);
        b.Property(x => x.BankName).HasMaxLength(150);
        b.Property(x => x.ProtectedAccountNumber).HasMaxLength(2048);
        b.Property(x => x.AccountNumberLast4).HasMaxLength(4);
        b.Property(x => x.VerifiedAccountName).HasMaxLength(250);
        b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.VerificationReference).HasMaxLength(100);
    }
}
