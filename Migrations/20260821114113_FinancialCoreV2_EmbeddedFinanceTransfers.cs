using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_EmbeddedFinanceTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessCustomerId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceFinancialAccountId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessCustomerId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceFinancialAccountId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessCustomerId",
                table: "Transfers",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SourceFinancialAccountId",
                table: "Transfers",
                column: "SourceFinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferQuotes_BusinessCustomerId",
                table: "TransferQuotes",
                column: "BusinessCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferQuotes_SourceFinancialAccountId",
                table: "TransferQuotes",
                column: "SourceFinancialAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferQuotes_BusinessCustomers_BusinessCustomerId",
                table: "TransferQuotes",
                column: "BusinessCustomerId",
                principalTable: "BusinessCustomers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferQuotes_FinancialAccounts_SourceFinancialAccountId",
                table: "TransferQuotes",
                column: "SourceFinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessCustomers_BusinessCustomerId",
                table: "Transfers",
                column: "BusinessCustomerId",
                principalTable: "BusinessCustomers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_FinancialAccounts_SourceFinancialAccountId",
                table: "Transfers",
                column: "SourceFinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferQuotes_BusinessCustomers_BusinessCustomerId",
                table: "TransferQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferQuotes_FinancialAccounts_SourceFinancialAccountId",
                table: "TransferQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessCustomers_BusinessCustomerId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_FinancialAccounts_SourceFinancialAccountId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessCustomerId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_SourceFinancialAccountId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_TransferQuotes_BusinessCustomerId",
                table: "TransferQuotes");

            migrationBuilder.DropIndex(
                name: "IX_TransferQuotes_SourceFinancialAccountId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "BusinessCustomerId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "SourceFinancialAccountId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessCustomerId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "SourceFinancialAccountId",
                table: "TransferQuotes");
        }
    }
}
