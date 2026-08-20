using KorridorX.Models.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.RecipientId);
        builder.HasIndex(x => x.BusinessBeneficiaryId);
        builder.HasIndex(x => x.TransferQuoteId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ApprovalStatus);
        builder.HasIndex(x => x.ProviderTransferId);
        builder.HasIndex(x => x.ProviderReference);
        builder.HasIndex(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.CustomerProfileId, x.CreatedAt });
        builder.HasIndex(x => new { x.BusinessProfileId, x.CreatedAt });
        builder.HasIndex(x => new { x.RecipientId, x.SourceAmount, x.CreatedAt });
        builder.HasIndex(x => new { x.BusinessBeneficiaryId, x.SourceAmount, x.CreatedAt });
        builder.HasIndex(x => x.IsComplianceHold);
        builder.HasIndex(x => x.IsOperationalHold);
        builder.HasIndex(x => new { x.RiskDecision, x.RiskLevel });

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.PurposeNote).HasMaxLength(500);
        builder.Property(x => x.SourceCountryCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCountryCode).HasMaxLength(10);
        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(20);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(20);
        builder.Property(x => x.FeeCurrencyCode).HasMaxLength(20);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderTransferId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.ApprovalRejectionReason).HasMaxLength(1000);
        builder.Property(x => x.ComplianceHoldReason).HasMaxLength(1000);
        builder.Property(x => x.OperationalHoldReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.IsComplianceHold).IsConcurrencyToken();
        builder.Property(x => x.IsOperationalHold).IsConcurrencyToken();
        builder.Property(x => x.ApprovalCount).IsConcurrencyToken();

        builder.Property(x => x.SourceAmount).HasPrecision(36, 18);
        builder.Property(x => x.DestinationAmount).HasPrecision(36, 18);
        builder.Property(x => x.FeeAmount).HasPrecision(36, 18);
        builder.Property(x => x.TotalPayableAmount).HasPrecision(36, 18);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.ProviderRate).HasPrecision(18, 8);

        builder.HasOne(x => x.CustomerProfile)
            .WithMany()
            .HasForeignKey(x => x.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Recipient)
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessBeneficiary)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficiaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RecipientBankAccount)
            .WithMany()
            .HasForeignKey(x => x.RecipientBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RecipientMobileWallet)
            .WithMany()
            .HasForeignKey(x => x.RecipientMobileWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessBeneficiaryBankAccount)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficiaryBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessBeneficiaryMobileWallet)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficiaryMobileWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransferQuote)
            .WithMany()
            .HasForeignKey(x => x.TransferQuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransferStatusHistoryConfiguration : IEntityTypeConfiguration<TransferStatusHistory>
{
    public void Configure(EntityTypeBuilder<TransferStatusHistory> builder)
    {
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.ChangedAt);

        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.Source).HasMaxLength(100);

        builder.HasOne(x => x.Transfer)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransferTimelineEventConfiguration : IEntityTypeConfiguration<TransferTimelineEvent>
{
    public void Configure(EntityTypeBuilder<TransferTimelineEvent> builder)
    {
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.OccurredAt);

        builder.Property(x => x.EventType).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.Transfer)
            .WithMany(x => x.TimelineEvents)
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
