using KorridorX.Models.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class ScreeningRecordConfiguration : IEntityTypeConfiguration<ScreeningRecord>
{
    public void Configure(EntityTypeBuilder<ScreeningRecord> builder)
    {
        builder.HasIndex(x => x.SubjectType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsBlocking);
        builder.HasIndex(x => x.ScreenedAt);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.ProviderReference);
        builder.HasIndex(x => new { x.CustomerProfileId, x.SubjectType, x.ScreenedAt });
        builder.HasIndex(x => new { x.BusinessProfileId, x.SubjectType, x.ScreenedAt });
        builder.HasIndex(x => new { x.RecipientId, x.SubjectType, x.ScreenedAt });
        builder.HasIndex(x => new { x.BusinessBeneficiaryId, x.SubjectType, x.ScreenedAt });
        builder.HasIndex(x => new { x.BusinessBeneficialOwnerId, x.SubjectType, x.ScreenedAt });
        builder.HasIndex(x => new { x.TransferId, x.ScreenedAt });

        builder.Property(x => x.SubjectName).HasMaxLength(300);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.RegistrationNumberLastFour).HasMaxLength(4);
        builder.Property(x => x.ProviderCode).HasMaxLength(100);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.HighestMatchScore).HasPrecision(7, 2);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

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
        builder.HasOne(x => x.BusinessBeneficialOwner)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficialOwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ScreeningMatchConfiguration : IEntityTypeConfiguration<ScreeningMatch>
{
    public void Configure(EntityTypeBuilder<ScreeningMatch> builder)
    {
        builder.HasIndex(x => x.ScreeningRecordId);
        builder.HasIndex(x => x.WatchlistType);
        builder.HasIndex(x => x.ProviderMatchId);
        builder.HasIndex(x => x.MatchScore);
        builder.HasIndex(x => x.IsFalsePositive);

        builder.Property(x => x.ListName).HasMaxLength(300);
        builder.Property(x => x.MatchedName).HasMaxLength(300);
        builder.Property(x => x.ProviderMatchId).HasMaxLength(200);
        builder.Property(x => x.MatchScore).HasPrecision(7, 2);
        builder.Property(x => x.MatchReason).HasMaxLength(1000);
        builder.Property(x => x.CountryCode).HasMaxLength(10);

        builder.HasOne(x => x.ScreeningRecord)
            .WithMany(x => x.Matches)
            .HasForeignKey(x => x.ScreeningRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ComplianceCaseConfiguration : IEntityTypeConfiguration<ComplianceCase>
{
    public void Configure(EntityTypeBuilder<ComplianceCase> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.CaseType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.IsBlocking);
        builder.HasIndex(x => x.AssignedToUserId);
        builder.HasIndex(x => x.OpenedAt);
        builder.HasIndex(x => x.DueAt);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.BusinessBeneficialOwnerId);
        builder.HasIndex(x => x.ScreeningRecordId);
        builder.HasIndex(x => x.AmlFlagId);
        builder.HasIndex(x => new { x.Status, x.IsBlocking, x.Priority, x.OpenedAt });

        builder.Property(x => x.Reference).HasMaxLength(50);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.DecisionReason).HasMaxLength(4000);
        builder.Property(x => x.Status).IsConcurrencyToken();

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
        builder.HasOne(x => x.BusinessBeneficialOwner)
            .WithMany()
            .HasForeignKey(x => x.BusinessBeneficialOwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScreeningRecord)
            .WithMany()
            .HasForeignKey(x => x.ScreeningRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AmlFlag)
            .WithMany()
            .HasForeignKey(x => x.AmlFlagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ComplianceCaseNoteConfiguration : IEntityTypeConfiguration<ComplianceCaseNote>
{
    public void Configure(EntityTypeBuilder<ComplianceCaseNote> builder)
    {
        builder.HasIndex(x => x.ComplianceCaseId);
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.Note).HasMaxLength(4000);

        builder.HasOne(x => x.ComplianceCase)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.ComplianceCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ComplianceCaseEvidenceConfiguration : IEntityTypeConfiguration<ComplianceCaseEvidence>
{
    public void Configure(EntityTypeBuilder<ComplianceCaseEvidence> builder)
    {
        builder.HasIndex(x => x.ComplianceCaseId);
        builder.HasIndex(x => x.EvidenceType);
        builder.HasIndex(x => x.AddedByUserId);
        builder.HasIndex(x => x.CreatedAt);

        builder.Property(x => x.EvidenceType).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Source).HasMaxLength(200);
        builder.Property(x => x.ExternalReference).HasMaxLength(500);
        builder.Property(x => x.StorageKey).HasMaxLength(1000);

        builder.HasOne(x => x.ComplianceCase)
            .WithMany(x => x.Evidence)
            .HasForeignKey(x => x.ComplianceCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
