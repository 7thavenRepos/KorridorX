using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionFundingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Collections_TransferId",
                table: "Collections");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_TransferId",
                table: "Collections",
                column: "TransferId");
        }
    }
}
