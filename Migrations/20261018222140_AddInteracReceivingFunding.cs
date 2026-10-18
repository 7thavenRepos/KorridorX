using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddInteracReceivingFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InteracReceivingRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderWalletConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderBusinessId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderRegistrationId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReceivingEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedReceivingEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IndividualEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_InteracReceivingRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteracReceivingRoutes_ProviderWalletConfigurations_Provide~",
                        column: x => x.ProviderWalletConfigurationId,
                        principalTable: "ProviderWalletConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCollectionCreditClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCollectionCreditClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderCollectionCreditClaims_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProviderCollectionCreditClaims_FinancialAccounts_FinancialA~",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InteracDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SenderEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ReceivingEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderOccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalWebhookJson = table.Column<string>(type: "text", nullable: false),
                    VerifiedProviderJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteracDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteracDeposits_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InteracDeposits_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InteracDeposits_InteracReceivingRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "InteracReceivingRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InteracSenderOwnerships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstablishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteracSenderOwnerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteracSenderOwnerships_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InteracSenderOwnerships_InteracReceivingRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "InteracReceivingRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InteracSendingEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CodeSalt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CodeExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanResendAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_InteracSendingEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteracSendingEmails_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InteracSendingEmails_InteracReceivingRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "InteracReceivingRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InteracDepositReviewNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteracDepositReviewNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteracDepositReviewNotes_InteracDeposits_DepositId",
                        column: x => x.DepositId,
                        principalTable: "InteracDeposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InteracDepositReviewNotes_DepositId_CreatedAt",
                table: "InteracDepositReviewNotes",
                columns: new[] { "DepositId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InteracDeposits_CollectionId",
                table: "InteracDeposits",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InteracDeposits_Environment_ConnectionKey_ProviderTransacti~",
                table: "InteracDeposits",
                columns: new[] { "Environment", "ConnectionKey", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InteracDeposits_Environment_Status_ReceivedAt",
                table: "InteracDeposits",
                columns: new[] { "Environment", "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InteracDeposits_FinancialAccountId",
                table: "InteracDeposits",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InteracDeposits_RouteId",
                table: "InteracDeposits",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_InteracReceivingRoutes_Environment_ConnectionKey_Normalized~",
                table: "InteracReceivingRoutes",
                columns: new[] { "Environment", "ConnectionKey", "NormalizedReceivingEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InteracReceivingRoutes_Environment_ConnectionKey_ProviderRe~",
                table: "InteracReceivingRoutes",
                columns: new[] { "Environment", "ConnectionKey", "ProviderRegistrationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InteracReceivingRoutes_ProviderWalletConfigurationId",
                table: "InteracReceivingRoutes",
                column: "ProviderWalletConfigurationId");

            migrationBuilder.CreateIndex(
                name: "UX_InteracRoute_ActiveConnection",
                table: "InteracReceivingRoutes",
                columns: new[] { "Environment", "ConnectionKey" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_InteracSenderOwnerships_FinancialAccountId",
                table: "InteracSenderOwnerships",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_InteracSender_RouteOwnership",
                table: "InteracSenderOwnerships",
                columns: new[] { "RouteId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InteracSendingEmails_FinancialAccountId",
                table: "InteracSendingEmails",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InteracSendingEmails_RouteId_FinancialAccountId_NormalizedE~",
                table: "InteracSendingEmails",
                columns: new[] { "RouteId", "FinancialAccountId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCollectionCreditClaims_CollectionId",
                table: "ProviderCollectionCreditClaims",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCollectionCreditClaims_FinancialAccountId",
                table: "ProviderCollectionCreditClaims",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_ProviderCollectionCredit_Transaction",
                table: "ProviderCollectionCreditClaims",
                columns: new[] { "ProviderCode", "Environment", "ConnectionKey", "ProviderTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InteracDepositReviewNotes");

            migrationBuilder.DropTable(
                name: "InteracSenderOwnerships");

            migrationBuilder.DropTable(
                name: "InteracSendingEmails");

            migrationBuilder.DropTable(
                name: "ProviderCollectionCreditClaims");

            migrationBuilder.DropTable(
                name: "InteracDeposits");

            migrationBuilder.DropTable(
                name: "InteracReceivingRoutes");
        }
    }
}
