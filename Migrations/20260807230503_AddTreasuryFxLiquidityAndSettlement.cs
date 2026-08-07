using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryFxLiquidityAndSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxMarkupRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MarkupPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MaximumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_FxMarkupRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiquidityThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MinimumBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LiquidityThresholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderWalletBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderBusinessId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderWalletBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettlementBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WindowStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GrossInflows = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossOutflows = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedNetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualNetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VarianceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReconciliationNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_SettlementBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettlementBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderTransactionRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementBatchItems_ProviderTransactions_ProviderTransacti~",
                        column: x => x.ProviderTransactionRowId,
                        principalTable: "ProviderTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementBatchItems_SettlementBatches_SettlementBatchId",
                        column: x => x.SettlementBatchId,
                        principalTable: "SettlementBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxMarkupRules_SourceCurrencyCode_DestinationCurrencyCode_Is~",
                table: "FxMarkupRules",
                columns: new[] { "SourceCurrencyCode", "DestinationCurrencyCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LiquidityThresholds_ProviderCode_CurrencyCode_IsActive",
                table: "LiquidityThresholds",
                columns: new[] { "ProviderCode", "CurrencyCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_CurrencyCode",
                table: "ProviderWalletBalances",
                columns: new[] { "ProviderCode", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_ProviderWalletId",
                table: "ProviderWalletBalances",
                columns: new[] { "ProviderCode", "ProviderWalletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementBatches_ProviderCode_CurrencyCode_WindowStart_Win~",
                table: "SettlementBatches",
                columns: new[] { "ProviderCode", "CurrencyCode", "WindowStart", "WindowEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementBatches_Reference",
                table: "SettlementBatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementBatchItems_ProviderTransactionRowId",
                table: "SettlementBatchItems",
                column: "ProviderTransactionRowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementBatchItems_SettlementBatchId",
                table: "SettlementBatchItems",
                column: "SettlementBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxMarkupRules");

            migrationBuilder.DropTable(
                name: "LiquidityThresholds");

            migrationBuilder.DropTable(
                name: "ProviderWalletBalances");

            migrationBuilder.DropTable(
                name: "SettlementBatchItems");

            migrationBuilder.DropTable(
                name: "SettlementBatches");
        }
    }
}
