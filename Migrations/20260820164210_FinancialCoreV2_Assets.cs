using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_Assets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryCurrencies");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.AlterColumn<decimal>(
                name: "ToAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequestedAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FromAmountMinusFees",
                table: "TreasuryRebalanceRequests",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FromAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "Transfers",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "Transfers",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "Transfers",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "Transfers",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "TransferQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "TransferQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "TransferQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "TransferQuotes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinAmount",
                table: "TransferFees",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxAmount",
                table: "TransferFees",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FixedFee",
                table: "TransferFees",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequestedRefundAmount",
                table: "TransferDisputes",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SettlementStatementItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "SettlementStatementImports",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "SettlementStatementImports",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossDebits",
                table: "SettlementStatementImports",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossCredits",
                table: "SettlementStatementImports",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SettlementBatchItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "SettlementBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossOutflows",
                table: "SettlementBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossInflows",
                table: "SettlementBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedNetAmount",
                table: "SettlementBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualNetAmount",
                table: "SettlementBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "RegulatoryReports",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "ProviderWalletBalances",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProviderFeeAmount",
                table: "ProviderTransactions",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountWithoutFee",
                table: "ProviderTransactions",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ProviderTransactions",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "ProviderInvoices",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "ProviderInvoices",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "ProviderInvoices",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "ProviderInvoices",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchedProviderFeeAmount",
                table: "ProviderInvoices",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchedProviderFeeAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payouts",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetBalance",
                table: "LiquidityThresholds",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumBalance",
                table: "LiquidityThresholds",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaximumBalance",
                table: "LiquidityThresholds",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitAmount",
                table: "JournalLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "JournalLines",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerTransferLimit",
                table: "ComplianceLimits",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyLimit",
                table: "ComplianceLimits",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyLimit",
                table: "ComplianceLimits",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Collections",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "SettledBalance",
                table: "BusinessWallets",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "HeldBalance",
                table: "BusinessWallets",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableBalance",
                table: "BusinessWallets",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessWalletReservations",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalSourceAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFeeAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessLedgerTransactions",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessLedgerEntries",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AccountBalanceAfter",
                table: "BusinessLedgerEntries",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    IsStablecoin = table.Column<bool>(type: "boolean", nullable: false),
                    IsSupported = table.Column<bool>(type: "boolean", nullable: false),
                    DepositEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WithdrawalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TradingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InstantEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AssetNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetworkCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NativeAssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContractAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequiredConfirmations = table.Column<int>(type: "integer", nullable: false),
                    MinimumDeposit = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    MinimumWithdrawal = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    WithdrawalFee = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    DepositEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WithdrawalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetNetworks_Assets_AssetCode",
                        column: x => x.AssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CountryAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CanSend = table.Column<bool>(type: "boolean", nullable: false),
                    CanReceive = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeposit = table.Column<bool>(type: "boolean", nullable: false),
                    CanWithdraw = table.Column<bool>(type: "boolean", nullable: false),
                    CanTrade = table.Column<bool>(type: "boolean", nullable: false),
                    CanUseInstant = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryAssets_Assets_AssetCode",
                        column: x => x.AssetCode,
                        principalTable: "Assets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CountryAssets_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetNetworks_AssetCode_NetworkCode",
                table: "AssetNetworks",
                columns: new[] { "AssetCode", "NetworkCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_IsSupported",
                table: "Assets",
                column: "IsSupported");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Type_IsSupported",
                table: "Assets",
                columns: new[] { "Type", "IsSupported" });

            migrationBuilder.CreateIndex(
                name: "IX_CountryAssets_AssetCode",
                table: "CountryAssets",
                column: "AssetCode");

            migrationBuilder.CreateIndex(
                name: "IX_CountryAssets_CountryCode_AssetCode",
                table: "CountryAssets",
                columns: new[] { "CountryCode", "AssetCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetNetworks");

            migrationBuilder.DropTable(
                name: "CountryAssets");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.AlterColumn<decimal>(
                name: "ToAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequestedAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "FromAmountMinusFees",
                table: "TreasuryRebalanceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FromAmount",
                table: "TreasuryRebalanceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "Transfers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "Transfers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "Transfers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "Transfers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "TransferQuotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "TransferQuotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "TransferQuotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "TransferQuotes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinAmount",
                table: "TransferFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxAmount",
                table: "TransferFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FixedFee",
                table: "TransferFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequestedRefundAmount",
                table: "TransferDisputes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SettlementStatementItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "SettlementStatementImports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "SettlementStatementImports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossDebits",
                table: "SettlementStatementImports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossCredits",
                table: "SettlementStatementImports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SettlementBatchItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "SettlementBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossOutflows",
                table: "SettlementBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossInflows",
                table: "SettlementBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedNetAmount",
                table: "SettlementBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualNetAmount",
                table: "SettlementBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "RegulatoryReports",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "ProviderWalletBalances",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "ProviderFeeAmount",
                table: "ProviderTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AmountWithoutFee",
                table: "ProviderTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ProviderTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "ProviderInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "ProviderInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "ProviderInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "ProviderInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchedProviderFeeAmount",
                table: "ProviderInvoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "VarianceAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchedProviderFeeAmount",
                table: "ProviderInvoiceLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payouts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetBalance",
                table: "LiquidityThresholds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumBalance",
                table: "LiquidityThresholds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaximumBalance",
                table: "LiquidityThresholds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitAmount",
                table: "JournalLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "JournalLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerTransferLimit",
                table: "ComplianceLimits",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyLimit",
                table: "ComplianceLimits",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "DailyLimit",
                table: "ComplianceLimits",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Collections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "SettledBalance",
                table: "BusinessWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "HeldBalance",
                table: "BusinessWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableBalance",
                table: "BusinessWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessWalletReservations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "SourceAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "DestinationAmount",
                table: "BusinessPaymentBatchItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalSourceAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPayableAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFeeAmount",
                table: "BusinessPaymentBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessLedgerTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BusinessLedgerEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "AccountBalanceAfter",
                table: "BusinessLedgerEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(36,18)",
                oldPrecision: 36,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    IsFiat = table.Column<bool>(type: "boolean", nullable: false),
                    IsStablecoin = table.Column<bool>(type: "boolean", nullable: false),
                    IsSupported = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "CountryCurrencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CanReceive = table.Column<bool>(type: "boolean", nullable: false),
                    CanSend = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryCurrencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountryCurrencies_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CountryCurrencies_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountryCurrencies_CountryCode_CurrencyCode",
                table: "CountryCurrencies",
                columns: new[] { "CountryCode", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountryCurrencies_CurrencyCode",
                table: "CountryCurrencies",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsSupported",
                table: "Currencies",
                column: "IsSupported");
        }
    }
}
