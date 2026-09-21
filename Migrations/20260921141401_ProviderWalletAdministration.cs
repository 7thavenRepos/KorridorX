using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class ProviderWalletAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderWalletConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetworkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CollectionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PayoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultForCollection = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultForPayout = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderBalance = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
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
                    table.PrimaryKey("PK_ProviderWalletConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderWalletConfigurations_Assets_AssetCode",
                        column: x => x.AssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderWalletSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SelectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderWalletSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderWalletSelections_ProviderWalletConfigurations_Walle~",
                        column: x => x.WalletConfigurationId,
                        principalTable: "ProviderWalletConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletConfigurations_AssetCode",
                table: "ProviderWalletConfigurations",
                column: "AssetCode");

            migrationBuilder.CreateIndex(
                name: "UX_ProviderWallet_CollectionRoute",
                table: "ProviderWalletConfigurations",
                columns: new[] { "ProviderCode", "Environment", "AssetCode", "NetworkCode" },
                unique: true,
                filter: "\"IsActive\" AND \"DefaultForCollection\" AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "UX_ProviderWallet_Identity",
                table: "ProviderWalletConfigurations",
                columns: new[] { "ProviderCode", "Environment", "ProviderWalletId", "AssetCode", "NetworkCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProviderWallet_PayoutRoute",
                table: "ProviderWalletConfigurations",
                columns: new[] { "ProviderCode", "Environment", "AssetCode", "NetworkCode" },
                unique: true,
                filter: "\"IsActive\" AND \"DefaultForPayout\" AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletSelections_OperationType_OperationId",
                table: "ProviderWalletSelections",
                columns: new[] { "OperationType", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletSelections_WalletConfigurationId",
                table: "ProviderWalletSelections",
                column: "WalletConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderWalletSelections");

            migrationBuilder.DropTable(
                name: "ProviderWalletConfigurations");
        }
    }
}
