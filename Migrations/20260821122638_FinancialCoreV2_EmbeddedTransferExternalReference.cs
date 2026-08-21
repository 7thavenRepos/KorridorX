using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class FinancialCoreV2_EmbeddedTransferExternalReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "Transfers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessCustomerId_ExternalReference",
                table: "Transfers",
                columns: new[] { "BusinessCustomerId", "ExternalReference" },
                unique: true,
                filter: "\"BusinessCustomerId\" IS NOT NULL AND \"ExternalReference\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessCustomerId_ExternalReference",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "Transfers");
        }
    }
}
