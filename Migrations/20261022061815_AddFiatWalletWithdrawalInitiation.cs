using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddFiatWalletWithdrawalInitiation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiatWalletWithdrawals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrencyDecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderBankId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProtectedAccountNumber = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AccountNumberLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    VerifiedAccountName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WithdrawalAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    ProviderFee = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    KorridorXFee = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    KorridorXFeePolicy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalWalletDebit = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    ExpectedRecipientAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    FeeQuotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FinancialReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedToProviderAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OperationalHoldReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FiatWalletWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiatWalletWithdrawals_FiatWalletWithdrawalDestinations_Dest~",
                        column: x => x.DestinationId,
                        principalTable: "FiatWalletWithdrawalDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiatWalletWithdrawals_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiatWalletWithdrawals_FinancialReservations_FinancialReserv~",
                        column: x => x.FinancialReservationId,
                        principalTable: "FinancialReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiatWalletWithdrawals_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_BusinessProfileId_SourceCountryCode_C~",
                table: "FiatWalletWithdrawals",
                columns: new[] { "BusinessProfileId", "SourceCountryCode", "CurrencyCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_CustomerProfileId_SourceCountryCode_C~",
                table: "FiatWalletWithdrawals",
                columns: new[] { "CustomerProfileId", "SourceCountryCode", "CurrencyCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_DestinationId",
                table: "FiatWalletWithdrawals",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_FinancialAccountId",
                table: "FiatWalletWithdrawals",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_FinancialReservationId",
                table: "FiatWalletWithdrawals",
                column: "FinancialReservationId",
                unique: true,
                filter: "\"FinancialReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_OwnerType_OwnerId_CreatedAt",
                table: "FiatWalletWithdrawals",
                columns: new[] { "OwnerType", "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_OwnerType_OwnerId_IdempotencyKeyHash",
                table: "FiatWalletWithdrawals",
                columns: new[] { "OwnerType", "OwnerId", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_PayoutId",
                table: "FiatWalletWithdrawals",
                column: "PayoutId",
                unique: true,
                filter: "\"PayoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_Reference",
                table: "FiatWalletWithdrawals",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiatWalletWithdrawals_Status_CreatedAt",
                table: "FiatWalletWithdrawals",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiatWalletWithdrawals");
        }
    }
}
