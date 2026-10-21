using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationsAndAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgeBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScoringKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MilestoneIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentTemplates", x => x.Id);
                    table.CheckConstraint("CK_AssessmentTemplates_Scoring", "[ScoringKind] IN (N'binary', N'three_point')");
                    table.ForeignKey(
                        name: "FK_AssessmentTemplates_CurriculumFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "CurriculumFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObserverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgeBandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MilestoneIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShareWithParents = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Observations", x => x.Id);
                    table.CheckConstraint("CK_Observations_Type", "[Type] IN (N'formal', N'informal', N'concern')");
                    table.ForeignKey(
                        name: "FK_Observations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Observations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AssessedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StaffSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRuns", x => x.Id);
                    table.CheckConstraint("CK_AssessmentRuns_Status", "[Status] IN (N'draft', N'completed')");
                    table.ForeignKey(
                        name: "FK_AssessmentRuns_AssessmentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AssessmentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRuns_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentScores_AssessmentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AssessmentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentScores_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentScores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRuns_StudentId",
                table: "AssessmentRuns",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRuns_TemplateId",
                table: "AssessmentRuns",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRuns_TenantId_StudentId_AssessedOn",
                table: "AssessmentRuns",
                columns: new[] { "TenantId", "StudentId", "AssessedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRuns_TenantId_TemplateId_StudentId_Term",
                table: "AssessmentRuns",
                columns: new[] { "TenantId", "TemplateId", "StudentId", "Term" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentScores_MilestoneId",
                table: "AssessmentScores",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentScores_RunId",
                table: "AssessmentScores",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentScores_TenantId_RunId_MilestoneId",
                table: "AssessmentScores",
                columns: new[] { "TenantId", "RunId", "MilestoneId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentTemplates_FrameworkId",
                table: "AssessmentTemplates",
                column: "FrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentTemplates_TenantId",
                table: "AssessmentTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Observations_StudentId",
                table: "Observations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Observations_TenantId_CreatedAt",
                table: "Observations",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Observations_TenantId_StudentId_ObservedAt",
                table: "Observations",
                columns: new[] { "TenantId", "StudentId", "ObservedAt" });

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentScores");

            migrationBuilder.DropTable(
                name: "Observations");

            migrationBuilder.DropTable(
                name: "AssessmentRuns");

            migrationBuilder.DropTable(
                name: "AssessmentTemplates");
        }
    }
}
