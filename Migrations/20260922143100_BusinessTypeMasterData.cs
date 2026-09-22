using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class BusinessTypeMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessTypes", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "BusinessTypes",
                columns: new[] { "Code", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { "corporation", true, "Corporation", 10 },
                    { "government_entity", true, "Government entity", 20 },
                    { "llc", true, "Limited liability company (LLC)", 30 },
                    { "non_profit", true, "Non-profit organization", 40 },
                    { "other", true, "Other", 70 },
                    { "partnership", true, "Partnership", 50 },
                    { "sole_proprietorship", true, "Sole proprietorship", 60 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessTypes");
        }
    }
}
