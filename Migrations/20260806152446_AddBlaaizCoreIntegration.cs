using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBlaaizCoreIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderExpiresAt",
                table: "Collections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCustomers_ProviderCode_CustomerProfileId",
                table: "ProviderCustomers",
                columns: new[] { "ProviderCode", "CustomerProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderCustomers_ProviderCode_CustomerProfileId",
                table: "ProviderCustomers");

            migrationBuilder.DropColumn(
                name: "ProviderExpiresAt",
                table: "Collections");
        }
    }
}
