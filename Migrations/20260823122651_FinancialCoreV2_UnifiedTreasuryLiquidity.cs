using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_UnifiedTreasuryLiquidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_CurrencyCode",
                table: "ProviderWalletBalances");

            migrationBuilder.DropIndex(
                name: "IX_LiquidityThresholds_ProviderCode_CurrencyCode_IsActive",
                table: "LiquidityThresholds");

            migrationBuilder.AlterColumn<string>(
                name: "ToCurrencyCode",
                table: "TreasuryRebalanceRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "FromCurrencyCode",
                table: "TreasuryRebalanceRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementStatementItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementStatementImports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementBatchItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementBatches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "ProviderWalletBalances",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<Guid>(
                name: "AssetNetworkId",
                table: "ProviderWalletBalances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkCode",
                table: "ProviderWalletBalances",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "LiquidityThresholds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<int>(
                name: "FinancialAccountType",
                table: "LiquidityThresholds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkCode",
                table: "LiquidityThresholds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeType",
                table: "LiquidityThresholds",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "SourceCurrencyCode",
                table: "FxMarkupRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "DestinationCurrencyCode",
                table: "FxMarkupRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletBalances_AssetNetworkId",
                table: "ProviderWalletBalances",
                column: "AssetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_CurrencyCode_NetworkCode",
                table: "ProviderWalletBalances",
                columns: new[] { "ProviderCode", "CurrencyCode", "NetworkCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LiquidityThresholds_ScopeType_ProviderCode_CurrencyCode_Net~",
                table: "LiquidityThresholds",
                columns: new[] { "ScopeType", "ProviderCode", "CurrencyCode", "NetworkCode", "FinancialAccountType", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderWalletBalances_AssetNetworks_AssetNetworkId",
                table: "ProviderWalletBalances",
                column: "AssetNetworkId",
                principalTable: "AssetNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderWalletBalances_AssetNetworks_AssetNetworkId",
                table: "ProviderWalletBalances");

            migrationBuilder.DropIndex(
                name: "IX_ProviderWalletBalances_AssetNetworkId",
                table: "ProviderWalletBalances");

            migrationBuilder.DropIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_CurrencyCode_NetworkCode",
                table: "ProviderWalletBalances");

            migrationBuilder.DropIndex(
                name: "IX_LiquidityThresholds_ScopeType_ProviderCode_CurrencyCode_Net~",
                table: "LiquidityThresholds");

            migrationBuilder.DropColumn(
                name: "AssetNetworkId",
                table: "ProviderWalletBalances");

            migrationBuilder.DropColumn(
                name: "NetworkCode",
                table: "ProviderWalletBalances");

            migrationBuilder.DropColumn(
                name: "FinancialAccountType",
                table: "LiquidityThresholds");

            migrationBuilder.DropColumn(
                name: "NetworkCode",
                table: "LiquidityThresholds");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "LiquidityThresholds");

            migrationBuilder.AlterColumn<string>(
                name: "ToCurrencyCode",
                table: "TreasuryRebalanceRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FromCurrencyCode",
                table: "TreasuryRebalanceRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementStatementItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementStatementImports",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementBatchItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "SettlementBatches",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "ProviderWalletBalances",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "LiquidityThresholds",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "SourceCurrencyCode",
                table: "FxMarkupRules",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "DestinationCurrencyCode",
                table: "FxMarkupRules",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletBalances_ProviderCode_CurrencyCode",
                table: "ProviderWalletBalances",
                columns: new[] { "ProviderCode", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_LiquidityThresholds_ProviderCode_CurrencyCode_IsActive",
                table: "LiquidityThresholds",
                columns: new[] { "ProviderCode", "CurrencyCode", "IsActive" });
        }
    }
}
