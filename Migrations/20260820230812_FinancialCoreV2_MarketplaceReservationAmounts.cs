using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_MarketplaceReservationAmounts : Migration
    {
        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CapturedAmount",
                table: "FinancialReservations",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReleasedAmount",
                table: "FinancialReservations",
                type: "numeric(36,18)",
                precision: 36,
                scale: 18,
                nullable: false,
                defaultValue: 0m);

            // Backfill reservations that were already captured before
            // partial reservation accounting was introduced.
            migrationBuilder.Sql("""
        UPDATE "FinancialReservations"
        SET "CapturedAmount" = "Amount"
        WHERE "Status" = 2;
        """);

            // Backfill reservations that were already released before
            // partial reservation accounting was introduced.
            migrationBuilder.Sql("""
        UPDATE "FinancialReservations"
        SET "ReleasedAmount" = "Amount"
        WHERE "Status" = 3;
        """);
        }
         
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapturedAmount",
                table: "FinancialReservations");

            migrationBuilder.DropColumn(
                name: "ReleasedAmount",
                table: "FinancialReservations");
        }
    }
}
