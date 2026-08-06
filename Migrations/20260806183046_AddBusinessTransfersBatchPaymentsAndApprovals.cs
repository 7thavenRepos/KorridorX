using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTransfersBatchPaymentsAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "Transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalRejectionReason",
                table: "Transfers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryBankAccountId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessBeneficiaryMobileWalletId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessFundingSource",
                table: "Transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalApprovedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "Transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovals",
                table: "Transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedForApprovalAt",
                table: "Transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationCountryCode",
                table: "TransferQuotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceCountryCode",
                table: "TransferQuotes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TransferType",
                table: "TransferQuotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Permissions",
                table: "BusinessUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTransferCreatorApproval",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredTransferApprovals",
                table: "BusinessProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTransferApproval",
                table: "BusinessProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessPaymentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceCountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FundingSource = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    ValidItems = table.Column<int>(type: "integer", nullable: false),
                    InvalidItems = table.Column<int>(type: "integer", nullable: false),
                    CompletedItems = table.Column<int>(type: "integer", nullable: false),
                    FailedItems = table.Column<int>(type: "integer", nullable: false),
                    TotalSourceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    ApprovalCount = table.Column<int>(type: "integer", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPaymentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatches_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_AspNetUsers_ActionedByUserId",
                        column: x => x.ActionedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_BusinessPaymentBatches_BusinessPaymentBat~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessApprovals_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPaymentBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryBankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryMobileWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationCountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DestinationAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ProviderRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    PurposeNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidationErrors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TransferQuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPaymentBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaries_BusinessBen~",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaryBankAccounts_B~",
                        column: x => x.BusinessBeneficiaryBankAccountId,
                        principalTable: "BusinessBeneficiaryBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessBeneficiaryMobileWallets_~",
                        column: x => x.BusinessBeneficiaryMobileWalletId,
                        principalTable: "BusinessBeneficiaryMobileWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_BusinessPaymentBatches_BusinessPa~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_TransferQuotes_TransferQuoteId",
                        column: x => x.TransferQuoteId,
                        principalTable: "TransferQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentBatchItems_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ApprovalStatus",
                table: "Transfers",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryBankAccountId",
                table: "Transfers",
                column: "BusinessBeneficiaryBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryId",
                table: "Transfers",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessBeneficiaryMobileWalletId",
                table: "Transfers",
                column: "BusinessBeneficiaryMobileWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BusinessProfileId",
                table: "Transfers",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferQuotes_BusinessProfileId",
                table: "TransferQuotes",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_ActionedAt",
                table: "BusinessApprovals",
                column: "ActionedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_ActionedByUserId",
                table: "BusinessApprovals",
                column: "ActionedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessPaymentBatchId",
                table: "BusinessApprovals",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessPaymentBatchId_ActionedByUserId",
                table: "BusinessApprovals",
                columns: new[] { "BusinessPaymentBatchId", "ActionedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_BusinessProfileId",
                table: "BusinessApprovals",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_TransferId",
                table: "BusinessApprovals",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessApprovals_TransferId_ActionedByUserId",
                table: "BusinessApprovals",
                columns: new[] { "TransferId", "ActionedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_BusinessProfileId",
                table: "BusinessPaymentBatches",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_CreatedAt",
                table: "BusinessPaymentBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_Reference",
                table: "BusinessPaymentBatches",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatches_Status",
                table: "BusinessPaymentBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryBankAccountId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessBeneficiaryMobileWalletId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessBeneficiaryMobileWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessPaymentBatchId",
                table: "BusinessPaymentBatchItems",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_BusinessPaymentBatchId_RowNumber",
                table: "BusinessPaymentBatchItems",
                columns: new[] { "BusinessPaymentBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_TransferId",
                table: "BusinessPaymentBatchItems",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPaymentBatchItems_TransferQuoteId",
                table: "BusinessPaymentBatchItems",
                column: "TransferQuoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferQuotes_BusinessProfiles_BusinessProfileId",
                table: "TransferQuotes",
                column: "BusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaries_BusinessBeneficiaryId",
                table: "Transfers",
                column: "BusinessBeneficiaryId",
                principalTable: "BusinessBeneficiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryBankAccounts_BusinessBeneficia~",
                table: "Transfers",
                column: "BusinessBeneficiaryBankAccountId",
                principalTable: "BusinessBeneficiaryBankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryMobileWallets_BusinessBenefici~",
                table: "Transfers",
                column: "BusinessBeneficiaryMobileWalletId",
                principalTable: "BusinessBeneficiaryMobileWallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_BusinessProfiles_BusinessProfileId",
                table: "Transfers",
                column: "BusinessProfileId",
                principalTable: "BusinessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferQuotes_BusinessProfiles_BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaries_BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryBankAccounts_BusinessBeneficia~",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessBeneficiaryMobileWallets_BusinessBenefici~",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_BusinessProfiles_BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropTable(
                name: "BusinessApprovals");

            migrationBuilder.DropTable(
                name: "BusinessPaymentBatchItems");

            migrationBuilder.DropTable(
                name: "BusinessPaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_ApprovalStatus",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryBankAccountId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessBeneficiaryMobileWalletId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_TransferQuotes_BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovalRejectionReason",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryBankAccountId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessBeneficiaryMobileWalletId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessFundingSource",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "FinalApprovedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RequiredApprovals",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "SubmittedForApprovalAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BusinessProfileId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "DestinationCountryCode",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "SourceCountryCode",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "TransferType",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "BusinessUsers");

            migrationBuilder.DropColumn(
                name: "AllowTransferCreatorApproval",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "RequiredTransferApprovals",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "RequiresTransferApproval",
                table: "BusinessProfiles");

            migrationBuilder.DropColumn(
                name: "TransferApprovalThreshold",
                table: "BusinessProfiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "Transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerProfileId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
