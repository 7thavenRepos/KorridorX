using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessFundingWalletLedgerCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SettledBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeldBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_BusinessWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWallets_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_BusinessLedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_BusinessProfiles_BusinessProfile~",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerTransactions_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessWalletReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPaymentBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessWalletReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessPaymentBatches_BusinessP~",
                        column: x => x.BusinessPaymentBatchId,
                        principalTable: "BusinessPaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessWalletReservations_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessWalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountBalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessLedgerTransactions_BusinessLe~",
                        column: x => x.BusinessLedgerTransactionId,
                        principalTable: "BusinessLedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessLedgerEntries_BusinessWallets_BusinessWalletId",
                        column: x => x.BusinessWalletId,
                        principalTable: "BusinessWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_ReadAt",
                table: "NotificationMessages",
                column: "ReadAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessLedgerTransactionId",
                table: "BusinessLedgerEntries",
                column: "BusinessLedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerEntries_BusinessWalletId",
                table: "BusinessLedgerEntries",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessPaymentBatchId",
                table: "BusinessLedgerTransactions",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_BusinessProfileId",
                table: "BusinessLedgerTransactions",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_CollectionId",
                table: "BusinessLedgerTransactions",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_PostedAt",
                table: "BusinessLedgerTransactions",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_Reference",
                table: "BusinessLedgerTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessLedgerTransactions_TransferId",
                table: "BusinessLedgerTransactions",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessPaymentBatchId",
                table: "BusinessWalletReservations",
                column: "BusinessPaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_BusinessWalletId",
                table: "BusinessWalletReservations",
                column: "BusinessWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Reference",
                table: "BusinessWalletReservations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_Status",
                table: "BusinessWalletReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWalletReservations_TransferId",
                table: "BusinessWalletReservations",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_BusinessProfileId_CurrencyCode",
                table: "BusinessWallets",
                columns: new[] { "BusinessProfileId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessWallets_Status",
                table: "BusinessWallets",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessLedgerEntries");

            migrationBuilder.DropTable(
                name: "BusinessWalletReservations");

            migrationBuilder.DropTable(
                name: "BusinessLedgerTransactions");

            migrationBuilder.DropTable(
                name: "BusinessWallets");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_ReadAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "NotificationMessages");
        }
    }
}
