using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_DigitalAssetSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalAssetDepositAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAddressId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DestinationTag = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetDepositAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetDepositAddresses_AssetNetworks_AssetNetworkId",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetDepositAddresses_FinancialAccounts_FinancialAcc~",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetNetworkTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TransactionHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ToAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DestinationTag = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    NetworkFee = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Confirmations = table.Column<int>(type: "integer", nullable: false),
                    RequiredConfirmations = table.Column<int>(type: "integer", nullable: false),
                    BlockNumber = table.Column<long>(type: "bigint", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetNetworkTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetNetworkTransactions_AssetNetworks_AssetNetworkId",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetNetworkTransactions_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetNetworkTransactions_LedgerTransactions_LedgerTr~",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetNetworkTransactions_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetWithdrawalDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DestinationTag = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_DigitalAssetWithdrawalDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawalDestinations_AssetNetworks_AssetNetwo~",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    NetworkFee = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    TotalDebitAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawals_AssetNetworks_AssetNetworkId",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawals_DigitalAssetWithdrawalDestinations_~",
                        column: x => x.DestinationId,
                        principalTable: "DigitalAssetWithdrawalDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawals_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawals_FinancialReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "FinancialReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetWithdrawals_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositAddresses_AssetNetworkId",
                table: "DigitalAssetDepositAddresses",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositAddresses_BusinessProfileId_BusinessCust~",
                table: "DigitalAssetDepositAddresses",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositAddresses_FinancialAccountId",
                table: "DigitalAssetDepositAddresses",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositAddresses_ProviderCode_AssetNetworkId_A~1",
                table: "DigitalAssetDepositAddresses",
                columns: new[] { "ProviderCode", "AssetNetworkId", "Address", "DestinationTag" },
                unique: true,
                filter: "\"DestinationTag\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositAddresses_ProviderCode_AssetNetworkId_Ad~",
                table: "DigitalAssetDepositAddresses",
                columns: new[] { "ProviderCode", "AssetNetworkId", "Address" },
                unique: true,
                filter: "\"DestinationTag\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_AssetNetworkId",
                table: "DigitalAssetNetworkTransactions",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_CollectionId",
                table: "DigitalAssetNetworkTransactions",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_LedgerTransactionId",
                table: "DigitalAssetNetworkTransactions",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_PayoutId",
                table: "DigitalAssetNetworkTransactions",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_ProviderCode_ProviderTransa~",
                table: "DigitalAssetNetworkTransactions",
                columns: new[] { "ProviderCode", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_Status_Direction",
                table: "DigitalAssetNetworkTransactions",
                columns: new[] { "Status", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetNetworkTransactions_TransactionHash",
                table: "DigitalAssetNetworkTransactions",
                column: "TransactionHash");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_AssetNetworkId",
                table: "DigitalAssetWithdrawalDestinations",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~1",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "AssetNetworkId", "Address", "DestinationTag" },
                unique: true,
                filter: "\"DestinationTag\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busine~",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "AssetNetworkId", "Address" },
                unique: true,
                filter: "\"DestinationTag\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_AssetNetworkId",
                table: "DigitalAssetWithdrawals",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_BusinessProfileId_BusinessCustomerI~",
                table: "DigitalAssetWithdrawals",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_DestinationId",
                table: "DigitalAssetWithdrawals",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_FinancialAccountId",
                table: "DigitalAssetWithdrawals",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_PayoutId",
                table: "DigitalAssetWithdrawals",
                column: "PayoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_ReservationId",
                table: "DigitalAssetWithdrawals",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawals_Status",
                table: "DigitalAssetWithdrawals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalAssetDepositAddresses");

            migrationBuilder.DropTable(
                name: "DigitalAssetNetworkTransactions");

            migrationBuilder.DropTable(
                name: "DigitalAssetWithdrawals");

            migrationBuilder.DropTable(
                name: "DigitalAssetWithdrawalDestinations");
        }
    }
}
