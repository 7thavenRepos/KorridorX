using KorridorX.Models.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class FinanceTranslationRateConfiguration : IEntityTypeConfiguration<FinanceTranslationRate>
{
    public void Configure(EntityTypeBuilder<FinanceTranslationRate> builder)
    {
        builder.HasIndex(x => new { x.SourceCurrencyCode, x.BaseCurrencyCode, x.RateType, x.EffectiveFrom });
        builder.HasIndex(x => new { x.SourceCurrencyCode, x.BaseCurrencyCode, x.RateType, x.IsActive });
        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.BaseCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.Rate).HasPrecision(24, 10);
        builder.Property(x => x.Source).HasMaxLength(100);
    }
}

public sealed class ProviderInvoiceConfiguration : IEntityTypeConfiguration<ProviderInvoice>
{
    public void Configure(EntityTypeBuilder<ProviderInvoice> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.InvoiceNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.FileHash }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.InvoiceDate });
        builder.Property(x => x.InvoiceNumber).HasMaxLength(150);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.NetAmount).HasPrecision(36, 18);
        builder.Property(x => x.TaxAmount).HasPrecision(36, 18);
        builder.Property(x => x.TotalAmount).HasPrecision(36, 18);
        builder.Property(x => x.MatchedProviderFeeAmount).HasPrecision(36, 18);
        builder.Property(x => x.VarianceAmount).HasPrecision(36, 18);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.FileHash).HasMaxLength(128);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
        builder.HasOne(x => x.JournalEntry).WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProviderInvoiceLineConfiguration : IEntityTypeConfiguration<ProviderInvoiceLine>
{
    public void Configure(EntityTypeBuilder<ProviderInvoiceLine> builder)
    {
        builder.HasIndex(x => x.ProviderInvoiceId);
        builder.HasIndex(x => x.ProviderTransactionRowId);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.NetAmount).HasPrecision(36, 18);
        builder.Property(x => x.TaxAmount).HasPrecision(36, 18);
        builder.Property(x => x.TotalAmount).HasPrecision(36, 18);
        builder.Property(x => x.MatchedProviderFeeAmount).HasPrecision(36, 18);
        builder.Property(x => x.VarianceAmount).HasPrecision(36, 18);
        builder.HasOne(x => x.ProviderInvoice).WithMany(x => x.Lines).HasForeignKey(x => x.ProviderInvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProviderTransaction).WithMany().HasForeignKey(x => x.ProviderTransactionRowId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.HasIndex(x => new { x.JurisdictionCode, x.TaxCode, x.AppliesTo, x.EffectiveFrom });
        builder.HasIndex(x => new { x.JurisdictionCode, x.AppliesTo, x.IsActive });
        builder.Property(x => x.JurisdictionCode).HasMaxLength(10);
        builder.Property(x => x.TaxCode).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(150);
        builder.Property(x => x.RatePercentage).HasPrecision(9, 4);
    }
}

public sealed class FinanceCloseChecklistItemConfiguration : IEntityTypeConfiguration<FinanceCloseChecklistItem>
{
    public void Configure(EntityTypeBuilder<FinanceCloseChecklistItem> builder)
    {
        builder.HasIndex(x => new { x.AccountingPeriodId, x.Code }).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(80);
        builder.Property(x => x.Label).HasMaxLength(250);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FinanceCloseRequestConfiguration : IEntityTypeConfiguration<FinanceCloseRequest>
{
    public void Configure(EntityTypeBuilder<FinanceCloseRequest> builder)
    {
        builder.HasIndex(x => new { x.AccountingPeriodId, x.Status });
        builder.Property(x => x.RequestNote).HasMaxLength(2000);
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId).OnDelete(DeleteBehavior.Cascade);
    }
}
