using KorridorX.Models.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Iso3Code).HasMaxLength(10);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsSupported);
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Symbol).HasMaxLength(20);
        builder.Property(x => x.Type).HasConversion<int>();

        builder.HasIndex(x => x.IsSupported);
        builder.HasIndex(x => new { x.Type, x.IsSupported });
    }
}

public class AssetNetworkConfiguration : IEntityTypeConfiguration<AssetNetwork>
{
    public void Configure(EntityTypeBuilder<AssetNetwork> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AssetCode, x.NetworkCode }).IsUnique();

        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.NetworkCode).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.NativeAssetCode).HasMaxLength(20);
        builder.Property(x => x.ContractAddress).HasMaxLength(200);
        builder.Property(x => x.MinimumDeposit).HasPrecision(36, 18);
        builder.Property(x => x.MinimumWithdrawal).HasPrecision(36, 18);
        builder.Property(x => x.WithdrawalFee).HasPrecision(36, 18);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasOne(x => x.Asset)
            .WithMany(x => x.Networks)
            .HasForeignKey(x => x.AssetCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CountryAssetConfiguration : IEntityTypeConfiguration<CountryAsset>
{
    public void Configure(EntityTypeBuilder<CountryAsset> builder)
    {
        builder.HasIndex(x => new { x.CountryCode, x.AssetCode }).IsUnique();

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.AssetCode).HasMaxLength(20);

        builder.HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Asset)
            .WithMany()
            .HasForeignKey(x => x.AssetCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
