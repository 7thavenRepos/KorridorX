using KorridorX.Models.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class MarketplacePairConfiguration : IEntityTypeConfiguration<MarketplacePair>
{
    public void Configure(EntityTypeBuilder<MarketplacePair> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.BaseAssetCode, x.QuoteAssetCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.BaseAssetCode).HasMaxLength(20);
        builder.Property(x => x.QuoteAssetCode).HasMaxLength(20);
        builder.Property(x => x.MinimumOrderQuantity).HasPrecision(36, 18);
        builder.Property(x => x.MaximumOrderQuantity).HasPrecision(36, 18);
        builder.Property(x => x.QuantityIncrement).HasPrecision(36, 18);
        builder.Property(x => x.PriceIncrement).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.BaseAsset).WithMany().HasForeignKey(x => x.BaseAssetCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteAsset).WithMany().HasForeignKey(x => x.QuoteAssetCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TradeOrderConfiguration : IEntityTypeConfiguration<TradeOrder>
{
    public void Configure(EntityTypeBuilder<TradeOrder> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.MarketplacePairId, x.Side, x.Status, x.LimitPrice, x.CreatedAt });
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId, x.Status });
        builder.HasIndex(x => x.ReservationId).IsUnique().HasFilter("\"ReservationId\" IS NOT NULL");
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.OriginalQuantity).HasPrecision(36, 18);
        builder.Property(x => x.RemainingQuantity).HasPrecision(36, 18).IsConcurrencyToken();
        builder.Property(x => x.FilledQuantity).HasPrecision(36, 18);
        builder.Property(x => x.LimitPrice).HasPrecision(36, 18);
        builder.Property(x => x.AverageFillPrice).HasPrecision(36, 18);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.MarketplacePair).WithMany(x => x.Orders).HasForeignKey(x => x.MarketplacePairId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BaseFinancialAccount).WithMany().HasForeignKey(x => x.BaseFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteFinancialAccount).WithMany().HasForeignKey(x => x.QuoteFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TradeMatchConfiguration : IEntityTypeConfiguration<TradeMatch>
{
    public void Configure(EntityTypeBuilder<TradeMatch> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.MarketplacePairId, x.Status, x.MatchedAt });
        builder.HasIndex(x => x.BuyOrderId);
        builder.HasIndex(x => x.SellOrderId);
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Price).HasPrecision(36, 18);
        builder.Property(x => x.BaseQuantity).HasPrecision(36, 18);
        builder.Property(x => x.QuoteQuantity).HasPrecision(36, 18);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.MarketplacePair).WithMany(x => x.Matches).HasForeignKey(x => x.MarketplacePairId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuyOrder).WithMany(x => x.BuyMatches).HasForeignKey(x => x.BuyOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SellOrder).WithMany(x => x.SellMatches).HasForeignKey(x => x.SellOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => x.TradeMatchId).IsUnique();
        builder.HasIndex(x => new { x.MarketplacePairId, x.Status, x.CreatedAt });
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Price).HasPrecision(36, 18);
        builder.Property(x => x.BaseQuantity).HasPrecision(36, 18);
        builder.Property(x => x.QuoteQuantity).HasPrecision(36, 18);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.Status).IsConcurrencyToken();

        builder.HasOne(x => x.TradeMatch).WithOne(x => x.Trade).HasForeignKey<Trade>(x => x.TradeMatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MarketplacePair).WithMany().HasForeignKey(x => x.MarketplacePairId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuyerBaseFinancialAccount).WithMany().HasForeignKey(x => x.BuyerBaseFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuyerQuoteFinancialAccount).WithMany().HasForeignKey(x => x.BuyerQuoteFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SellerBaseFinancialAccount).WithMany().HasForeignKey(x => x.SellerBaseFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SellerQuoteFinancialAccount).WithMany().HasForeignKey(x => x.SellerQuoteFinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BaseLedgerTransaction).WithMany().HasForeignKey(x => x.BaseLedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteLedgerTransaction).WithMany().HasForeignKey(x => x.QuoteLedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}


public class BusinessTradingRfqConfiguration : IEntityTypeConfiguration<BusinessTradingRfq>
{
    public void Configure(EntityTypeBuilder<BusinessTradingRfq> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.RequesterOwnerType, x.RequesterOwnerId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.CounterpartyOwnerType, x.CounterpartyOwnerId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.TradeMatchId).IsUnique().HasFilter("\"TradeMatchId\" IS NOT NULL");
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Quantity).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.MarketplacePair).WithMany().HasForeignKey(x => x.MarketplacePairId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessTradingRfqQuoteConfiguration : IEntityTypeConfiguration<BusinessTradingRfqQuote>
{
    public void Configure(EntityTypeBuilder<BusinessTradingRfqQuote> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.BusinessTradingRfqId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.ResponderOwnerType, x.ResponderOwnerId, x.Status });
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.Price).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne(x => x.BusinessTradingRfq).WithMany(x => x.Quotes).HasForeignKey(x => x.BusinessTradingRfqId).OnDelete(DeleteBehavior.Restrict);
    }
}
