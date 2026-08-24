using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundFundsRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboundFundsRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectDisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    InternalReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LiftedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LiftReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_OutboundFundsRestrictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundFundsRestrictions_AppliedByUserId",
                table: "OutboundFundsRestrictions",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundFundsRestrictions_IsActive_SubjectType_AppliedAt",
                table: "OutboundFundsRestrictions",
                columns: new[] { "IsActive", "SubjectType", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundFundsRestrictions_LiftedByUserId",
                table: "OutboundFundsRestrictions",
                column: "LiftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundFundsRestrictions_Source",
                table: "OutboundFundsRestrictions",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundFundsRestrictions_SubjectType_SubjectId",
                table: "OutboundFundsRestrictions",
                columns: new[] { "SubjectType", "SubjectId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboundFundsRestrictions");
        }
    }
}
