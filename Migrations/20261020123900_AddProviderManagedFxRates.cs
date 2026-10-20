using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderManagedFxRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FxRateObservationId",
                table: "TransferQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FxRateObservationId",
                table: "InstantQuotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FxProviderRateConfigurationId",
                table: "ExchangeRates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FxRateObservationId",
                table: "ExchangeRates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FxProviderRateConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstantPairId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketplacePairId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderRateId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MarkupPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    MaximumCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActivatedConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ScheduleMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    DailyTimes = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NextRefreshAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefreshLeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastObservationStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProviderTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastProviderRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LastCustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
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
                    table.PrimaryKey("PK_FxProviderRateConfigurations", x => x.Id);
                    table.CheckConstraint("CK_FxProviderRateConfigurations_Markup", "\"MarkupPercentage\" >= 0 AND \"MarkupPercentage\" < 100");
                    table.CheckConstraint("CK_FxProviderRateConfigurations_Pair", "(\"InstantPairId\" IS NOT NULL) <> (\"MarketplacePairId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FxProviderRateConfigurations_InstantPairs_InstantPairId",
                        column: x => x.InstantPairId,
                        principalTable: "InstantPairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FxProviderRateConfigurations_MarketplacePairs_MarketplacePa~",
                        column: x => x.MarketplacePairId,
                        principalTable: "MarketplacePairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FxRateObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationRevision = table.Column<long>(type: "bigint", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderRateId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    CustomerRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MarkupPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderRequestLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    SendEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SwapEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRateObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FxRateObservations_FxProviderRateConfigurations_Configurati~",
                        column: x => x.ConfigurationId,
                        principalTable: "FxProviderRateConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferQuotes_FxRateObservationId",
                table: "TransferQuotes",
                column: "FxRateObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_InstantQuotes_FxRateObservationId",
                table: "InstantQuotes",
                column: "FxRateObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_FxProviderRateConfigurationId",
                table: "ExchangeRates",
                column: "FxProviderRateConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_FxRateObservationId",
                table: "ExchangeRates",
                column: "FxRateObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_FxProviderRateConfigurations_InstantPairId",
                table: "FxProviderRateConfigurations",
                column: "InstantPairId");

            migrationBuilder.CreateIndex(
                name: "IX_FxProviderRateConfigurations_IsActive_NextRefreshAtUtc",
                table: "FxProviderRateConfigurations",
                columns: new[] { "IsActive", "NextRefreshAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FxProviderRateConfigurations_MarketplacePairId",
                table: "FxProviderRateConfigurations",
                column: "MarketplacePairId");

            migrationBuilder.CreateIndex(
                name: "UX_FxProviderRateConfigurations_ActiveDirection",
                table: "FxProviderRateConfigurations",
                columns: new[] { "SourceCurrencyCode", "DestinationCurrencyCode" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_FxRateObservations_ConfigurationId_ReceivedAtUtc",
                table: "FxRateObservations",
                columns: new[] { "ConfigurationId", "ReceivedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_FxProviderRateConfigurations_FxProviderRateCo~",
                table: "ExchangeRates",
                column: "FxProviderRateConfigurationId",
                principalTable: "FxProviderRateConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExchangeRates_FxRateObservations_FxRateObservationId",
                table: "ExchangeRates",
                column: "FxRateObservationId",
                principalTable: "FxRateObservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InstantQuotes_FxRateObservations_FxRateObservationId",
                table: "InstantQuotes",
                column: "FxRateObservationId",
                principalTable: "FxRateObservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferQuotes_FxRateObservations_FxRateObservationId",
                table: "TransferQuotes",
                column: "FxRateObservationId",
                principalTable: "FxRateObservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_FxProviderRateConfigurations_FxProviderRateCo~",
                table: "ExchangeRates");

            migrationBuilder.DropForeignKey(
                name: "FK_ExchangeRates_FxRateObservations_FxRateObservationId",
                table: "ExchangeRates");

            migrationBuilder.DropForeignKey(
                name: "FK_InstantQuotes_FxRateObservations_FxRateObservationId",
                table: "InstantQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferQuotes_FxRateObservations_FxRateObservationId",
                table: "TransferQuotes");

            migrationBuilder.DropTable(
                name: "FxRateObservations");

            migrationBuilder.DropTable(
                name: "FxProviderRateConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_TransferQuotes_FxRateObservationId",
                table: "TransferQuotes");

            migrationBuilder.DropIndex(
                name: "IX_InstantQuotes_FxRateObservationId",
                table: "InstantQuotes");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_FxProviderRateConfigurationId",
                table: "ExchangeRates");

            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_FxRateObservationId",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "FxRateObservationId",
                table: "TransferQuotes");

            migrationBuilder.DropColumn(
                name: "FxRateObservationId",
                table: "InstantQuotes");

            migrationBuilder.DropColumn(
                name: "FxProviderRateConfigurationId",
                table: "ExchangeRates");

            migrationBuilder.DropColumn(
                name: "FxRateObservationId",
                table: "ExchangeRates");
        }
    }
}
