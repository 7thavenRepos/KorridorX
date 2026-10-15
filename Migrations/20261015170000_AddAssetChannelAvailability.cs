using KorridorX.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KorridorX.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015170000_AddAssetChannelAvailability")]
public partial class AddAssetChannelAvailability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IndividualEnabled",
            table: "Assets",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "BusinessEnabled",
            table: "Assets",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IndividualEnabled", table: "Assets");
        migrationBuilder.DropColumn(name: "BusinessEnabled", table: "Assets");
    }
}
