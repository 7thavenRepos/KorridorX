using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_MarketplaceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplacePairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BaseAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuoteAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MinimumOrderQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    MaximumOrderQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    QuantityIncrement = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    PriceIncrement = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplacePairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplacePairs_Assets_BaseAssetCode",
                        column: x => x.BaseAssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplacePairs_Assets_QuoteAssetCode",
                        column: x => x.QuoteAssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MarketplacePairId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    OrderType = table.Column<int>(type: "integer", nullable: false),
                    TimeInForce = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    FilledQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    LimitPrice = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    AverageFillPrice = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeOrders_FinancialAccounts_BaseFinancialAccountId",
                        column: x => x.BaseFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeOrders_FinancialAccounts_QuoteFinancialAccountId",
                        column: x => x.QuoteFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeOrders_FinancialReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "FinancialReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeOrders_MarketplacePairs_MarketplacePairId",
                        column: x => x.MarketplacePairId,
                        principalTable: "MarketplacePairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MarketplacePairId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MakerOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TakerOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    QuoteQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettlementStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeMatches_MarketplacePairs_MarketplacePairId",
                        column: x => x.MarketplacePairId,
                        principalTable: "MarketplacePairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeMatches_TradeOrders_BuyOrderId",
                        column: x => x.BuyOrderId,
                        principalTable: "TradeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeMatches_TradeOrders_SellOrderId",
                        column: x => x.SellOrderId,
                        principalTable: "TradeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TradeMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplacePairId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerBaseFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerQuoteFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerBaseFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerQuoteFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    QuoteQuantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BaseLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuoteLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_FinancialAccounts_BuyerBaseFinancialAccountId",
                        column: x => x.BuyerBaseFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_FinancialAccounts_BuyerQuoteFinancialAccountId",
                        column: x => x.BuyerQuoteFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_FinancialAccounts_SellerBaseFinancialAccountId",
                        column: x => x.SellerBaseFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_FinancialAccounts_SellerQuoteFinancialAccountId",
                        column: x => x.SellerQuoteFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_LedgerTransactions_BaseLedgerTransactionId",
                        column: x => x.BaseLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_LedgerTransactions_QuoteLedgerTransactionId",
                        column: x => x.QuoteLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_MarketplacePairs_MarketplacePairId",
                        column: x => x.MarketplacePairId,
                        principalTable: "MarketplacePairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_TradeMatches_TradeMatchId",
                        column: x => x.TradeMatchId,
                        principalTable: "TradeMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePairs_BaseAssetCode_QuoteAssetCode",
                table: "MarketplacePairs",
                columns: new[] { "BaseAssetCode", "QuoteAssetCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePairs_Code",
                table: "MarketplacePairs",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePairs_QuoteAssetCode",
                table: "MarketplacePairs",
                column: "QuoteAssetCode");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePairs_Status",
                table: "MarketplacePairs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TradeMatches_BuyOrderId",
                table: "TradeMatches",
                column: "BuyOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeMatches_MarketplacePairId_Status_MatchedAt",
                table: "TradeMatches",
                columns: new[] { "MarketplacePairId", "Status", "MatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeMatches_Reference",
                table: "TradeMatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeMatches_SellOrderId",
                table: "TradeMatches",
                column: "SellOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_BaseFinancialAccountId",
                table: "TradeOrders",
                column: "BaseFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_MarketplacePairId_Side_Status_LimitPrice_Create~",
                table: "TradeOrders",
                columns: new[] { "MarketplacePairId", "Side", "Status", "LimitPrice", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_OwnerType_OwnerId_Status",
                table: "TradeOrders",
                columns: new[] { "OwnerType", "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_QuoteFinancialAccountId",
                table: "TradeOrders",
                column: "QuoteFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_Reference",
                table: "TradeOrders",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_ReservationId",
                table: "TradeOrders",
                column: "ReservationId",
                unique: true,
                filter: "\"ReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_BaseLedgerTransactionId",
                table: "Trades",
                column: "BaseLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_BuyerBaseFinancialAccountId",
                table: "Trades",
                column: "BuyerBaseFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_BuyerQuoteFinancialAccountId",
                table: "Trades",
                column: "BuyerQuoteFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_MarketplacePairId_Status_CreatedAt",
                table: "Trades",
                columns: new[] { "MarketplacePairId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_QuoteLedgerTransactionId",
                table: "Trades",
                column: "QuoteLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Reference",
                table: "Trades",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SellerBaseFinancialAccountId",
                table: "Trades",
                column: "SellerBaseFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SellerQuoteFinancialAccountId",
                table: "Trades",
                column: "SellerQuoteFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TradeMatchId",
                table: "Trades",
                column: "TradeMatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "TradeMatches");

            migrationBuilder.DropTable(
                name: "TradeOrders");

            migrationBuilder.DropTable(
                name: "MarketplacePairs");
        }
    }
}
