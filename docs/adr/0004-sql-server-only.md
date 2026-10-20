# ADR-0004: Microsoft SQL Server is the only data store (Golden Rule)
- Status: Accepted (owner decision, 2026-10-20). Supersedes the PostgreSQL 16 choice in specs 05, 07, 10.
- Rule: every data store in every phase is the owner's local Microsoft SQL Server. No other database technology. No seeded data and no hard-coded values; configuration comes from settings/secrets, data comes from users.
- Consequences:
  - EF Core SqlServer provider. JSONB fields become JSON text (nvarchar(max)) via value converters.
  - Tenant isolation: SQL Server Row-Level Security (security policy with filter + block predicates reading SESSION_CONTEXT 'tenant_id') plus EF Core query filters.
  - Audit log immutability: INSTEAD OF UPDATE/DELETE trigger. Monthly partitioning of audit_log is deferred until a maintenance job exists (Sprint 2).
  - Column names use EF defaults (PascalCase) rather than snake_case.
  - Tests create their own data at run time and clean it up; there are no fixtures or seed scripts.
