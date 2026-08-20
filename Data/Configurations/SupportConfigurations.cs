using KorridorX.Models.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasIndex(x => x.BusinessProfileId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.AssignedToUserId);
        builder.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt });
        builder.HasIndex(x => new { x.IsSlaBreached, x.Status });
        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Subject).HasMaxLength(250);
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.EscalationLevel).IsConcurrencyToken();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerProfile).WithMany().HasForeignKey(x => x.CustomerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BusinessProfile).WithMany().HasForeignKey(x => x.BusinessProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.HasIndex(x => new { x.SupportTicketId, x.CreatedAt });
        builder.Property(x => x.Body).HasMaxLength(8000);
        builder.HasOne(x => x.SupportTicket).WithMany(x => x.Messages).HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AuthorUser).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferDisputeConfiguration : IEntityTypeConfiguration<TransferDispute>
{
    public void Configure(EntityTypeBuilder<TransferDispute> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.SupportTicketId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(4000);
        builder.Property(x => x.ResolutionNote).HasMaxLength(4000);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.RequestedRefundAmount).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupportTicket).WithMany().HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerProfile).WithMany().HasForeignKey(x => x.CustomerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BusinessProfile).WithMany().HasForeignKey(x => x.BusinessProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferInvestigationConfiguration : IEntityTypeConfiguration<TransferInvestigation>
{
    public void Configure(EntityTypeBuilder<TransferInvestigation> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.TransferDisputeId);
        builder.HasIndex(x => x.SupportTicketId);
        builder.HasIndex(x => new { x.Status, x.DueAt });
        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.Property(x => x.Findings).HasMaxLength(8000);
        builder.Property(x => x.ProviderCaseReference).HasMaxLength(200);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TransferDispute).WithMany(x => x.Investigations).HasForeignKey(x => x.TransferDisputeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupportTicket).WithMany().HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupportEvidenceConfiguration : IEntityTypeConfiguration<SupportEvidence>
{
    public void Configure(EntityTypeBuilder<SupportEvidence> builder)
    {
        builder.HasIndex(x => x.SupportTicketId);
        builder.HasIndex(x => x.TransferDisputeId);
        builder.HasIndex(x => x.TransferInvestigationId);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.MimeType).HasMaxLength(100);
        builder.Property(x => x.StorageKey).HasMaxLength(1000);
        builder.Property(x => x.StorageUrl).HasMaxLength(2000);
        builder.HasOne(x => x.SupportTicket).WithMany(x => x.Evidence).HasForeignKey(x => x.SupportTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TransferDispute).WithMany(x => x.Evidence).HasForeignKey(x => x.TransferDisputeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TransferInvestigation).WithMany(x => x.Evidence).HasForeignKey(x => x.TransferInvestigationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
