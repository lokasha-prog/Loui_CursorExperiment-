using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRetentionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cutoff = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowsDeleted = table.Column<int>(type: "int", nullable: false),
                    ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AuditRetentionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditRetentionRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditRetentionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    AutoPurgeEnabled = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AuditRetentionSettings", x => x.Id);
                    table.CheckConstraint("CK_AuditRetentionSettings_Days", "[RetentionDays] >= 365");
                    table.ForeignKey(
                        name: "FK_AuditRetentionSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionRuns_TenantId_CreatedAt",
                table: "AuditRetentionRuns",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionRuns_TenantId_RunAt",
                table: "AuditRetentionRuns",
                columns: new[] { "TenantId", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionSettings_TenantId",
                table: "AuditRetentionSettings",
                column: "TenantId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(@"
ALTER TRIGGER [dbo].[TR_AuditLogs_Immutable] ON [dbo].[AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    -- History can only be removed by the retention procedure of the app (it sets audit_purge for one statement),
    -- and never when it is younger than a year. Everything else, including every UPDATE, is refused.
    IF NOT EXISTS (SELECT 1 FROM inserted) AND CAST(SESSION_CONTEXT(N'audit_purge') AS int) = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM deleted WHERE OccurredAt >= DATEADD(DAY, -365, SYSDATETIMEOFFSET()))
        BEGIN
            THROW 51002, N'AuditLogs entries younger than 365 days can never be removed.', 1;
        END;
        DELETE a FROM [dbo].[AuditLogs] a INNER JOIN deleted d ON d.Id = a.Id;
        RETURN;
    END;
    THROW 51000, N'AuditLogs is append-only. UPDATE and DELETE are not permitted.', 1;
END");

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SECURITY POLICY [Security].[TenantIsolationPolicy];");
            migrationBuilder.Sql(@"
ALTER TRIGGER [dbo].[TR_AuditLogs_Immutable] ON [dbo].[AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'AuditLogs is append-only. UPDATE and DELETE are not permitted.', 1;
END");

            migrationBuilder.DropTable(
                name: "AuditRetentionRuns");

            migrationBuilder.DropTable(
                name: "AuditRetentionSettings");

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");
        }
    }
}
