using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_DigitalAssetDepositIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalAssetDepositIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderCollectionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetworkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_DigitalAssetDepositIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssetDepositIntents_AssetNetworks_AssetNetworkId",
                        column: x => x.AssetNetworkId,
                        principalTable: "AssetNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetDepositIntents_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalAssetDepositIntents_FinancialAccounts_FinancialAccou~",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_AssetNetworkId",
                table: "DigitalAssetDepositIntents",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_BusinessProfileId_BusinessCustom~",
                table: "DigitalAssetDepositIntents",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_CollectionId",
                table: "DigitalAssetDepositIntents",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_FinancialAccountId",
                table: "DigitalAssetDepositIntents",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_ProviderCode_ProviderCollectionId",
                table: "DigitalAssetDepositIntents",
                columns: new[] { "ProviderCode", "ProviderCollectionId" },
                unique: true,
                filter: "\"ProviderCollectionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetDepositIntents_Status_ProviderExpiresAt",
                table: "DigitalAssetDepositIntents",
                columns: new[] { "Status", "ProviderExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalAssetDepositIntents");
        }
    }
}
