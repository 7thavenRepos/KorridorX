using KorridorX.Models.Instant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class InstantPairConfiguration : IEntityTypeConfiguration<InstantPair>
{
    public void Configure(EntityTypeBuilder<InstantPair> builder)
    {
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(x => new { x.SourceAssetCode, x.DestinationAssetCode })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.SourceAssetCode).HasMaxLength(20);
        builder.Property(x => x.DestinationAssetCode).HasMaxLength(20);
        builder.Property(x => x.MinimumSourceAmount).HasPrecision(36, 18);
        builder.Property(x => x.MaximumSourceAmount).HasPrecision(36, 18);
        builder.Property(x => x.SourceAmountIncrement).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.SourceAsset)
            .WithMany()
            .HasForeignKey(x => x.SourceAssetCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinationAsset)
            .WithMany()
            .HasForeignKey(x => x.DestinationAssetCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseSourceFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseSourceFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseDestinationFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseDestinationFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InstantQuoteConfiguration : IEntityTypeConfiguration<InstantQuote>
{
    public void Configure(EntityTypeBuilder<InstantQuote> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.InstantPairId, x.Status, x.ExpiresAt });

        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.SourceAmount).HasPrecision(36, 18);
        builder.Property(x => x.DestinationAmount).HasPrecision(36, 18);
        builder.Property(x => x.ProviderRate).HasPrecision(18, 8);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.InstantPair)
            .WithMany(x => x.Quotes)
            .HasForeignKey(x => x.InstantPairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserSourceFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.UserSourceFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserDestinationFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.UserDestinationFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseSourceFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseSourceFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseDestinationFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseDestinationFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ExchangeRate)
            .WithMany()
            .HasForeignKey(x => x.ExchangeRateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InstantTradeConfiguration : IEntityTypeConfiguration<InstantTrade>
{
    public void Configure(EntityTypeBuilder<InstantTrade> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.InstantQuoteId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.InstantPairId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.ReservationId)
            .IsUnique()
            .HasFilter("\"ReservationId\" IS NOT NULL");

        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.SourceAmount).HasPrecision(36, 18);
        builder.Property(x => x.DestinationAmount).HasPrecision(36, 18);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.InstantQuote)
            .WithOne(x => x.Trade)
            .HasForeignKey<InstantTrade>(x => x.InstantQuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InstantPair)
            .WithMany(x => x.Trades)
            .HasForeignKey(x => x.InstantPairId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserSourceFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.UserSourceFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserDestinationFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.UserDestinationFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseSourceFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseSourceFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HouseDestinationFinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.HouseDestinationFinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Reservation)
            .WithMany()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceLedgerTransaction)
            .WithMany()
            .HasForeignKey(x => x.SourceLedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinationLedgerTransaction)
            .WithMany()
            .HasForeignKey(x => x.DestinationLedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
