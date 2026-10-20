using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Database-level safety nets (spec 07 §1.3, §1.6, §5): row-level security that keeps every tenant's rows apart,
    /// and an append-only audit log. Later migrations that add tenant tables must finish with
    /// EXEC [Security].[RefreshTenantPolicy], which re-applies the policy to every table that has a TenantId column.
    /// </summary>
    public partial class TenantIsolationAndAuditImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SCHEMA [Security];");

            migrationBuilder.Sql(@"
CREATE FUNCTION [Security].[fn_TenantAccessPredicate](@TenantId uniqueidentifier)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS [Allowed]
WHERE @TenantId = CAST(SESSION_CONTEXT(N'tenant_id') AS uniqueidentifier);");

            migrationBuilder.Sql(@"
CREATE PROCEDURE [Security].[RefreshTenantPolicy]
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy' AND schema_id = SCHEMA_ID(N'Security'))
        DROP SECURITY POLICY [Security].[TenantIsolationPolicy];

    DECLARE @clauses nvarchar(max) = N'';
    SELECT @clauses = @clauses
        + N'ADD FILTER PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N','
        + N'ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' AFTER INSERT,'
        + N'ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' AFTER UPDATE,'
        + N'ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' BEFORE UPDATE,'
        + N'ADD BLOCK PREDICATE [Security].[fn_TenantAccessPredicate]([TenantId]) ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' BEFORE DELETE,'
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'TenantId'
    WHERE s.name <> N'Security';

    IF LEN(@clauses) = 0 RETURN;
    SET @clauses = LEFT(@clauses, LEN(@clauses) - 1);
    EXEC (N'CREATE SECURITY POLICY [Security].[TenantIsolationPolicy] ' + @clauses + N' WITH (STATE = ON);');
END");

            migrationBuilder.Sql("EXEC [Security].[RefreshTenantPolicy];");

            migrationBuilder.Sql(@"
CREATE TRIGGER [dbo].[TR_AuditLogs_Immutable] ON [dbo].[AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'AuditLogs is append-only. UPDATE and DELETE are not permitted.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [dbo].[TR_AuditLogs_Immutable];");
            migrationBuilder.Sql("DROP SECURITY POLICY [Security].[TenantIsolationPolicy];");
            migrationBuilder.Sql("DROP PROCEDURE [Security].[RefreshTenantPolicy];");
            migrationBuilder.Sql("DROP FUNCTION [Security].[fn_TenantAccessPredicate];");
            migrationBuilder.Sql("DROP SCHEMA [Security];");
        }
    }
}
