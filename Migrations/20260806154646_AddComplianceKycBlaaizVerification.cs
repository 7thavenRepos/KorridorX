using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceKycBlaaizVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEvents_ProviderEventId",
                table: "WebhookEvents");

            migrationBuilder.AddColumn<string>(
                name: "TimestampHeader",
                table: "WebhookEvents",
                type: "character varying(100)",
                maxLength: 100,
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

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ProviderCode_ProviderEventId",
                table: "WebhookEvents",
                columns: new[] { "ProviderCode", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_KycApplicationId_DocumentType",
                table: "KycDocuments",
                columns: new[] { "KycApplicationId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycDocuments_ProviderFileId",
                table: "KycDocuments",
                column: "ProviderFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEvents_ProviderCode_ProviderEventId",
                table: "WebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_KycDocuments_KycApplicationId_DocumentType",
                table: "KycDocuments");

            migrationBuilder.DropIndex(
                name: "IX_KycDocuments_ProviderFileId",
                table: "KycDocuments");

            migrationBuilder.DropColumn(
                name: "TimestampHeader",
                table: "WebhookEvents");

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

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ProviderEventId",
                table: "WebhookEvents",
                column: "ProviderEventId");
        }
    }
}
