using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_BusinessPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseCustomerRate",
                table: "InstantQuotes",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "InstantQuotes"
                SET "BaseCustomerRate" = "CustomerRate"
                WHERE "BaseCustomerRate" IS NULL;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseCustomerRate",
                table: "InstantQuotes",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,8)",
                oldPrecision: 18,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessMarkupPercentage",
                table: "InstantQuotes",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPricingPolicyId",
                table: "InstantQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessPricingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MarkupPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MaximumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_BusinessPricingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPricingPolicies_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_BusinessPricingPolicyId",
                table: "InstantQuotes",
                column: "BusinessPricingPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPricingPolicies_BusinessProfileId_SourceAssetCode_D~",
                table: "BusinessPricingPolicies",
                columns: new[] { "BusinessProfileId", "SourceAssetCode", "DestinationAssetCode", "IsActive", "EffectiveFrom" });

            migrationBuilder.AddForeignKey(
                name: "FK_InstantQuotes_BusinessPricingPolicies_BusinessPricingPolicy~",
                table: "InstantQuotes",
                column: "BusinessPricingPolicyId",
                principalTable: "BusinessPricingPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstantQuotes_BusinessPricingPolicies_BusinessPricingPolicy~",
                table: "InstantQuotes");

            migrationBuilder.DropTable(
                name: "BusinessPricingPolicies");

            migrationBuilder.DropIndex(
                name: "IX_InstantQuotes_BusinessPricingPolicyId",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BaseCustomerRate",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessMarkupPercentage",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessPricingPolicyId",
                table: "InstantQuotes");
        }
    }
}
