using KorridorX.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class BusinessInvitationConfiguration : IEntityTypeConfiguration<BusinessInvitation>
{
    public void Configure(EntityTypeBuilder<BusinessInvitation> builder)
    {
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.BusinessProfileId, x.NormalizedEmail });
        builder.HasIndex(x => x.ExpiresAt);

        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsConcurrencyToken();

        builder.HasOne(x => x.BusinessProfile)
            .WithMany(x => x.Invitations)
            .HasForeignKey(x => x.BusinessProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AcceptedByUser)
            .WithMany()
            .HasForeignKey(x => x.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
