using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class G5G_BusinessBeneficiaryPayoutDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutMethod",
                table: "BusinessBeneficiaryBankAccounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientType",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "BusinessBeneficiaryBankAccounts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "City",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "PayoutMethod",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "State",
                table: "BusinessBeneficiaryBankAccounts");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "BusinessBeneficiaryBankAccounts");
        }
    }
}
