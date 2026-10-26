using KorridorX.Models.AccountClosure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class AccountClosureRequestConfiguration : IEntityTypeConfiguration<AccountClosureRequest>
{
    public void Configure(EntityTypeBuilder<AccountClosureRequest> builder)
    {
        builder.HasIndex(x => new { x.Scope, x.SubjectId })
            .IsUnique()
            // One open closure request per account; withdrawal permits a new request.
            // The constraint protects races across API instances.
            .HasFilter("\"IsDeleted\" = false AND \"Status\" = 1");
        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasIndex(x => x.RequestedAtUtc);
    }
}
