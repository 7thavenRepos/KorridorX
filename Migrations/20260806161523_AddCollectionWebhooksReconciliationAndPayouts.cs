using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionWebhooksReconciliationAndPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

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
                name: "ExpiredAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundInitiatedAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "InteracAnswer",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "InteracQuestion",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundInitiatedAt",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Collections");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId");
        }
    }
}
