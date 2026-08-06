using KorridorX.Models.BusinessTransfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class BusinessPaymentBatchConfiguration : IEntityTypeConfiguration<BusinessPaymentBatch>
{
    public void Configure(EntityTypeBuilder<BusinessPaymentBatch> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.SourceCountryCode).HasMaxLength(10);
        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.ApprovalCount).IsConcurrencyToken();

        builder.Property(x => x.TotalSourceAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalFeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalPayableAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.BusinessProfile)
            .WithMany()
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessPaymentBatchItemConfiguration : IEntityTypeConfiguration<BusinessPaymentBatchItem>
{
    public void Configure(EntityTypeBuilder<BusinessPaymentBatchItem> builder)
    {
        builder.HasIndex(x => x.BusinessPaymentBatchId);
        builder.HasIndex(x => x.BusinessBeneficiaryId);
        builder.HasIndex(x => x.TransferId).IsUnique();
        builder.HasIndex(x => new { x.BusinessPaymentBatchId, x.RowNumber }).IsUnique();

        builder.Property(x => x.ExternalReference).HasMaxLength(100);
        builder.Property(x => x.DestinationCountryCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.PurposeNote).HasMaxLength(500);
        builder.Property(x => x.ValidationErrors).HasMaxLength(4000);

        builder.Property(x => x.SourceAmount).HasPrecision(18, 2);
        builder.Property(x => x.DestinationAmount).HasPrecision(18, 2);
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalPayableAmount).HasPrecision(18, 2);
        builder.Property(x => x.CustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.ProviderRate).HasPrecision(18, 8);

        builder.HasOne(x => x.BusinessPaymentBatch)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BusinessPaymentBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.BusinessBeneficiary)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficiaryId)
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

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessApprovalConfiguration : IEntityTypeConfiguration<BusinessApproval>
{
    public void Configure(EntityTypeBuilder<BusinessApproval> builder)
    {
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.BusinessPaymentBatchId);
        builder.HasIndex(x => x.ActionedAt);
        builder.HasIndex(x => new { x.TransferId, x.ActionedByUserId }).IsUnique();
        builder.HasIndex(x => new { x.BusinessPaymentBatchId, x.ActionedByUserId }).IsUnique();

        builder.Property(x => x.Comment).HasMaxLength(1000);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.BusinessPaymentBatch)
            .WithMany(x => x.Approvals)
            .HasForeignKey(x => x.BusinessPaymentBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActionedByUser)
            .WithMany()
            .HasForeignKey(x => x.ActionedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
