using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAuditAndOperationalControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "NotificationMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "NotificationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "NotificationMessages",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "NotificationMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "BusinessLedgerTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyRequestHash",
                table: "BusinessLedgerTransactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfTransactionId",
                table: "BusinessLedgerTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "BusinessLedgerTransactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAt",
                table: "BusinessLedgerTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByTransactionId",
                table: "BusinessLedgerTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_DeadLetteredAt",
                table: "NotificationMessages",
                column: "DeadLetteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_LockId",
                table: "NotificationMessages",
                column: "LockId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Status_NextAttemptAt",
                table: "NotificationMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId_IdempotencyKey",
                table: "BusinessLedgerTransactions",
                columns: new[] { "BusinessProfileId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_ReversalOfTransactionId",
                table: "BusinessLedgerTransactions",
                column: "ReversalOfTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_ReversedByTransactionId",
                table: "BusinessLedgerTransactions",
                column: "ReversedByTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category",
                table: "AuditLogs",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_DeadLetteredAt",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_LockId",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_Status_NextAttemptAt",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId_IdempotencyKey",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BusinessLedgerTransactions_ReversalOfTransactionId",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BusinessLedgerTransactions_ReversedByTransactionId",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Category",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "LockId",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyRequestHash",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalOfTransactionId",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "ReversedByTransactionId",
                table: "BusinessLedgerTransactions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "AuditLogs");
        }
    }
}
