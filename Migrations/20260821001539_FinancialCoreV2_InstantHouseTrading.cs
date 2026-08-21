using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_InstantHouseTrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstantPairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HouseSourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseDestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MinimumSourceAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    MaximumSourceAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    SourceAmountIncrement = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    QuoteValiditySeconds = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_InstantPairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstantPairs_Assets_DestinationAssetCode",
                        column: x => x.DestinationAssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantPairs_Assets_SourceAssetCode",
                        column: x => x.SourceAssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantPairs_FinancialAccounts_HouseDestinationFinancialAcc~",
                        column: x => x.HouseDestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantPairs_FinancialAccounts_HouseSourceFinancialAccountId",
                        column: x => x.HouseSourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstantQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InstantPairId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserDestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseSourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseDestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    DestinationAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    ProviderRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_InstantQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_ExchangeRates_ExchangeRateId",
                        column: x => x.ExchangeRateId,
                        principalTable: "ExchangeRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_FinancialAccounts_HouseDestinationFinancialAc~",
                        column: x => x.HouseDestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_FinancialAccounts_HouseSourceFinancialAccount~",
                        column: x => x.HouseSourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_FinancialAccounts_UserDestinationFinancialAcc~",
                        column: x => x.UserDestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_FinancialAccounts_UserSourceFinancialAccountId",
                        column: x => x.UserSourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantQuotes_InstantPairs_InstantPairId",
                        column: x => x.InstantPairId,
                        principalTable: "InstantPairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstantTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InstantQuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstantPairId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserDestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseSourceFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseDestinationFinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    DestinationAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_InstantTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstantTrades_FinancialAccounts_HouseDestinationFinancialAc~",
                        column: x => x.HouseDestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_FinancialAccounts_HouseSourceFinancialAccount~",
                        column: x => x.HouseSourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_FinancialAccounts_UserDestinationFinancialAcc~",
                        column: x => x.UserDestinationFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_FinancialAccounts_UserSourceFinancialAccountId",
                        column: x => x.UserSourceFinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_FinancialReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "FinancialReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_InstantPairs_InstantPairId",
                        column: x => x.InstantPairId,
                        principalTable: "InstantPairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_InstantQuotes_InstantQuoteId",
                        column: x => x.InstantQuoteId,
                        principalTable: "InstantQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_LedgerTransactions_DestinationLedgerTransacti~",
                        column: x => x.DestinationLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstantTrades_LedgerTransactions_SourceLedgerTransactionId",
                        column: x => x.SourceLedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_Code",
                table: "InstantPairs",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_DestinationAssetCode",
                table: "InstantPairs",
                column: "DestinationAssetCode");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_HouseDestinationFinancialAccountId",
                table: "InstantPairs",
                column: "HouseDestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_HouseSourceFinancialAccountId",
                table: "InstantPairs",
                column: "HouseSourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_SourceAssetCode_DestinationAssetCode",
                table: "InstantPairs",
                columns: new[] { "SourceAssetCode", "DestinationAssetCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_InstantPairs_Status",
                table: "InstantPairs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_ExchangeRateId",
                table: "InstantQuotes",
                column: "ExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_HouseDestinationFinancialAccountId",
                table: "InstantQuotes",
                column: "HouseDestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_HouseSourceFinancialAccountId",
                table: "InstantQuotes",
                column: "HouseSourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_InstantPairId_Status_ExpiresAt",
                table: "InstantQuotes",
                columns: new[] { "InstantPairId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_Reference",
                table: "InstantQuotes",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_UserDestinationFinancialAccountId",
                table: "InstantQuotes",
                column: "UserDestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_UserId_Status_ExpiresAt",
                table: "InstantQuotes",
                columns: new[] { "UserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_UserSourceFinancialAccountId",
                table: "InstantQuotes",
                column: "UserSourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_DestinationLedgerTransactionId",
                table: "InstantTrades",
                column: "DestinationLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_HouseDestinationFinancialAccountId",
                table: "InstantTrades",
                column: "HouseDestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_HouseSourceFinancialAccountId",
                table: "InstantTrades",
                column: "HouseSourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_InstantPairId_Status_CreatedAt",
                table: "InstantTrades",
                columns: new[] { "InstantPairId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_InstantQuoteId",
                table: "InstantTrades",
                column: "InstantQuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_Reference",
                table: "InstantTrades",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_ReservationId",
                table: "InstantTrades",
                column: "ReservationId",
                unique: true,
                filter: "\"ReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_SourceLedgerTransactionId",
                table: "InstantTrades",
                column: "SourceLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_UserDestinationFinancialAccountId",
                table: "InstantTrades",
                column: "UserDestinationFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_UserId_Status_CreatedAt",
                table: "InstantTrades",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_UserSourceFinancialAccountId",
                table: "InstantTrades",
                column: "UserSourceFinancialAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstantTrades");

            migrationBuilder.DropTable(
                name: "InstantQuotes");

            migrationBuilder.DropTable(
                name: "InstantPairs");
        }
    }
}
