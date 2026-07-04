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

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Symbol).HasMaxLength(10);

        builder.HasIndex(x => x.IsSupported);
    }
}

public class CountryCurrencyConfiguration : IEntityTypeConfiguration<CountryCurrency>
{
    public void Configure(EntityTypeBuilder<CountryCurrency> builder)
    {
        builder.HasIndex(x => new { x.CountryCode, x.CurrencyCode }).IsUnique();

        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);

        builder.HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}