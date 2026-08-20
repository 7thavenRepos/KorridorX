using KorridorX.Models.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.Purpose);
        builder.HasIndex(x => x.FinancialAccountId);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        builder.HasIndex(x => new { x.ContextEntityType, x.ContextEntityId });        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProviderCollectionId);
        builder.HasIndex(x => x.ProviderReference);
        builder.HasIndex(x => x.ProviderRefundId);

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderCollectionId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(1000);
        builder.Property(x => x.VirtualAccountNumber).HasMaxLength(100);
        builder.Property(x => x.VirtualAccountBankName).HasMaxLength(150);
        builder.Property(x => x.VirtualAccountName).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.ProviderRefundId).HasMaxLength(150);
        builder.Property(x => x.ProviderRefundReference).HasMaxLength(150);
        builder.Property(x => x.RefundReason).HasMaxLength(250);
        builder.Property(x => x.RefundFailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.ContextEntityType).HasMaxLength(100);
        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);    }
}

public class CollectionAttemptConfiguration : IEntityTypeConfiguration<CollectionAttempt>
{
    public void Configure(EntityTypeBuilder<CollectionAttempt> builder)
    {
        builder.HasIndex(x => x.CollectionId);
        builder.HasIndex(x => x.AttemptedAt);

        builder.Property(x => x.ProviderRequestId).HasMaxLength(150);
        builder.Property(x => x.ProviderResponseId).HasMaxLength(150);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasOne(x => x.Collection)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.Purpose);
        builder.HasIndex(x => x.FinancialAccountId);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        builder.HasIndex(x => new { x.ContextEntityType, x.ContextEntityId });        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProviderPayoutId);
        builder.HasIndex(x => x.ProviderReference);

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderPayoutId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.InteracQuestion).HasMaxLength(500);
        builder.Property(x => x.InteracAnswer).HasMaxLength(500);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.ContextEntityType).HasMaxLength(100);
        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FinancialAccount)
            .WithMany()
            .HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);    }
}

public class PayoutAttemptConfiguration : IEntityTypeConfiguration<PayoutAttempt>
{
    public void Configure(EntityTypeBuilder<PayoutAttempt> builder)
    {
        builder.HasIndex(x => x.PayoutId);
        builder.HasIndex(x => x.AttemptedAt);

        builder.Property(x => x.ProviderRequestId).HasMaxLength(150);
        builder.Property(x => x.ProviderResponseId).HasMaxLength(150);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasOne(x => x.Payout)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.PayoutId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}