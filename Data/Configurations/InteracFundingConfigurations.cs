using KorridorX.Models.FinancialCore;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class InteracReceivingRouteMap : IEntityTypeConfiguration<InteracReceivingRoute>
{
    public void Configure(EntityTypeBuilder<InteracReceivingRoute> b)
    {
        b.ToTable("InteracReceivingRoutes"); b.HasKey(x => x.Id);
        b.Property(x => x.Environment).HasMaxLength(50); b.Property(x => x.ConnectionKey).HasMaxLength(64);
        b.Property(x => x.ProviderWalletId).HasMaxLength(150); b.Property(x => x.ProviderBusinessId).HasMaxLength(150);
        b.Property(x => x.ProviderRegistrationId).HasMaxLength(150);
        b.Property(x => x.ReceivingEmail).HasMaxLength(254); b.Property(x => x.NormalizedReceivingEmail).HasMaxLength(254);
        b.Property(x => x.DisplayName).HasMaxLength(120); b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ProviderStatus).HasMaxLength(30); b.Property(x => x.FailureReason).HasMaxLength(1000);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<ProviderWalletConfiguration>().WithMany().HasForeignKey(x => x.ProviderWalletConfigurationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Environment, x.ConnectionKey, x.NormalizedReceivingEmail }).IsUnique();
        b.HasIndex(x => new { x.Environment, x.ConnectionKey, x.ProviderRegistrationId }).IsUnique();
        b.HasIndex(x => new { x.Environment, x.ConnectionKey }).IsUnique().HasFilter("\"Status\" = 'Active'")
            .HasDatabaseName("UX_InteracRoute_ActiveConnection");
    }
}

public sealed class InteracSendingEmailMap : IEntityTypeConfiguration<InteracSendingEmail>
{
    public void Configure(EntityTypeBuilder<InteracSendingEmail> b)
    {
        b.ToTable("InteracSendingEmails"); b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(254); b.Property(x => x.NormalizedEmail).HasMaxLength(254);
        b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.CodeHash).HasMaxLength(128);
        b.Property(x => x.CodeSalt).HasMaxLength(128); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<InteracReceivingRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RouteId, x.FinancialAccountId, x.NormalizedEmail }).IsUnique();
    }
}

public sealed class InteracSenderOwnershipMap : IEntityTypeConfiguration<InteracSenderOwnership>
{
    public void Configure(EntityTypeBuilder<InteracSenderOwnership> b)
    {
        b.ToTable("InteracSenderOwnerships"); b.HasKey(x => x.Id);
        b.Property(x => x.NormalizedEmail).HasMaxLength(254);
        b.HasOne<InteracReceivingRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RouteId, x.NormalizedEmail }).IsUnique().HasDatabaseName("UX_InteracSender_RouteOwnership");
    }
}

public sealed class ProviderCollectionCreditClaimMap : IEntityTypeConfiguration<ProviderCollectionCreditClaim>
{
    public void Configure(EntityTypeBuilder<ProviderCollectionCreditClaim> b)
    {
        b.ToTable("ProviderCollectionCreditClaims"); b.HasKey(x => x.Id);
        b.Property(x => x.ProviderCode).HasMaxLength(50); b.Property(x => x.Environment).HasMaxLength(50);
        b.Property(x => x.ConnectionKey).HasMaxLength(64); b.Property(x => x.ProviderTransactionId).HasMaxLength(150);
        b.Property(x => x.CurrencyCode).HasMaxLength(20); b.Property(x => x.Amount).HasPrecision(36, 18);
        b.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProviderCode, x.Environment, x.ConnectionKey, x.ProviderTransactionId }).IsUnique()
            .HasDatabaseName("UX_ProviderCollectionCredit_Transaction");
    }
}

public sealed class InteracDepositMap : IEntityTypeConfiguration<InteracDeposit>
{
    public void Configure(EntityTypeBuilder<InteracDeposit> b)
    {
        b.ToTable("InteracDeposits"); b.HasKey(x => x.Id);
        b.Property(x => x.Environment).HasMaxLength(50); b.Property(x => x.ConnectionKey).HasMaxLength(64);
        b.Property(x => x.ProviderTransactionId).HasMaxLength(150); b.Property(x => x.ProviderReference).HasMaxLength(150);
        b.Property(x => x.Status).HasMaxLength(30); b.Property(x => x.ReviewReason).HasMaxLength(1000);
        b.Property(x => x.SenderEmail).HasMaxLength(254); b.Property(x => x.ReceivingEmail).HasMaxLength(254);
        b.Property(x => x.CurrencyCode).HasMaxLength(20);
        b.Property(x => x.GrossAmount).HasPrecision(36, 18); b.Property(x => x.NetAmount).HasPrecision(36, 18);
        b.Property(x => x.FeeAmount).HasPrecision(36, 18);
        b.HasOne<InteracReceivingRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Environment, x.ConnectionKey, x.ProviderTransactionId }).IsUnique();
        b.HasIndex(x => new { x.Environment, x.Status, x.ReceivedAt });
    }
}

public sealed class InteracDepositReviewNoteMap : IEntityTypeConfiguration<InteracDepositReviewNote>
{
    public void Configure(EntityTypeBuilder<InteracDepositReviewNote> b)
    {
        b.ToTable("InteracDepositReviewNotes"); b.HasKey(x => x.Id); b.Property(x => x.Note).HasMaxLength(2000);
        b.HasOne<InteracDeposit>().WithMany().HasForeignKey(x => x.DepositId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DepositId, x.CreatedAt });
    }
}
