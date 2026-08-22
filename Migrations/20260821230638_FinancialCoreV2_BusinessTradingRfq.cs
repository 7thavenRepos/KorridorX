using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_BusinessTradingRfq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessTradingRfqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MarketplacePairId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterOwnerType = table.Column<int>(type: "integer", nullable: false),
                    RequesterOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CounterpartyOwnerType = table.Column<int>(type: "integer", nullable: false),
                    CounterpartyOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedQuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TradeMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_BusinessTradingRfqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessTradingRfqs_MarketplacePairs_MarketplacePairId",
                        column: x => x.MarketplacePairId,
                        principalTable: "MarketplacePairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessTradingRfqQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BusinessTradingRfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponderOwnerType = table.Column<int>(type: "integer", nullable: false),
                    ResponderOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_BusinessTradingRfqQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessTradingRfqQuotes_BusinessTradingRfqs_BusinessTradin~",
                        column: x => x.BusinessTradingRfqId,
                        principalTable: "BusinessTradingRfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqQuotes_BusinessTradingRfqId_Status_Create~",
                table: "BusinessTradingRfqQuotes",
                columns: new[] { "BusinessTradingRfqId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqQuotes_Reference",
                table: "BusinessTradingRfqQuotes",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqQuotes_ResponderOwnerType_ResponderOwnerI~",
                table: "BusinessTradingRfqQuotes",
                columns: new[] { "ResponderOwnerType", "ResponderOwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqs_CounterpartyOwnerType_CounterpartyOwner~",
                table: "BusinessTradingRfqs",
                columns: new[] { "CounterpartyOwnerType", "CounterpartyOwnerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqs_MarketplacePairId",
                table: "BusinessTradingRfqs",
                column: "MarketplacePairId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqs_Reference",
                table: "BusinessTradingRfqs",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqs_RequesterOwnerType_RequesterOwnerId_Sta~",
                table: "BusinessTradingRfqs",
                columns: new[] { "RequesterOwnerType", "RequesterOwnerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessTradingRfqs_TradeMatchId",
                table: "BusinessTradingRfqs",
                column: "TradeMatchId",
                unique: true,
                filter: "\"TradeMatchId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessTradingRfqQuotes");

            migrationBuilder.DropTable(
                name: "BusinessTradingRfqs");
        }
    }
}
