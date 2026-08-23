using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_Ledger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessLedgerEntries");

            migrationBuilder.DropTable(
                name: "BusinessWalletReservations");

            migrationBuilder.DropTable(
                name: "BusinessLedgerTransactions");

            migrationBuilder.DropTable(
                name: "BusinessWallets");

            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SettledBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    HeldBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
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
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_Assets_AssetCode",
                        column: x => x.AssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IdempotencyScope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyRequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContextEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedByTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_Assets_AssetCode",
                        column: x => x.AssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_LedgerTransactions_ReversalOfTransaction~",
                        column: x => x.ReversalOfTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_LedgerTransactions_ReversedByTransaction~",
                        column: x => x.ReversedByTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContextEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FinancialReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialReservations_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BalanceBucket = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    AccountBalanceAfter = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerPostings_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerPostings_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_AccountCode",
                table: "FinancialAccounts",
                column: "AccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_AssetCode",
                table: "FinancialAccounts",
                column: "AssetCode");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_OwnerType_OwnerId",
                table: "FinancialAccounts",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_OwnerType_OwnerId_AssetCode_AccountType",
                table: "FinancialAccounts",
                columns: new[] { "OwnerType", "OwnerId", "AssetCode", "AccountType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_Status",
                table: "FinancialAccounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReservations_FinancialAccountId",
                table: "FinancialReservations",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReservations_Reference",
                table: "FinancialReservations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReservations_RelatedEntityType_RelatedEntityId",
                table: "FinancialReservations",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialReservations_Status",
                table: "FinancialReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerPostings_FinancialAccountId",
                table: "LedgerPostings",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerPostings_LedgerTransactionId",
                table: "LedgerPostings",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_AssetCode",
                table: "LedgerTransactions",
                column: "AssetCode");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ContextEntityType_ContextEntityId",
                table: "LedgerTransactions",
                columns: new[] { "ContextEntityType", "ContextEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_IdempotencyScope_IdempotencyKey",
                table: "LedgerTransactions",
                columns: new[] { "IdempotencyScope", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PostedAt",
                table: "LedgerTransactions",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_Reference",
                table: "LedgerTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_RelatedEntityType_RelatedEntityId",
                table: "LedgerTransactions",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ReversalOfTransactionId",
                table: "LedgerTransactions",
                column: "ReversalOfTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ReversedByTransactionId",
                table: "LedgerTransactions",
                column: "ReversedByTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialReservations");

            migrationBuilder.DropTable(
                name: "LedgerPostings");

            migrationBuilder.DropTable(
                name: "FinancialAccounts");

            migrationBuilder.DropTable(
                name: "LedgerTransactions");

            migrationBuilder.CreateTable(
                name: "BusinessLedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyRequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedByTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessProfiles_BusinessProfile~",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HeldBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettledBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWallets_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountBalanceAfter = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Side = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessLedgerTransactions_BusinessLe~",
                        column: x => x.BusinessLedgerTransactionId,
                        principalTable: "BusinessLedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWalletReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessWalletReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessLedgerTransactionId",
                table: "BusinessLedgerEntries",
                column: "BusinessLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessWalletId",
                table: "BusinessLedgerEntries",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessPaymentBatchId",
                table: "BusinessLedgerTransactions",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId",
                table: "BusinessLedgerTransactions",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId_IdempotencyKey",
                table: "BusinessLedgerTransactions",
                columns: new[] { "BusinessProfileId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_CollectionId",
                table: "BusinessLedgerTransactions",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_PostedAt",
                table: "BusinessLedgerTransactions",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_Reference",
                table: "BusinessLedgerTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_ReversalOfTransactionId",
                table: "BusinessLedgerTransactions",
                column: "ReversalOfTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_ReversedByTransactionId",
                table: "BusinessLedgerTransactions",
                column: "ReversedByTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_TransferId",
                table: "BusinessLedgerTransactions",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessPaymentBatchId",
                table: "BusinessWalletReservations",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessWalletId",
                table: "BusinessWalletReservations",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Reference",
                table: "BusinessWalletReservations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Status",
                table: "BusinessWalletReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_TransferId",
                table: "BusinessWalletReservations",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_BusinessProfileId_CurrencyCode",
                table: "BusinessWallets",
                columns: new[] { "BusinessProfileId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_Status",
                table: "BusinessWallets",
                column: "Status");
        }
    }
}
