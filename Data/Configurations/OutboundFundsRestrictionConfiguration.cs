using KorridorX.Models.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class OutboundFundsRestrictionConfiguration
    : IEntityTypeConfiguration<OutboundFundsRestriction>
{
    public void Configure(
        EntityTypeBuilder<OutboundFundsRestriction> builder)
    {
        builder.HasIndex(x => new
        {
            x.SubjectType,
            x.SubjectId
        })
        .HasFilter("\"IsActive\" = TRUE AND NOT \"IsDeleted\"")
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.IsActive,
            x.SubjectType,
            x.AppliedAt
        });

        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => x.AppliedByUserId);
        builder.HasIndex(x => x.LiftedByUserId);

        builder.Property(x => x.SubjectDisplayName)
            .HasMaxLength(300);

        builder.Property(x => x.InternalReason)
            .HasMaxLength(2000);

        builder.Property(x => x.ExternalReference)
            .HasMaxLength(300);

        builder.Property(x => x.LiftReason)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .IsConcurrencyToken();
    }
}
