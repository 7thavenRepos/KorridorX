using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_GeneralPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "Payouts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ContextEntityId",
                table: "Payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextEntityType",
                table: "Payouts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "Payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Payouts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "Payouts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "Collections",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ContextEntityId",
                table: "Collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextEntityType",
                table: "Collections",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "Collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Collections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "Collections",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
    UPDATE "Collections"
    SET
        "Purpose" = 1,
        "RelatedEntityType" = 'Transfer',
        "RelatedEntityId" = "TransferId"
    WHERE "TransferId" IS NOT NULL;
    """);

            migrationBuilder.Sql("""
    UPDATE "Payouts"
    SET
        "Purpose" = 1,
        "RelatedEntityType" = 'Transfer',
        "RelatedEntityId" = "TransferId"
    WHERE "TransferId" IS NOT NULL;
    """);

            migrationBuilder.AlterColumn<int>(
                name: "Purpose",
                table: "Collections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Purpose",
                table: "Payouts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ContextEntityType_ContextEntityId",
                table: "Payouts",
                columns: new[] { "ContextEntityType", "ContextEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_FinancialAccountId",
                table: "Payouts",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Purpose",
                table: "Payouts",
                column: "Purpose");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_RelatedEntityType_RelatedEntityId",
                table: "Payouts",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ContextEntityType_ContextEntityId",
                table: "Collections",
                columns: new[] { "ContextEntityType", "ContextEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_FinancialAccountId",
                table: "Collections",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Purpose",
                table: "Collections",
                column: "Purpose");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_RelatedEntityType_RelatedEntityId",
                table: "Collections",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId");

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_FinancialAccounts_FinancialAccountId",
                table: "Collections",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_FinancialAccounts_FinancialAccountId",
                table: "Payouts",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_FinancialAccounts_FinancialAccountId",
                table: "Collections");

            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_FinancialAccounts_FinancialAccountId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_ContextEntityType_ContextEntityId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_FinancialAccountId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_Purpose",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_RelatedEntityType_RelatedEntityId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Collections_ContextEntityType_ContextEntityId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_FinancialAccountId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_Purpose",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_RelatedEntityType_RelatedEntityId",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ContextEntityId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ContextEntityType",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ContextEntityId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ContextEntityType",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Collections");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "Collections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_TransferId",
                table: "Payouts",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId",
                unique: true);
        }
    }
}
