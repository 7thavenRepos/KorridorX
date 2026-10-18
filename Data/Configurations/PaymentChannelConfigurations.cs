using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class PaymentChannelMap : IEntityTypeConfiguration<PaymentChannel>
{
    public void Configure(EntityTypeBuilder<PaymentChannel> b)
    {
        b.ToTable("PaymentChannels"); b.HasKey(x => x.Id);
        b.Property(x => x.Environment).HasMaxLength(50);
        b.Property(x => x.ProviderCode).HasMaxLength(50);
        b.Property(x => x.AssetCode).HasMaxLength(20);
        b.Property(x => x.CountryCode).HasMaxLength(10);
        b.Property(x => x.CapabilityCode).HasMaxLength(60);
        b.Property(x => x.DisplayName).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.MinimumAmount).HasPrecision(36, 18);
        b.Property(x => x.MaximumAmount).HasPrecision(36, 18);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetCode).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProviderWalletConfiguration>().WithMany().HasForeignKey(x => x.ProviderWalletConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Environment, x.Direction, x.AssetCode });
        b.HasIndex(x => new { x.Environment, x.Direction, x.AssetCode, x.CountryCode, x.CapabilityCode, x.ProviderWalletConfigurationId })
            .IsUnique().HasDatabaseName("UX_PaymentChannel_Route");
    }
}

public sealed class PaymentChannelSelectionMap : IEntityTypeConfiguration<PaymentChannelSelection>
{
    public void Configure(EntityTypeBuilder<PaymentChannelSelection> b)
    {
        b.ToTable("PaymentChannelSelections"); b.HasKey(x => x.Id);
        b.Property(x => x.OperationType).HasMaxLength(40);
        b.Property(x => x.ProviderCode).HasMaxLength(50);
        b.Property(x => x.Environment).HasMaxLength(50);
        b.Property(x => x.ProviderWalletId).HasMaxLength(150);
        b.Property(x => x.CapabilityCode).HasMaxLength(60);
        b.Property(x => x.AssetCode).HasMaxLength(20);
        b.Property(x => x.CountryCode).HasMaxLength(10);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(36, 18);
        b.HasOne<PaymentChannel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProviderWalletConfiguration>().WithMany().HasForeignKey(x => x.ProviderWalletConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OperationType, x.OperationId }).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.FinancialAccountId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL").HasDatabaseName("UX_PaymentChannel_Request");
    }
}

public sealed class WalletDepositAccountMap : IEntityTypeConfiguration<WalletDepositAccount>
{
    public void Configure(EntityTypeBuilder<WalletDepositAccount> b)
    {
        b.ToTable("WalletDepositAccounts"); b.HasKey(x => x.Id);
        b.Property(x => x.Environment).HasMaxLength(50);
        b.Property(x => x.ConnectionKey).HasMaxLength(64);
        b.Property(x => x.AssetCode).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ProviderWalletId).HasMaxLength(150);
        b.Property(x => x.ProviderCustomerId).HasMaxLength(150);
        b.Property(x => x.ProviderAccountId).HasMaxLength(150);
        b.Property(x => x.AccountNumber).HasMaxLength(150);
        b.Property(x => x.AccountName).HasMaxLength(200);
        b.Property(x => x.BankName).HasMaxLength(200);
        b.Property(x => x.Iban).HasMaxLength(50);
        b.Property(x => x.SortCode).HasMaxLength(50);
        b.Property(x => x.RoutingNumber).HasMaxLength(50);
        b.Property(x => x.ProviderReference).HasMaxLength(150);
        b.Property(x => x.FailureReason).HasMaxLength(1000);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PaymentChannel>().WithMany().HasForeignKey(x => x.PaymentChannelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProviderWalletConfiguration>().WithMany().HasForeignKey(x => x.ProviderWalletConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Environment, x.ProviderWalletId, x.ProviderCustomerId }).IsUnique()
            .HasDatabaseName("UX_WalletDepositAccount_CustomerWallet");
        b.HasIndex(x => new { x.Environment, x.ConnectionKey, x.ProviderAccountId }).IsUnique()
            .HasFilter("\"ProviderAccountId\" IS NOT NULL").HasDatabaseName("UX_WalletDepositAccount_ProviderAccount");
    }
}

public sealed class PaymentChannelCollectionMap : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> b)
    {
        b.Property(x => x.PaymentChannelName).HasMaxLength(120);
        b.Property(x => x.ClientRequestKey).HasMaxLength(100);
        b.HasIndex(x => new { x.FinancialAccountId, x.ClientRequestKey }).IsUnique()
            .HasFilter("\"ClientRequestKey\" IS NOT NULL").HasDatabaseName("UX_Collection_WalletRequest");
    }
}
public sealed class PaymentChannelPayoutMap : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> b) => b.Property(x => x.PaymentChannelName).HasMaxLength(120);
}
