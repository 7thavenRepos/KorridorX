using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_RemittanceDestinationProviderMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayoutDestinationProviderMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationType = table.Column<int>(type: "integer", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<int>(type: "integer", nullable: false),
                    ProviderBankId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderPartyId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderDestinationId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderVerifiedAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderVerificationReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastVerificationError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_PayoutDestinationProviderMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDestinationProviderMappings_DestinationType_Destinati~",
                table: "PayoutDestinationProviderMappings",
                columns: new[] { "DestinationType", "DestinationId", "ProviderCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDestinationProviderMappings_IsVerified",
                table: "PayoutDestinationProviderMappings",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDestinationProviderMappings_ProviderCode_ProviderDest~",
                table: "PayoutDestinationProviderMappings",
                columns: new[] { "ProviderCode", "ProviderDestinationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDestinationProviderMappings_ProviderCode_ProviderPart~",
                table: "PayoutDestinationProviderMappings",
                columns: new[] { "ProviderCode", "ProviderPartyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutDestinationProviderMappings");
        }
    }
}
