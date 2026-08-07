using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryRebalancingSettlementStatementsAndFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementStatementImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedRowCount = table.Column<int>(type: "integer", nullable: false),
                    UnmatchedRowCount = table.Column<int>(type: "integer", nullable: false),
                    GrossCredits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossDebits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VarianceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_SettlementStatementImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementStatementImports_SettlementBatches_SettlementBatc~",
                        column: x => x.SettlementBatchId,
                        principalTable: "SettlementBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreasuryRebalanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromProviderWalletBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToProviderWalletBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ToCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderSwapId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FromAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FromAmountMinusFees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ToAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    CustomExchangeRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_TreasuryRebalanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreasuryRebalanceRequests_ProviderWalletBalances_FromProvid~",
                        column: x => x.FromProviderWalletBalanceId,
                        principalTable: "ProviderWalletBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryRebalanceRequests_ProviderWalletBalances_ToProvider~",
                        column: x => x.ToProviderWalletBalanceId,
                        principalTable: "ProviderWalletBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementStatementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementStatementImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsMatched = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderTransactionRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementStatementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementStatementItems_SettlementStatementImports_Settlem~",
                        column: x => x.SettlementStatementImportId,
                        principalTable: "SettlementStatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementStatementImports_ProviderCode_FileHash",
                table: "SettlementStatementImports",
                columns: new[] { "ProviderCode", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementStatementImports_SettlementBatchId",
                table: "SettlementStatementImports",
                column: "SettlementBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementStatementItems_ProviderTransactionId",
                table: "SettlementStatementItems",
                column: "ProviderTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementStatementItems_SettlementStatementImportId",
                table: "SettlementStatementItems",
                column: "SettlementStatementImportId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryRebalanceRequests_FromProviderWalletBalanceId",
                table: "TreasuryRebalanceRequests",
                column: "FromProviderWalletBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryRebalanceRequests_ProviderCode_Status_CreatedAt",
                table: "TreasuryRebalanceRequests",
                columns: new[] { "ProviderCode", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryRebalanceRequests_Reference",
                table: "TreasuryRebalanceRequests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryRebalanceRequests_ToProviderWalletBalanceId",
                table: "TreasuryRebalanceRequests",
                column: "ToProviderWalletBalanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementStatementItems");

            migrationBuilder.DropTable(
                name: "TreasuryRebalanceRequests");

            migrationBuilder.DropTable(
                name: "SettlementStatementImports");
        }
    }
}
