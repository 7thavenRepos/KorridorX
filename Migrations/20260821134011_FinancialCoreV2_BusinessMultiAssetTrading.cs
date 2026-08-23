using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_BusinessMultiAssetTrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OwnerId as nullable first so existing rows can be backfilled
            // from their current UserId safely.
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "InstantTrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                table: "InstantTrades",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "InstantQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                table: "InstantQuotes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Existing Instant records are consumer/User records.
            migrationBuilder.Sql("""
        UPDATE "InstantTrades"
        SET
            "OwnerType" = 1,
            "OwnerId" = "UserId";
        """);

            migrationBuilder.Sql("""
        UPDATE "InstantQuotes"
        SET
            "OwnerType" = 1,
            "OwnerId" = "UserId";
        """);

            // Once all existing records have been backfilled,
            // enforce the model's required OwnerId.
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "InstantTrades",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "InstantQuotes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstantTrades_OwnerType_OwnerId_Status_CreatedAt",
                table: "InstantTrades",
                columns: new[] { "OwnerType", "OwnerId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_OwnerType_OwnerId_Status_ExpiresAt",
                table: "InstantQuotes",
                columns: new[] { "OwnerType", "OwnerId", "Status", "ExpiresAt" });
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstantTrades_OwnerType_OwnerId_Status_CreatedAt",
                table: "InstantTrades");

            migrationBuilder.DropIndex(
                name: "IX_InstantQuotes_OwnerType_OwnerId_Status_ExpiresAt",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "InstantTrades");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "InstantQuotes");
        }
    }
}
