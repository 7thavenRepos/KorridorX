using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionsPepTransactionMonitoringComplianceCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreeningRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficialOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistrationNumberLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HighestMatchScore = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScreenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ScreeningRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_BusinessBeneficialOwners_BusinessBeneficia~",
                        column: x => x.BusinessBeneficialOwnerId,
                        principalTable: "BusinessBeneficialOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_BusinessBeneficiaries_BusinessBeneficiaryId",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScreeningRecords_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CaseType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessBeneficialOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScreeningRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmlFlagId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ComplianceCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_AmlFlags_AmlFlagId",
                        column: x => x.AmlFlagId,
                        principalTable: "AmlFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_BusinessBeneficialOwners_BusinessBeneficial~",
                        column: x => x.BusinessBeneficialOwnerId,
                        principalTable: "BusinessBeneficialOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_BusinessBeneficiaries_BusinessBeneficiaryId",
                        column: x => x.BusinessBeneficiaryId,
                        principalTable: "BusinessBeneficiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_ScreeningRecords_ScreeningRecordId",
                        column: x => x.ScreeningRecordId,
                        principalTable: "ScreeningRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceCases_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreeningRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchlistType = table.Column<int>(type: "integer", nullable: false),
                    ListName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MatchedName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProviderMatchId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MatchScore = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    MatchReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsFalsePositive = table.Column<bool>(type: "boolean", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningMatches_ScreeningRecords_ScreeningRecordId",
                        column: x => x.ScreeningRecordId,
                        principalTable: "ScreeningRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceCaseEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCaseEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceCaseEvidence_ComplianceCases_ComplianceCaseId",
                        column: x => x.ComplianceCaseId,
                        principalTable: "ComplianceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceCaseNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCaseNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceCaseNotes_ComplianceCases_ComplianceCaseId",
                        column: x => x.ComplianceCaseId,
                        principalTable: "ComplianceCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseEvidence_AddedByUserId",
                table: "ComplianceCaseEvidence",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseEvidence_ComplianceCaseId",
                table: "ComplianceCaseEvidence",
                column: "ComplianceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseEvidence_CreatedAt",
                table: "ComplianceCaseEvidence",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseEvidence_EvidenceType",
                table: "ComplianceCaseEvidence",
                column: "EvidenceType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseNotes_ComplianceCaseId",
                table: "ComplianceCaseNotes",
                column: "ComplianceCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseNotes_CreatedAt",
                table: "ComplianceCaseNotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCaseNotes_CreatedByUserId",
                table: "ComplianceCaseNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_AmlFlagId",
                table: "ComplianceCases",
                column: "AmlFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_AssignedToUserId",
                table: "ComplianceCases",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_BusinessBeneficialOwnerId",
                table: "ComplianceCases",
                column: "BusinessBeneficialOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_BusinessBeneficiaryId",
                table: "ComplianceCases",
                column: "BusinessBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_BusinessProfileId",
                table: "ComplianceCases",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_CaseType",
                table: "ComplianceCases",
                column: "CaseType");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_CustomerProfileId",
                table: "ComplianceCases",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_DueAt",
                table: "ComplianceCases",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_IsBlocking",
                table: "ComplianceCases",
                column: "IsBlocking");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_OpenedAt",
                table: "ComplianceCases",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_Priority",
                table: "ComplianceCases",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_RecipientId",
                table: "ComplianceCases",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_Reference",
                table: "ComplianceCases",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_ScreeningRecordId",
                table: "ComplianceCases",
                column: "ScreeningRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_Status",
                table: "ComplianceCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_Status_IsBlocking_Priority_OpenedAt",
                table: "ComplianceCases",
                columns: new[] { "Status", "IsBlocking", "Priority", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_TransferId",
                table: "ComplianceCases",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningMatches_IsFalsePositive",
                table: "ScreeningMatches",
                column: "IsFalsePositive");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningMatches_MatchScore",
                table: "ScreeningMatches",
                column: "MatchScore");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningMatches_ProviderMatchId",
                table: "ScreeningMatches",
                column: "ProviderMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningMatches_ScreeningRecordId",
                table: "ScreeningMatches",
                column: "ScreeningRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningMatches_WatchlistType",
                table: "ScreeningMatches",
                column: "WatchlistType");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_BusinessBeneficialOwnerId_SubjectType_Scre~",
                table: "ScreeningRecords",
                columns: new[] { "BusinessBeneficialOwnerId", "SubjectType", "ScreenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_BusinessBeneficiaryId_SubjectType_Screened~",
                table: "ScreeningRecords",
                columns: new[] { "BusinessBeneficiaryId", "SubjectType", "ScreenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_BusinessProfileId_SubjectType_ScreenedAt",
                table: "ScreeningRecords",
                columns: new[] { "BusinessProfileId", "SubjectType", "ScreenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_CustomerProfileId_SubjectType_ScreenedAt",
                table: "ScreeningRecords",
                columns: new[] { "CustomerProfileId", "SubjectType", "ScreenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_ExpiresAt",
                table: "ScreeningRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_IsBlocking",
                table: "ScreeningRecords",
                column: "IsBlocking");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_ProviderReference",
                table: "ScreeningRecords",
                column: "ProviderReference");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_RecipientId_SubjectType_ScreenedAt",
                table: "ScreeningRecords",
                columns: new[] { "RecipientId", "SubjectType", "ScreenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_ScreenedAt",
                table: "ScreeningRecords",
                column: "ScreenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_Status",
                table: "ScreeningRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_SubjectType",
                table: "ScreeningRecords",
                column: "SubjectType");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningRecords_TransferId_ScreenedAt",
                table: "ScreeningRecords",
                columns: new[] { "TransferId", "ScreenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceCaseEvidence");

            migrationBuilder.DropTable(
                name: "ComplianceCaseNotes");

            migrationBuilder.DropTable(
                name: "ScreeningMatches");

            migrationBuilder.DropTable(
                name: "ComplianceCases");

            migrationBuilder.DropTable(
                name: "ScreeningRecords");
        }
    }
}
