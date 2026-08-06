using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBankDirectoryRecipientVerificationRefundRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefundSyncedAt",
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

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Collections",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProviderBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<int>(type: "integer", nullable: false),
                    ProviderBankId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NationalBankCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderCountryId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ProviderRefundId",
                table: "Collections",
                column: "ProviderRefundId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderBanks_Code",
                table: "ProviderBanks",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderBanks_Name",
                table: "ProviderBanks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderBanks_ProviderCode_CountryCode_IsActive",
                table: "ProviderBanks",
                columns: new[] { "ProviderCode", "CountryCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderBanks_ProviderCode_ProviderBankId",
                table: "ProviderBanks",
                columns: new[] { "ProviderCode", "ProviderBankId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderBanks");

            migrationBuilder.DropIndex(
                name: "IX_Collections_ProviderRefundId",
                table: "Collections");

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
                name: "LastRefundSyncedAt",
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
                name: "RefundReason",
                table: "Collections");
        }
    }
}
