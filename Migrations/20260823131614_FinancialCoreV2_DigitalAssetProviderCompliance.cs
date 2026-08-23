using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_DigitalAssetProviderCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualNetworkFee",
                table: "DigitalAssetWithdrawals",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetworkFeeVariance",
                table: "DigitalAssetWithdrawals",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DigitalAssetAddressRiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RiskScore = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonsJson = table.Column<string>(type: "text", nullable: true),
                    RawResultJson = table.Column<string>(type: "text", nullable: true),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetAddressRiskAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetAddressRiskAssessments_AssetNetworks_AssetNetwo~",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetProviderConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WebhookSecretProtected = table.Column<string>(type: "text", nullable: true),
                    WebhookSecretLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    WebhooksEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BalanceSyncEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastHealthCheckAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHealthCheckSucceeded = table.Column<bool>(type: "boolean", nullable: true),
                    LastHealthCheckMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetProviderConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetTravelRuleRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalAssetWithdrawalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetworkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginatorVasp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BeneficiaryVasp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetTravelRuleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetTravelRuleRecords_DigitalAssetWithdrawals_Digit~",
                        column: x => x.DigitalAssetWithdrawalId,
                        principalTable: "DigitalAssetWithdrawals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DigitalAssetWebhookReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetWebhookReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetAddressRiskAssessments_AssetNetworkId",
                table: "DigitalAssetAddressRiskAssessments",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetAddressRiskAssessments_BusinessProfileId_Busine~",
                table: "DigitalAssetAddressRiskAssessments",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "AssetNetworkId", "Address", "Direction", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetAddressRiskAssessments_IsBlocking_RiskLevel_Ass~",
                table: "DigitalAssetAddressRiskAssessments",
                columns: new[] { "IsBlocking", "RiskLevel", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetProviderConfigurations_ProviderCode",
                table: "DigitalAssetProviderConfigurations",
                column: "ProviderCode",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetTravelRuleRecords_DigitalAssetWithdrawalId",
                table: "DigitalAssetTravelRuleRecords",
                column: "DigitalAssetWithdrawalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetTravelRuleRecords_Status_CreatedAt",
                table: "DigitalAssetTravelRuleRecords",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWebhookReceipts_ProviderCode_ProviderEventId",
                table: "DigitalAssetWebhookReceipts",
                columns: new[] { "ProviderCode", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWebhookReceipts_Status_ReceivedAt",
                table: "DigitalAssetWebhookReceipts",
                columns: new[] { "Status", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalAssetAddressRiskAssessments");

            migrationBuilder.DropTable(
                name: "DigitalAssetProviderConfigurations");

            migrationBuilder.DropTable(
                name: "DigitalAssetTravelRuleRecords");

            migrationBuilder.DropTable(
                name: "DigitalAssetWebhookReceipts");

            migrationBuilder.DropColumn(
                name: "ActualNetworkFee",
                table: "DigitalAssetWithdrawals");

            migrationBuilder.DropColumn(
                name: "NetworkFeeVariance",
                table: "DigitalAssetWithdrawals");
        }
    }
}
