using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_EmbeddedFinanceProvisioningWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessCustomerId",
                table: "ProviderCustomers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessWebhookEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EventTypesCsv = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SigningSecretProtected = table.Column<string>(type: "text", nullable: false),
                    SigningSecretLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_BusinessWebhookEndpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWebhookEndpoints_ApiApplications_ApiApplicationId",
                        column: x => x.ApiApplicationId,
                        principalTable: "ApiApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWebhookEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWebhookEndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    LastResponseBody = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessWebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWebhookDeliveries_BusinessWebhookEndpoints_Business~",
                        column: x => x.BusinessWebhookEndpointId,
                        principalTable: "BusinessWebhookEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWebhookDeliveries_BusinessWebhookEvents_BusinessWeb~",
                        column: x => x.BusinessWebhookEventId,
                        principalTable: "BusinessWebhookEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCustomers_BusinessCustomerId",
                table: "ProviderCustomers",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCustomers_ProviderCode_BusinessCustomerId",
                table: "ProviderCustomers",
                columns: new[] { "ProviderCode", "BusinessCustomerId" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookDeliveries_BusinessWebhookEndpointId",
                table: "BusinessWebhookDeliveries",
                column: "BusinessWebhookEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookDeliveries_BusinessWebhookEventId_BusinessWe~",
                table: "BusinessWebhookDeliveries",
                columns: new[] { "BusinessWebhookEventId", "BusinessWebhookEndpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookDeliveries_LockId",
                table: "BusinessWebhookDeliveries",
                column: "LockId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookDeliveries_Status_NextAttemptAt",
                table: "BusinessWebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookEndpoints_ApiApplicationId_Status",
                table: "BusinessWebhookEndpoints",
                columns: new[] { "ApiApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookEndpoints_BusinessProfileId_Url",
                table: "BusinessWebhookEndpoints",
                columns: new[] { "BusinessProfileId", "Url" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookEvents_BusinessProfileId_EventType_OccurredAt",
                table: "BusinessWebhookEvents",
                columns: new[] { "BusinessProfileId", "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookEvents_BusinessProfileId_OccurredAt",
                table: "BusinessWebhookEvents",
                columns: new[] { "BusinessProfileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWebhookEvents_EventId",
                table: "BusinessWebhookEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderCustomers_BusinessCustomers_BusinessCustomerId",
                table: "ProviderCustomers",
                column: "BusinessCustomerId",
                principalTable: "BusinessCustomers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderCustomers_BusinessCustomers_BusinessCustomerId",
                table: "ProviderCustomers");

            migrationBuilder.DropTable(
                name: "BusinessWebhookDeliveries");

            migrationBuilder.DropTable(
                name: "BusinessWebhookEndpoints");

            migrationBuilder.DropTable(
                name: "BusinessWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_ProviderCustomers_BusinessCustomerId",
                table: "ProviderCustomers");

            migrationBuilder.DropIndex(
                name: "IX_ProviderCustomers_ProviderCode_BusinessCustomerId",
                table: "ProviderCustomers");

            migrationBuilder.DropColumn(
                name: "BusinessCustomerId",
                table: "ProviderCustomers");
        }
    }
}
