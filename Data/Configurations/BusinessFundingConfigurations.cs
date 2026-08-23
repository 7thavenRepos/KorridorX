using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId, x.AssetCode, x.AccountType }).IsUnique();
        builder.HasIndex(x => x.AccountCode).IsUnique();
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.AccountCode).HasMaxLength(80);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.SettledBalance).HasPrecision(36, 18).IsConcurrencyToken();
        builder.Property(x => x.AvailableBalance).HasPrecision(36, 18).IsConcurrencyToken();
        builder.Property(x => x.HeldBalance).HasPrecision(36, 18).IsConcurrencyToken();

        builder.HasOne(x => x.Asset)
            .WithMany()
            .HasForeignKey(x => x.AssetCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.PostedAt);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        builder.HasIndex(x => new { x.ContextEntityType, x.ContextEntityId });
        builder.HasIndex(x => new { x.IdempotencyScope, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(x => x.ReversalOfTransactionId).IsUnique();
        builder.HasIndex(x => x.ReversedByTransactionId).IsUnique();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyScope).HasMaxLength(200);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.IdempotencyRequestHash).HasMaxLength(128);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.ContextEntityType).HasMaxLength(100);
        builder.Property(x => x.ReversalReason).HasMaxLength(1000);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.Asset)
            .WithMany()
            .HasForeignKey(x => x.AssetCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReversalOfTransaction)
            .WithOne()
            .HasForeignKey<LedgerTransaction>(x => x.ReversalOfTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReversedByTransaction)
            .WithOne()
            .HasForeignKey<LedgerTransaction>(x => x.ReversedByTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LedgerPostingConfiguration : IEntityTypeConfiguration<LedgerPosting>
{
    public void Configure(EntityTypeBuilder<LedgerPosting> builder)
    {
        builder.HasIndex(x => x.LedgerTransactionId);
        builder.HasIndex(x => x.FinancialAccountId);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.AccountBalanceAfter).HasPrecision(36, 18);

        builder.HasOne(x => x.LedgerTransaction)
            .WithMany(x => x.Postings)
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FinancialAccount)
            .WithMany(x => x.LedgerPostings)
            .HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinancialReservationConfiguration : IEntityTypeConfiguration<FinancialReservation>
{
    public void Configure(EntityTypeBuilder<FinancialReservation> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        builder.HasIndex(x => x.FinancialAccountId);
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.ContextEntityType).HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.CapturedAmount).HasPrecision(36, 18);
        builder.Property(x => x.ReleasedAmount).HasPrecision(36, 18);
        builder.Property(x => x.ReleaseReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.FinancialAccount)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
