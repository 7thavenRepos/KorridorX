using KorridorX.Models.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class AccountingAccountConfiguration : IEntityTypeConfiguration<AccountingAccount>
{
    public void Configure(EntityTypeBuilder<AccountingAccount> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Type, x.IsActive });
        builder.Property(x => x.Code).HasMaxLength(30);
        builder.Property(x => x.Name).HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(1000);
    }
}

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => new { x.StartsAt, x.EndsAt });
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.CloseNote).HasMaxLength(2000);
    }
}

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.SourceKey).IsUnique();
        builder.HasIndex(x => new { x.AccountingPeriodId, x.EntryDate });
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.SourceKey).HasMaxLength(200);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ReversalReason).HasMaxLength(2000);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversalOfJournalEntry).WithMany().HasForeignKey(x => x.ReversalOfJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.HasIndex(x => x.JournalEntryId);
        builder.HasIndex(x => x.AccountingAccountId);
        builder.Property(x => x.DebitAmount).HasPrecision(36, 18);
        builder.Property(x => x.CreditAmount).HasPrecision(36, 18);
        builder.Property(x => x.Narrative).HasMaxLength(1000);
        builder.HasOne(x => x.JournalEntry).WithMany(x => x.Lines).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AccountingAccount).WithMany().HasForeignKey(x => x.AccountingAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
