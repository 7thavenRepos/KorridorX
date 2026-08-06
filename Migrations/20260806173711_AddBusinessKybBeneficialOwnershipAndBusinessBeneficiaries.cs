using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessKybBeneficialOwnershipAndBusinessBeneficiaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountPurpose",
                table: "BusinessProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCustomers_ProviderCode_BusinessProfileId",
                table: "ProviderCustomers",
                columns: new[] { "ProviderCode", "BusinessProfileId" },
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessBeneficialOwners");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaryMobileWallets");

            migrationBuilder.DropTable(
                name: "BusinessKybDocuments");

            migrationBuilder.DropTable(
                name: "BusinessBeneficiaries");

            migrationBuilder.DropTable(
                name: "BusinessKybApplications");

            migrationBuilder.DropIndex(
                name: "IX_ProviderCustomers_ProviderCode_BusinessProfileId",
                table: "ProviderCustomers");

            migrationBuilder.DropColumn(
                name: "AccountPurpose",
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
                name: "SourceOfFunds",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TradingName",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "BusinessProfiles");
        }
    }
}
