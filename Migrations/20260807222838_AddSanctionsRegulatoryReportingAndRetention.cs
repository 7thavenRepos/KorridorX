using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionsRegulatoryReportingAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataRetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordType = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_DataRetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReportType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ComplianceCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RegulatoryAuthority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FilingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Narrative = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    SuspicionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ActivityStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivityEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PreparedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedForApprovalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FiledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilingDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastExportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RegulatoryReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryReports_ComplianceCases_ComplianceCaseId",
                        column: x => x.ComplianceCaseId,
                        principalTable: "ComplianceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataRetentionPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDryRun = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedLegalHoldCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionExecutionLogs_DataRetentionPolicies_DataRetentionP~",
                        column: x => x.DataRetentionPolicyId,
                        principalTable: "DataRetentionPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliesToAllComplianceData = table.Column<bool>(type: "boolean", nullable: false),
                    ComplianceCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegulatoryReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_LegalHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalHolds_ComplianceCases_ComplianceCaseId",
                        column: x => x.ComplianceCaseId,
                        principalTable: "ComplianceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalHolds_RegulatoryReports_RegulatoryReportId",
                        column: x => x.RegulatoryReportId,
                        principalTable: "RegulatoryReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataRetentionPolicies_IsActive",
                table: "DataRetentionPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DataRetentionPolicies_RecordType",
                table: "DataRetentionPolicies",
                column: "RecordType");

            migrationBuilder.CreateIndex(
                name: "IX_DataRetentionPolicies_RecordType_IsActive",
                table: "DataRetentionPolicies",
                columns: new[] { "RecordType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_ComplianceCaseId",
                table: "LegalHolds",
                column: "ComplianceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_EntityName_EntityId",
                table: "LegalHolds",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_Reference",
                table: "LegalHolds",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_RegulatoryReportId",
                table: "LegalHolds",
                column: "RegulatoryReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_Status",
                table: "LegalHolds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_Status_EffectiveAt",
                table: "LegalHolds",
                columns: new[] { "Status", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_ComplianceCaseId",
                table: "RegulatoryReports",
                column: "ComplianceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_FilingDueAt",
                table: "RegulatoryReports",
                column: "FilingDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_FilingReference",
                table: "RegulatoryReports",
                column: "FilingReference");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_JurisdictionCode",
                table: "RegulatoryReports",
                column: "JurisdictionCode");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_Reference",
                table: "RegulatoryReports",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_ReportType",
                table: "RegulatoryReports",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_Status",
                table: "RegulatoryReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReports_Status_FilingDueAt",
                table: "RegulatoryReports",
                columns: new[] { "Status", "FilingDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionExecutionLogs_CompletedAt",
                table: "RetentionExecutionLogs",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionExecutionLogs_DataRetentionPolicyId",
                table: "RetentionExecutionLogs",
                column: "DataRetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionExecutionLogs_IsDryRun",
                table: "RetentionExecutionLogs",
                column: "IsDryRun");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionExecutionLogs_StartedAt",
                table: "RetentionExecutionLogs",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalHolds");

            migrationBuilder.DropTable(
                name: "RetentionExecutionLogs");

            migrationBuilder.DropTable(
                name: "RegulatoryReports");

            migrationBuilder.DropTable(
                name: "DataRetentionPolicies");
        }
    }
}
