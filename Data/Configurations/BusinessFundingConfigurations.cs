using KorridorX.Models.BusinessFunding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class BusinessWalletConfiguration : IEntityTypeConfiguration<BusinessWallet>
{
    public void Configure(EntityTypeBuilder<BusinessWallet> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.CurrencyCode }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.SettledBalance).HasPrecision(36, 18).IsConcurrencyToken();
        builder.Property(x => x.AvailableBalance).HasPrecision(36, 18).IsConcurrencyToken();
        builder.Property(x => x.HeldBalance).HasPrecision(36, 18).IsConcurrencyToken();

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessLedgerTransactionConfiguration : IEntityTypeConfiguration<BusinessLedgerTransaction>
{
    public void Configure(EntityTypeBuilder<BusinessLedgerTransaction> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.BusinessPaymentBatchId);
        builder.HasIndex(x => x.CollectionId);
        builder.HasIndex(x => x.PostedAt);
        builder.HasIndex(x => new { x.BusinessProfileId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(x => x.ReversalOfTransactionId).IsUnique();
        builder.HasIndex(x => x.ReversedByTransactionId).IsUnique();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.IdempotencyRequestHash).HasMaxLength(128);
        builder.Property(x => x.ReversalReason).HasMaxLength(1000);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessPaymentBatch)
            .WithMany()
            .HasForeignKey(x => x.BusinessPaymentBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Collection)
            .WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessLedgerEntryConfiguration : IEntityTypeConfiguration<BusinessLedgerEntry>
{
    public void Configure(EntityTypeBuilder<BusinessLedgerEntry> builder)
    {
        builder.HasIndex(x => x.BusinessLedgerTransactionId);
        builder.HasIndex(x => x.BusinessWalletId);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.AccountBalanceAfter).HasPrecision(36, 18);

        builder.HasOne(x => x.BusinessLedgerTransaction)
            .WithMany(x => x.Entries)
            .HasForeignKey(x => x.BusinessLedgerTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.BusinessWallet)
            .WithMany(x => x.LedgerEntries)
            .HasForeignKey(x => x.BusinessWalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessWalletReservationConfiguration : IEntityTypeConfiguration<BusinessWalletReservation>
{
    public void Configure(EntityTypeBuilder<BusinessWalletReservation> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TransferId).IsUnique();
        builder.HasIndex(x => x.BusinessWalletId);
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.ReleaseReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.BusinessWallet)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.BusinessWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessPaymentBatch)
            .WithMany()
            .HasForeignKey(x => x.BusinessPaymentBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
