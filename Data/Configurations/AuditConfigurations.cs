using KorridorX.Models.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.EntityName);
        builder.HasIndex(x => x.EntityId);
        builder.HasIndex(x => x.OccurredAt);

        builder.Property(x => x.Action).HasMaxLength(150);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.EntityName).HasMaxLength(150);
        builder.Property(x => x.EntityId).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
    }
}