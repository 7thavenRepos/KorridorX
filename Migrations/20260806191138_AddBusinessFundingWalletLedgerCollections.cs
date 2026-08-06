using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessFundingWalletLedgerCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEvents_ProviderEventId",
                table: "WebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.AddColumn<string>(
                name: "TimestampHeader",
                table: "WebhookEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "Transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalRejectionReason",
                table: "Transfers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryBankAccountId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryMobileWalletId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessFundingSource",
                table: "Transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalApprovedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovals",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedForApprovalAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationCountryCode",
                table: "TransferQuotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceCountryCode",
                table: "TransferQuotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TransferType",
                table: "TransferQuotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastVerificationError",
                table: "RecipientBankAccounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderBankId",
                table: "RecipientBankAccounts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderVerificationReference",
                table: "RecipientBankAccounts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderVerifiedAccountName",
                table: "RecipientBankAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationAttemptedAt",
                table: "RecipientBankAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InteracAnswer",
                table: "Payouts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InteracQuestion",
                table: "Payouts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttachedToProviderAt",
                table: "KycDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAttachedToProvider",
                table: "KycDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUploaded",
                table: "KycDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProviderFileId",
                table: "KycDocuments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "KycDocuments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadConfirmedAt",
                table: "KycDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdentityExpiryDate",
                table: "KycApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IdentityIssueDate",
                table: "KycApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityNumberLastFour",
                table: "KycApplications",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityType",
                table: "KycApplications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefundSyncedAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderExpiresAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRefundId",
                table: "Collections",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRefundReference",
                table: "Collections",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundFailureReason",
                table: "Collections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundInitiatedAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Collections",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Permissions",
                table: "BusinessUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "AccountPurpose",
                table: "BusinessProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTransferCreatorApproval",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BusinessDescription",
                table: "BusinessProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "BusinessProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatedAnnualRevenue",
                table: "BusinessProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedMonthlyPayments",
                table: "BusinessProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IncorporationDate",
                table: "BusinessProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndustryType",
                table: "BusinessProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KybRejectedAt",
                table: "BusinessProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KybRejectionReason",
                table: "BusinessProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KybScope",
                table: "BusinessProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KybSubmittedAt",
                table: "BusinessProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingAddressLine1",
                table: "BusinessProfiles",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingAddressLine2",
                table: "BusinessProfiles",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCity",
                table: "BusinessProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCountryCode",
                table: "BusinessProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingPostalCode",
                table: "BusinessProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingStateOrProvince",
                table: "BusinessProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredTransferApprovals",
                table: "BusinessProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTransferApproval",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceOfFunds",
                table: "BusinessProfiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradingName",
                table: "BusinessProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "BusinessProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessBeneficiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RelationshipOrPurpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_BusinessBeneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessBeneficiaries_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessKybApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    KybScope = table.Column<int>(type: "integer", nullable: false),
                    ProviderApplicationId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SubmittedPayloadJson = table.Column<string>(type: "text", nullable: true),
                    ProviderResponseJson = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessKybApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessKybApplications_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPaymentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceCountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FundingSource = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    ValidItems = table.Column<int>(type: "integer", nullable: false),
                    InvalidItems = table.Column<int>(type: "integer", nullable: false),
                    CompletedItems = table.Column<int>(type: "integer", nullable: false),
                    FailedItems = table.Column<int>(type: "integer", nullable: false),
                    TotalSourceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessPaymentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatches_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SettledBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeldBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_BusinessWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWallets_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<int>(type: "integer", nullable: false),
                    ProviderBankId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    NationalBankCode = table.Column<string>(type: "text", nullable: true),
                    ProviderCountryId = table.Column<string>(type: "text", nullable: true),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    CountryName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderBanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessBeneficiaryBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BranchCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Iban = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SwiftBic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoutingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderBankId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderBeneficiaryId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderBankAccountId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderVerifiedAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderVerificationReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    VerificationAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastVerificationError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_BusinessBeneficiaryBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessBeneficiaryBankAccounts_BusinessBeneficiaries_Busin~",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessBeneficiaryMobileWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WalletNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderBeneficiaryId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderWalletId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_BusinessBeneficiaryMobileWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessBeneficiaryMobileWallets_BusinessBeneficiaries_Busi~",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessBeneficialOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessKybApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderOwnerId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Nationality = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OwnershipPercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    HasControl = table.Column<bool>(type: "boolean", nullable: false),
                    IsSigner = table.Column<bool>(type: "boolean", nullable: false),
                    IsBeneficialOwner = table.Column<bool>(type: "boolean", nullable: false),
                    IsPep = table.Column<bool>(type: "boolean", nullable: false),
                    IdDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdentityNumberLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    IdentityNumberEncrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IdDocumentCountry = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IdExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdentityFrontProviderFileId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IdentityBackProviderFileId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsIdentityFrontUploaded = table.Column<bool>(type: "boolean", nullable: false),
                    IsIdentityBackUploaded = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityFrontUploadConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdentityBackUploadConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AreIdentityFilesAttachedToProvider = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityFilesAttachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAdminCommentsJson = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessBeneficialOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessBeneficialOwners_BusinessKybApplications_BusinessKy~",
                        column: x => x.BusinessKybApplicationId,
                        principalTable: "BusinessKybApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessKybDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessKybApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProviderFileId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProviderDocumentId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsUploaded = table.Column<bool>(type: "boolean", nullable: false),
                    UploadConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRegisteredWithProvider = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredWithProviderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAdminCommentsJson = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessKybDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessKybDocuments_BusinessKybApplications_BusinessKybApp~",
                        column: x => x.BusinessKybApplicationId,
                        principalTable: "BusinessKybApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_AspNetUsers_ActionedByUserId",
                        column: x => x.ActionedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_BusinessPaymentBatches_BusinessPaymentBat~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_BusinessLedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessProfiles_BusinessProfile~",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWalletReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessWalletReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPaymentBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryBankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryMobileWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationCountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DestinationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ProviderRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    PurposeNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidationErrors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TransferQuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_BusinessPaymentBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaries_BusinessBen~",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaryBankAccounts_B~",
                        column: x => x.BusinessBeneficiaryBankAccountId,
                        principalTable: "BusinessBeneficiaryBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaryMobileWallets_~",
                        column: x => x.BusinessBeneficiaryMobileWalletId,
                        principalTable: "BusinessBeneficiaryMobileWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessPaymentBatches_BusinessPa~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_TransferQuotes_TransferQuoteId",
                        column: x => x.TransferQuoteId,
                        principalTable: "TransferQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountBalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessLedgerTransactions_BusinessLe~",
                        column: x => x.BusinessLedgerTransactionId,
                        principalTable: "BusinessLedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ProviderCode_ProviderEventId",
                table: "WebhookEvents",
                columns: new[] { "ProviderCode", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ApprovalStatus",
                table: "Transfers",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryBankAccountId",
                table: "Transfers",
                column: "BusinessBeneficiaryBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryId",
                table: "Transfers",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryMobileWalletId",
                table: "Transfers",
                column: "BusinessBeneficiaryMobileWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessProfileId",
                table: "Transfers",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferQuotes_BusinessProfileId",
                table: "TransferQuotes",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCustomers_ProviderCode_CustomerProfileId",
                table: "ProviderCustomers",
                columns: new[] { "ProviderCode", "CustomerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_ReadAt",
                table: "NotificationMessages",
                column: "ReadAt");

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_KycApplicationId_DocumentType",
                table: "KycDocuments",
                columns: new[] { "KycApplicationId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_ProviderFileId",
                table: "KycDocuments",
                column: "ProviderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ProviderRefundId",
                table: "Collections",
                column: "ProviderRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_ActionedAt",
                table: "BusinessApprovals",
                column: "ActionedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_ActionedByUserId",
                table: "BusinessApprovals",
                column: "ActionedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessPaymentBatchId",
                table: "BusinessApprovals",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessPaymentBatchId_ActionedByUserId",
                table: "BusinessApprovals",
                columns: new[] { "BusinessPaymentBatchId", "ActionedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessProfileId",
                table: "BusinessApprovals",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_TransferId",
                table: "BusinessApprovals",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_TransferId_ActionedByUserId",
                table: "BusinessApprovals",
                columns: new[] { "TransferId", "ActionedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficialOwners_BusinessKybApplicationId",
                table: "BusinessBeneficialOwners",
                column: "BusinessKybApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficialOwners_BusinessKybApplicationId_Email",
                table: "BusinessBeneficialOwners",
                columns: new[] { "BusinessKybApplicationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficialOwners_ProviderOwnerId",
                table: "BusinessBeneficialOwners",
                column: "ProviderOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaries_BusinessProfileId",
                table: "BusinessBeneficiaries",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaries_BusinessProfileId_Name",
                table: "BusinessBeneficiaries",
                columns: new[] { "BusinessProfileId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaries_CountryCode",
                table: "BusinessBeneficiaries",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaryBankAccounts_BusinessBeneficiaryId",
                table: "BusinessBeneficiaryBankAccounts",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaryBankAccounts_CountryCode_CurrencyCode",
                table: "BusinessBeneficiaryBankAccounts",
                columns: new[] { "CountryCode", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaryBankAccounts_ProviderBankId",
                table: "BusinessBeneficiaryBankAccounts",
                column: "ProviderBankId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaryMobileWallets_BusinessBeneficiaryId",
                table: "BusinessBeneficiaryMobileWallets",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessBeneficiaryMobileWallets_CountryCode_CurrencyCode",
                table: "BusinessBeneficiaryMobileWallets",
                columns: new[] { "CountryCode", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybApplications_BusinessProfileId",
                table: "BusinessKybApplications",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybApplications_ProviderApplicationId",
                table: "BusinessKybApplications",
                column: "ProviderApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybApplications_Status",
                table: "BusinessKybApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybDocuments_BusinessKybApplicationId",
                table: "BusinessKybDocuments",
                column: "BusinessKybApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybDocuments_BusinessKybApplicationId_DocumentType_~",
                table: "BusinessKybDocuments",
                columns: new[] { "BusinessKybApplicationId", "DocumentType", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybDocuments_ProviderDocumentId",
                table: "BusinessKybDocuments",
                column: "ProviderDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessKybDocuments_ProviderFileId",
                table: "BusinessKybDocuments",
                column: "ProviderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessLedgerTransactionId",
                table: "BusinessLedgerEntries",
                column: "BusinessLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessWalletId",
                table: "BusinessLedgerEntries",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessPaymentBatchId",
                table: "BusinessLedgerTransactions",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId",
                table: "BusinessLedgerTransactions",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_CollectionId",
                table: "BusinessLedgerTransactions",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_PostedAt",
                table: "BusinessLedgerTransactions",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_Reference",
                table: "BusinessLedgerTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_TransferId",
                table: "BusinessLedgerTransactions",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_BusinessProfileId",
                table: "BusinessPaymentBatches",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_CreatedAt",
                table: "BusinessPaymentBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_Reference",
                table: "BusinessPaymentBatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_Status",
                table: "BusinessPaymentBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryBankAccountId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryMobileWalletId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryMobileWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessPaymentBatchId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessPaymentBatchId_RowNumber",
                table: "BusinessPaymentBatchItems",
                columns: new[] { "BusinessPaymentBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_TransferId",
                table: "BusinessPaymentBatchItems",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_TransferQuoteId",
                table: "BusinessPaymentBatchItems",
                column: "TransferQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessPaymentBatchId",
                table: "BusinessWalletReservations",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessWalletId",
                table: "BusinessWalletReservations",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Reference",
                table: "BusinessWalletReservations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Status",
                table: "BusinessWalletReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_TransferId",
                table: "BusinessWalletReservations",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_BusinessProfileId_CurrencyCode",
                table: "BusinessWallets",
                columns: new[] { "BusinessProfileId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_Status",
                table: "BusinessWallets",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferQuotes_BusinessProfiles_BusinessProfileId",
                table: "TransferQuotes",
                column: "BusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaries_BusinessBeneficiaryId",
                table: "Transfers",
                column: "BusinessBeneficiaryId",
                principalTable: "BusinessBeneficiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryBankAccounts_BusinessBeneficia~",
                table: "Transfers",
                column: "BusinessBeneficiaryBankAccountId",
                principalTable: "BusinessBeneficiaryBankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryMobileWallets_BusinessBenefici~",
                table: "Transfers",
                column: "BusinessBeneficiaryMobileWalletId",
                principalTable: "BusinessBeneficiaryMobileWallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessProfiles_BusinessProfileId",
                table: "Transfers",
                column: "BusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferQuotes_BusinessProfiles_BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaries_BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryBankAccounts_BusinessBeneficia~",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryMobileWallets_BusinessBenefici~",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessProfiles_BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropTable(
                name: "BusinessApprovals");

            migrationBuilder.DropTable(
                name: "BusinessBeneficialOwners");

            migrationBuilder.DropTable(
                name: "BusinessKybDocuments");

            migrationBuilder.DropTable(
                name: "BusinessLedgerEntries");

            migrationBuilder.DropTable(
                name: "BusinessPaymentBatchItems");

            migrationBuilder.DropTable(
                name: "BusinessWalletReservations");

            migrationBuilder.DropTable(
                name: "ProviderBanks");

            migrationBuilder.DropTable(
                name: "BusinessKybApplications");

            migrationBuilder.DropTable(
                name: "BusinessLedgerTransactions");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaryMobileWallets");

            migrationBuilder.DropTable(
                name: "BusinessWallets");

            migrationBuilder.DropTable(
                name: "BusinessPaymentBatches");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaries");

            migrationBuilder.DropIndex(
                name: "IX_WebhookEvents_ProviderCode_ProviderEventId",
                table: "WebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_ApprovalStatus",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryBankAccountId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryMobileWalletId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_TransferQuotes_BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropIndex(
                name: "IX_ProviderCustomers_ProviderCode_CustomerProfileId",
                table: "ProviderCustomers");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_ReadAt",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_KycDocuments_KycApplicationId_DocumentType",
                table: "KycDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KycDocuments_ProviderFileId",
                table: "KycDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Collections_ProviderRefundId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "TimestampHeader",
                table: "WebhookEvents");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovalRejectionReason",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryBankAccountId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryMobileWalletId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessFundingSource",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "FinalApprovedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RequiredApprovals",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "SubmittedForApprovalAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "DestinationCountryCode",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "SourceCountryCode",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "TransferType",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "LastVerificationError",
                table: "RecipientBankAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderBankId",
                table: "RecipientBankAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderVerificationReference",
                table: "RecipientBankAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderVerifiedAccountName",
                table: "RecipientBankAccounts");

            migrationBuilder.DropColumn(
                name: "VerificationAttemptedAt",
                table: "RecipientBankAccounts");

            migrationBuilder.DropColumn(
                name: "InteracAnswer",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "InteracQuestion",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "AttachedToProviderAt",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "IsAttachedToProvider",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "IsUploaded",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "ProviderFileId",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "UploadConfirmedAt",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "IdentityExpiryDate",
                table: "KycApplications");

            migrationBuilder.DropColumn(
                name: "IdentityIssueDate",
                table: "KycApplications");

            migrationBuilder.DropColumn(
                name: "IdentityNumberLastFour",
                table: "KycApplications");

            migrationBuilder.DropColumn(
                name: "IdentityType",
                table: "KycApplications");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "LastRefundSyncedAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ProviderExpiresAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ProviderRefundId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ProviderRefundReference",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundFailureReason",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundInitiatedAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "BusinessUsers");

            migrationBuilder.DropColumn(
                name: "AccountPurpose",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "AllowTransferCreatorApproval",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "BusinessDescription",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "EstimatedAnnualRevenue",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "ExpectedMonthlyPayments",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "IncorporationDate",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "IndustryType",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KybRejectedAt",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KybRejectionReason",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KybScope",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "KybSubmittedAt",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingAddressLine1",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingAddressLine2",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingCity",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingCountryCode",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingPostalCode",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "OperatingStateOrProvince",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "RequiredTransferApprovals",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "RequiresTransferApproval",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "SourceOfFunds",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TradingName",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "BusinessProfiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ProviderEventId",
                table: "WebhookEvents",
                column: "ProviderEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId");
        }
    }
}
