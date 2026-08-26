using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_BusinessDigitalAssetOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~1",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busine~",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetWithdrawals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetWithdrawalDestinations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetTravelRuleRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetDepositIntents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetDepositAddresses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetAddressRiskAssessments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Asset~1",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "AssetNetworkId", "Address", "DestinationTag" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NULL AND \"DestinationTag\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_AssetN~",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "AssetNetworkId", "Address" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NULL AND \"DestinationTag\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~1",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "AssetNetworkId", "Address" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NOT NULL AND \"DestinationTag\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~2",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId", "AssetNetworkId", "Address", "DestinationTag" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NOT NULL AND \"DestinationTag\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busine~",
                table: "DigitalAssetWithdrawalDestinations",
                columns: new[] { "BusinessProfileId", "BusinessCustomerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // C2_ROLLBACK_DIRECT_BUSINESS_GUARD
            // Once direct BusinessProfile digital-asset rows exist,
            // BusinessCustomerId = NULL carries ownership meaning and
            // this migration cannot be safely rolled back automatically.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DigitalAssetWithdrawals" WHERE "BusinessCustomerId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DigitalAssetWithdrawalDestinations" WHERE "BusinessCustomerId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DigitalAssetTravelRuleRecords" WHERE "BusinessCustomerId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DigitalAssetDepositIntents" WHERE "BusinessCustomerId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DigitalAssetDepositAddresses" WHERE "BusinessCustomerId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DigitalAssetAddressRiskAssessments" WHERE "BusinessCustomerId" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Cannot roll back FinancialCoreV2_BusinessDigitalAssetOwnership while direct BusinessProfile digital-asset rows exist. Migrate or remove those rows explicitly first.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Asset~1",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_AssetN~",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~1",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busin~2",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.DropIndex(
                name: "IX_DigitalAssetWithdrawalDestinations_BusinessProfileId_Busine~",
                table: "DigitalAssetWithdrawalDestinations");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetWithdrawals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetWithdrawalDestinations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetTravelRuleRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetDepositIntents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetDepositAddresses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessCustomerId",
                table: "DigitalAssetAddressRiskAssessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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
        }
    }
}
