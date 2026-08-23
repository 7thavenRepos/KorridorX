using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_BusinessPricingCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdjustmentType",
                table: "BusinessPricingPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentValue",
                table: "BusinessPricingPolicies",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "BusinessPricingPolicies"
                SET
                    "AdjustmentType" = 1,
                    "AdjustmentValue" = "MarkupPercentage"
                WHERE "AdjustmentType" IS NULL
                   OR "AdjustmentValue" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "AdjustmentType",
                table: "BusinessPricingPolicies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AdjustmentValue",
                table: "BusinessPricingPolicies",
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
                name: "BaseDestinationAmount",
                table: "InstantQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessPricingAdjustmentType",
                table: "InstantQuotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessPricingAdjustmentValue",
                table: "InstantQuotes",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "InstantQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessRevenueAmount",
                table: "InstantQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessRevenueRate",
                table: "InstantQuotes",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "InstantQuotes" q
                SET
                    "BaseDestinationAmount" = q."SourceAmount" * q."BaseCustomerRate",
                    "BusinessRevenueRate" =
                        CASE
                            WHEN q."BaseCustomerRate" > q."CustomerRate"
                                THEN q."BaseCustomerRate" - q."CustomerRate"
                            ELSE 0
                        END,
                    "BusinessRevenueAmount" =
                        CASE
                            WHEN (q."SourceAmount" * q."BaseCustomerRate") > q."DestinationAmount"
                                THEN (q."SourceAmount" * q."BaseCustomerRate") - q."DestinationAmount"
                            ELSE 0
                        END,
                    "BusinessProfileId" = p."BusinessProfileId",
                    "BusinessPricingAdjustmentType" = p."AdjustmentType",
                    "BusinessPricingAdjustmentValue" = p."AdjustmentValue"
                FROM "BusinessPricingPolicies" p
                WHERE q."BusinessPricingPolicyId" = p."Id";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "InstantQuotes"
                SET
                    "BaseDestinationAmount" = COALESCE(
                        "BaseDestinationAmount",
                        "SourceAmount" * "BaseCustomerRate"),
                    "BusinessRevenueRate" = COALESCE(
                        "BusinessRevenueRate",
                        CASE
                            WHEN "BaseCustomerRate" > "CustomerRate"
                                THEN "BaseCustomerRate" - "CustomerRate"
                            ELSE 0
                        END),
                    "BusinessRevenueAmount" = COALESCE(
                        "BusinessRevenueAmount",
                        CASE
                            WHEN ("SourceAmount" * "BaseCustomerRate") > "DestinationAmount"
                                THEN ("SourceAmount" * "BaseCustomerRate") - "DestinationAmount"
                            ELSE 0
                        END);
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseDestinationAmount",
                table: "InstantQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BusinessRevenueAmount",
                table: "InstantQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BusinessRevenueRate",
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
                name: "BaseCustomerRate",
                table: "InstantTrades",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseDestinationAmount",
                table: "InstantTrades",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessMarkupPercentage",
                table: "InstantTrades",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessPricingAdjustmentType",
                table: "InstantTrades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessPricingAdjustmentValue",
                table: "InstantTrades",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPricingPolicyId",
                table: "InstantTrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "InstantTrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessRevenueAmount",
                table: "InstantTrades",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessRevenueFinancialAccountId",
                table: "InstantTrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessRevenueLedgerTransactionId",
                table: "InstantTrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BusinessRevenueRate",
                table: "InstantTrades",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "InstantTrades" t
                SET
                    "BaseCustomerRate" = q."BaseCustomerRate",
                    "BaseDestinationAmount" = q."BaseDestinationAmount",
                    "BusinessMarkupPercentage" = q."BusinessMarkupPercentage",
                    "BusinessPricingAdjustmentType" = q."BusinessPricingAdjustmentType",
                    "BusinessPricingAdjustmentValue" = q."BusinessPricingAdjustmentValue",
                    "BusinessPricingPolicyId" = q."BusinessPricingPolicyId",
                    "BusinessProfileId" = q."BusinessProfileId",
                    "BusinessRevenueAmount" = q."BusinessRevenueAmount",
                    "BusinessRevenueRate" = q."BusinessRevenueRate"
                FROM "InstantQuotes" q
                WHERE t."InstantQuoteId" = q."Id";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "InstantTrades"
                SET
                    "BaseCustomerRate" = COALESCE("BaseCustomerRate", "CustomerRate"),
                    "BaseDestinationAmount" = COALESCE("BaseDestinationAmount", "DestinationAmount"),
                    "BusinessRevenueAmount" = COALESCE("BusinessRevenueAmount", 0),
                    "BusinessRevenueRate" = COALESCE("BusinessRevenueRate", 0);
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseCustomerRate",
                table: "InstantTrades",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,8)",
                oldPrecision: 18,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseDestinationAmount",
                table: "InstantTrades",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BusinessRevenueAmount",
                table: "InstantTrades",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BusinessRevenueRate",
                table: "InstantTrades",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,8)",
                oldPrecision: 18,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_BusinessRevenueFinancialAccountId",
                table: "InstantTrades",
                column: "BusinessRevenueFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_BusinessRevenueLedgerTransactionId",
                table: "InstantTrades",
                column: "BusinessRevenueLedgerTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstantTrades_FinancialAccounts_BusinessRevenueFinancialAcc~",
                table: "InstantTrades",
                column: "BusinessRevenueFinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InstantTrades_LedgerTransactions_BusinessRevenueLedgerTrans~",
                table: "InstantTrades",
                column: "BusinessRevenueLedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstantTrades_FinancialAccounts_BusinessRevenueFinancialAcc~",
                table: "InstantTrades");

            migrationBuilder.DropForeignKey(
                name: "FK_InstantTrades_LedgerTransactions_BusinessRevenueLedgerTrans~",
                table: "InstantTrades");

            migrationBuilder.DropIndex(
                name: "IX_InstantTrades_BusinessRevenueFinancialAccountId",
                table: "InstantTrades");

            migrationBuilder.DropIndex(
                name: "IX_InstantTrades_BusinessRevenueLedgerTransactionId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BaseCustomerRate",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BaseDestinationAmount",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessMarkupPercentage",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessPricingAdjustmentType",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessPricingAdjustmentValue",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessPricingPolicyId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueAmount",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueFinancialAccountId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueLedgerTransactionId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueRate",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "BaseDestinationAmount",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessPricingAdjustmentType",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessPricingAdjustmentValue",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueAmount",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessRevenueRate",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "AdjustmentType",
                table: "BusinessPricingPolicies");

            migrationBuilder.DropColumn(
                name: "AdjustmentValue",
                table: "BusinessPricingPolicies");
        }
    }
}
