using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorridorX.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSupportDisputesInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOperationalHold",
                table: "Transfers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OperationalHoldReason",
                table: "Transfers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstResponseDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolutionDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCustomerMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAgentMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    LastEscalatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorType = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisputeType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequestedRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_TransferDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_BusinessProfiles_BusinessProfileId",
                        column: x => x.BusinessProfileId,
                        principalTable: "BusinessProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferDisputes_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferInvestigations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferDisputeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Findings = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ProviderCaseReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_TransferInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferInvestigations_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferInvestigations_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferInvestigations_TransferDisputes_TransferDisputeId",
                        column: x => x.TransferDisputeId,
                        principalTable: "TransferDisputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferInvestigations_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferDisputeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransferInvestigationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StorageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportEvidence_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportEvidence_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportEvidence_TransferDisputes_TransferDisputeId",
                        column: x => x.TransferDisputeId,
                        principalTable: "TransferDisputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportEvidence_TransferInvestigations_TransferInvestigatio~",
                        column: x => x.TransferInvestigationId,
                        principalTable: "TransferInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_IsOperationalHold",
                table: "Transfers",
                column: "IsOperationalHold");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEvidence_SubmittedByUserId",
                table: "SupportEvidence",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEvidence_SupportTicketId",
                table: "SupportEvidence",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEvidence_TransferDisputeId",
                table: "SupportEvidence",
                column: "TransferDisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportEvidence_TransferInvestigationId",
                table: "SupportEvidence",
                column: "TransferInvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_AuthorUserId",
                table: "SupportTicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_SupportTicketId_CreatedAt",
                table: "SupportTicketMessages",
                columns: new[] { "SupportTicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedToUserId",
                table: "SupportTickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_BusinessProfileId",
                table: "SupportTickets",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CustomerProfileId",
                table: "SupportTickets",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_IsSlaBreached_Status",
                table: "SupportTickets",
                columns: new[] { "IsSlaBreached", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Reference",
                table: "SupportTickets",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_Priority_CreatedAt",
                table: "SupportTickets",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TransferId",
                table: "SupportTickets",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_AssignedToUserId",
                table: "TransferDisputes",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_BusinessProfileId",
                table: "TransferDisputes",
                column: "BusinessProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_CustomerProfileId",
                table: "TransferDisputes",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_Reference",
                table: "TransferDisputes",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_Status_CreatedAt",
                table: "TransferDisputes",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_SupportTicketId",
                table: "TransferDisputes",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_TransferId",
                table: "TransferDisputes",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferDisputes_UserId",
                table: "TransferDisputes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_AssignedToUserId",
                table: "TransferInvestigations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_Reference",
                table: "TransferInvestigations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_Status_DueAt",
                table: "TransferInvestigations",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_SupportTicketId",
                table: "TransferInvestigations",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_TransferDisputeId",
                table: "TransferInvestigations",
                column: "TransferDisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInvestigations_TransferId",
                table: "TransferInvestigations",
                column: "TransferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportEvidence");

            migrationBuilder.DropTable(
                name: "SupportTicketMessages");

            migrationBuilder.DropTable(
                name: "TransferInvestigations");

            migrationBuilder.DropTable(
                name: "TransferDisputes");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_IsOperationalHold",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "IsOperationalHold",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "OperationalHoldReason",
                table: "Transfers");
        }
    }
}
