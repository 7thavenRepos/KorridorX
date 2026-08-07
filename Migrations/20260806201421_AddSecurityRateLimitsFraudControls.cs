using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityRateLimitsFraudControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<string>(
                name: "ComplianceHoldReason",
                table: "Transfers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceReviewedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComplianceReviewedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplianceHold",
                table: "Transfers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RiskAssessedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskDecision",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskLevel",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeviceFingerprint",
                table: "RefreshTokens",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "RefreshTokens",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByToken",
                table: "RefreshTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                table: "RefreshTokens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "RefreshTokens",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocking",
                table: "AmlFlags",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDecision",
                table: "AmlFlags",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "AmlFlags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "AmlFlags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "AmlFlags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryId_SourceAmount_CreatedAt",
                table: "Transfers",
                columns: new[] { "BusinessBeneficiaryId", "SourceAmount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessProfileId_CreatedAt",
                table: "Transfers",
                columns: new[] { "BusinessProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_CustomerProfileId_CreatedAt",
                table: "Transfers",
                columns: new[] { "CustomerProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_IsComplianceHold",
                table: "Transfers",
                column: "IsComplianceHold");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_RecipientId_SourceAmount_CreatedAt",
                table: "Transfers",
                columns: new[] { "RecipientId", "SourceAmount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_RiskDecision_RiskLevel",
                table: "Transfers",
                columns: new[] { "RiskDecision", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_UserId_DeviceFingerprint_OccurredAt",
                table: "LoginHistories",
                columns: new[] { "UserId", "DeviceFingerprint", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_UserId_WasSuccessful_OccurredAt",
                table: "LoginHistories",
                columns: new[] { "UserId", "WasSuccessful", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AmlFlags_IsBlocking",
                table: "AmlFlags",
                column: "IsBlocking");

            migrationBuilder.CreateIndex(
                name: "IX_AmlFlags_IsResolved_IsBlocking_CreatedAt",
                table: "AmlFlags",
                columns: new[] { "IsResolved", "IsBlocking", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AmlFlags_RiskScore",
                table: "AmlFlags",
                column: "RiskScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryId_SourceAmount_CreatedAt",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessProfileId_CreatedAt",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_CustomerProfileId_CreatedAt",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_IsComplianceHold",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_RecipientId_SourceAmount_CreatedAt",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_RiskDecision_RiskLevel",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked_ExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_LoginHistories_UserId_DeviceFingerprint_OccurredAt",
                table: "LoginHistories");

            migrationBuilder.DropIndex(
                name: "IX_LoginHistories_UserId_WasSuccessful_OccurredAt",
                table: "LoginHistories");

            migrationBuilder.DropIndex(
                name: "IX_AmlFlags_IsBlocking",
                table: "AmlFlags");

            migrationBuilder.DropIndex(
                name: "IX_AmlFlags_IsResolved_IsBlocking_CreatedAt",
                table: "AmlFlags");

            migrationBuilder.DropIndex(
                name: "IX_AmlFlags_RiskScore",
                table: "AmlFlags");

            migrationBuilder.DropColumn(
                name: "ComplianceHoldReason",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ComplianceReviewedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ComplianceReviewedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "IsComplianceHold",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RiskAssessedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RiskDecision",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "DeviceFingerprint",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByToken",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "IsBlocking",
                table: "AmlFlags");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "AmlFlags");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AmlFlags");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "AmlFlags");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "AmlFlags");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");
        }
    }
}
