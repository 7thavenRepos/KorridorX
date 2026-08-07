using KorridorX.Models.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class RegulatoryReportConfiguration : IEntityTypeConfiguration<RegulatoryReport>
{
    public void Configure(EntityTypeBuilder<RegulatoryReport> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.ComplianceCaseId);
        builder.HasIndex(x => x.ReportType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.JurisdictionCode);
        builder.HasIndex(x => x.FilingReference);
        builder.HasIndex(x => x.FilingDueAt);
        builder.HasIndex(x => new { x.Status, x.FilingDueAt });

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.JurisdictionCode).HasMaxLength(10);
        builder.Property(x => x.RegulatoryAuthority).HasMaxLength(200);
        builder.Property(x => x.FilingReference).HasMaxLength(200);
        builder.Property(x => x.Narrative).HasMaxLength(12000);
        builder.Property(x => x.SuspicionReason).HasMaxLength(4000);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.RejectionReason).HasMaxLength(4000);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasOne(x => x.ComplianceCase)
            .WithMany()
            .HasForeignKey(x => x.ComplianceCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DataRetentionPolicyConfiguration : IEntityTypeConfiguration<DataRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<DataRetentionPolicy> builder)
    {
        builder.HasIndex(x => x.RecordType);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.RecordType, x.IsActive });

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
    }
}

public sealed class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
{
    public void Configure(EntityTypeBuilder<LegalHold> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ComplianceCaseId);
        builder.HasIndex(x => x.RegulatoryReportId);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => new { x.Status, x.EffectiveAt });

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Reason).HasMaxLength(4000);
        builder.Property(x => x.EntityName).HasMaxLength(150);
        builder.Property(x => x.EntityId).HasMaxLength(100);
        builder.Property(x => x.ReleaseReason).HasMaxLength(4000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.ComplianceCase)
            .WithMany()
            .HasForeignKey(x => x.ComplianceCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RegulatoryReport)
            .WithMany()
            .HasForeignKey(x => x.RegulatoryReportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RetentionExecutionLogConfiguration : IEntityTypeConfiguration<RetentionExecutionLog>
{
    public void Configure(EntityTypeBuilder<RetentionExecutionLog> builder)
    {
        builder.HasIndex(x => x.DataRetentionPolicyId);
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.CompletedAt);
        builder.HasIndex(x => x.IsDryRun);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(x => x.DataRetentionPolicy)
            .WithMany()
            .HasForeignKey(x => x.DataRetentionPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
