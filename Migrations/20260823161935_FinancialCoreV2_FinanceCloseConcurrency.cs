using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_FinanceCloseConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseRequests_AccountingPeriodId_Pending",
                table: "FinanceCloseRequests",
                column: "AccountingPeriodId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinanceCloseRequests_AccountingPeriodId_Pending",
                table: "FinanceCloseRequests");
        }
    }
}
