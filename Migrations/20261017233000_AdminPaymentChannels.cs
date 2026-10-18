using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace KorridorX.Migrations
{
    public partial class AdminPaymentChannels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(name: "PaymentChannels", columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderWalletConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    CapabilityCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IndividualEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderCapabilityConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    HasBeenActivated = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                }, constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentChannels", x => x.Id);
                    table.ForeignKey("FK_PaymentChannels_Assets_AssetCode", x => x.AssetCode, "Assets", "Code", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_PaymentChannels_ProviderWalletConfigurations_ProviderWallet~", x => x.ProviderWalletConfigurationId, "ProviderWalletConfigurations", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_PaymentChannels_Environment_Direction_AssetCode", table: "PaymentChannels", columns: new[] { "Environment", "Direction", "AssetCode" });
            migrationBuilder.CreateIndex(name: "UX_PaymentChannel_Route", table: "PaymentChannels", columns: new[] { "Environment", "Direction", "AssetCode", "CountryCode", "CapabilityCode", "ProviderWalletConfigurationId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_PaymentChannels_AssetCode", table: "PaymentChannels", column: "AssetCode");
            migrationBuilder.CreateIndex(name: "IX_PaymentChannels_ProviderWalletConfigurationId", table: "PaymentChannels", column: "ProviderWalletConfigurationId");
            migrationBuilder.CreateTable(name: "PaymentChannelSelections", columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CapabilityCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Business = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(36,18)", precision: 36, scale: 18, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                }, constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentChannelSelections", x => x.Id);
                    table.ForeignKey("FK_PaymentChannelSelections_PaymentChannels_ChannelId", x => x.ChannelId, "PaymentChannels", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_PaymentChannelSelections_ProviderWalletConfigurations_Provi~", x => x.ProviderWalletConfigurationId, "ProviderWalletConfigurations", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_PaymentChannelSelections_OperationType_OperationId", table: "PaymentChannelSelections", columns: new[] { "OperationType", "OperationId" }, unique: true);
            migrationBuilder.CreateIndex(name: "UX_PaymentChannel_Request", table: "PaymentChannelSelections", columns: new[] { "OwnerId", "FinancialAccountId", "IdempotencyKey" }, unique: true, filter: "\"IdempotencyKey\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_PaymentChannelSelections_ChannelId", table: "PaymentChannelSelections", column: "ChannelId");
            migrationBuilder.CreateIndex(name: "IX_PaymentChannelSelections_ProviderWalletConfigurationId", table: "PaymentChannelSelections", column: "ProviderWalletConfigurationId");
            migrationBuilder.CreateTable(name: "WalletDepositAccounts", columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderCustomerId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Iban = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoutingNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CanRetryCreation = table.Column<bool>(type: "boolean", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                }, constraints: table =>
                {
                    table.PrimaryKey("PK_WalletDepositAccounts", x => x.Id);
                    table.ForeignKey("FK_WalletDepositAccounts_FinancialAccounts_FinancialAccountId", x => x.FinancialAccountId, "FinancialAccounts", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_WalletDepositAccounts_PaymentChannels_PaymentChannelId", x => x.PaymentChannelId, "PaymentChannels", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_WalletDepositAccounts_ProviderWalletConfigurations_Provider~", x => x.ProviderWalletConfigurationId, "ProviderWalletConfigurations", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "UX_WalletDepositAccount_CustomerWallet", table: "WalletDepositAccounts", columns: new[] { "Environment", "ProviderWalletId", "ProviderCustomerId" }, unique: true);
            migrationBuilder.CreateIndex(name: "UX_WalletDepositAccount_ProviderAccount", table: "WalletDepositAccounts", columns: new[] { "Environment", "ConnectionKey", "ProviderAccountId" }, unique: true, filter: "\"ProviderAccountId\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_WalletDepositAccounts_FinancialAccountId", table: "WalletDepositAccounts", column: "FinancialAccountId");
            migrationBuilder.CreateIndex(name: "IX_WalletDepositAccounts_PaymentChannelId", table: "WalletDepositAccounts", column: "PaymentChannelId");
            migrationBuilder.CreateIndex(name: "IX_WalletDepositAccounts_ProviderWalletConfigurationId", table: "WalletDepositAccounts", column: "ProviderWalletConfigurationId");
            migrationBuilder.AddColumn<Guid>(name: "PaymentChannelId", table: "Collections", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PaymentChannelName", table: "Collections", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ClientRequestKey", table: "Collections", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "PaymentChannelId", table: "Payouts", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PaymentChannelName", table: "Payouts", type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.CreateIndex(name: "UX_Collection_WalletRequest", table: "Collections", columns: new[] { "FinancialAccountId", "ClientRequestKey" }, unique: true, filter: "\"ClientRequestKey\" IS NOT NULL");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("UX_Collection_WalletRequest", "Collections");
            migrationBuilder.DropTable("WalletDepositAccounts");
            migrationBuilder.DropTable("PaymentChannelSelections");
            migrationBuilder.DropTable("PaymentChannels");
            migrationBuilder.DropColumn("PaymentChannelId", "Collections");
            migrationBuilder.DropColumn("PaymentChannelName", "Collections");
            migrationBuilder.DropColumn("ClientRequestKey", "Collections");
            migrationBuilder.DropColumn("PaymentChannelId", "Payouts");
            migrationBuilder.DropColumn("PaymentChannelName", "Payouts");
        }
    }
}
