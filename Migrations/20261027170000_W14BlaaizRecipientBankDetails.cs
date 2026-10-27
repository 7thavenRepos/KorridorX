using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace KorridorX.Migrations
{
    public partial class W14BlaaizRecipientBankDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "RecipientType", table: "RecipientBankAccounts", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "AccountType", table: "RecipientBankAccounts", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Street", table: "RecipientBankAccounts", type: "character varying(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(name: "City", table: "RecipientBankAccounts", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "State", table: "RecipientBankAccounts", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PostalCode", table: "RecipientBankAccounts", type: "character varying(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<int>(name: "PayoutMethod", table: "RecipientBankAccounts", type: "integer", nullable: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RecipientType", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "AccountType", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "Street", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "City", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "State", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "PostalCode", table: "RecipientBankAccounts");
            migrationBuilder.DropColumn(name: "PayoutMethod", table: "RecipientBankAccounts");
        }
    }
}
