using KorridorX.Models.Lookups;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class ProviderWalletConfigurationMap : IEntityTypeConfiguration<ProviderWalletConfiguration>
{
    public void Configure(EntityTypeBuilder<ProviderWalletConfiguration> b)
    {
        b.ToTable("ProviderWalletConfigurations");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProviderCode).HasMaxLength(50);
        b.Property(x => x.Environment).HasMaxLength(50);
        b.Property(x => x.ProviderWalletId).HasMaxLength(150);
        b.Property(x => x.AssetCode).HasMaxLength(20);
        b.Property(x => x.NetworkCode).HasMaxLength(50);
        b.Property(x => x.DisplayName).HasMaxLength(120);
        b.Property(x => x.VerifiedConnectionKey).HasMaxLength(64);
        b.Property(x => x.LastProviderBalance).HasPrecision(36, 18);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetCode).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProviderCode, x.Environment, x.ProviderWalletId, x.AssetCode, x.NetworkCode }).IsUnique()
            .HasDatabaseName("UX_ProviderWallet_Identity");
        b.HasIndex(x => new { x.ProviderCode, x.Environment, x.AssetCode, x.NetworkCode }, "CollectionRoute").IsUnique()
            .HasDatabaseName("UX_ProviderWallet_CollectionRoute")
            .HasFilter("\"IsActive\" AND \"DefaultForCollection\" AND NOT \"IsDeleted\"");
        b.HasIndex(x => new { x.ProviderCode, x.Environment, x.AssetCode, x.NetworkCode }, "PayoutRoute").IsUnique()
            .HasDatabaseName("UX_ProviderWallet_PayoutRoute")
            .HasFilter("\"IsActive\" AND \"DefaultForPayout\" AND NOT \"IsDeleted\"");
    }
}

public sealed class ProviderWalletSelectionMap : IEntityTypeConfiguration<ProviderWalletSelection>
{
    public void Configure(EntityTypeBuilder<ProviderWalletSelection> b)
    {
        b.ToTable("ProviderWalletSelections");
        b.HasKey(x => x.Id);
        b.Property(x => x.OperationType).HasMaxLength(40);
        b.Property(x => x.Purpose).HasMaxLength(20);
        b.Property(x => x.ProviderWalletId).HasMaxLength(150);
        b.HasIndex(x => new { x.OperationType, x.OperationId }).IsUnique();
        b.HasOne(x => x.WalletConfiguration).WithMany().HasForeignKey(x => x.WalletConfigurationId).OnDelete(DeleteBehavior.Restrict);
    }
}
