# 10 — Multi-Tenancy Architecture

> How NMS supports both **on-prem single-tenant** and **SaaS multi-tenant** deployments from a single codebase.

---

## 1. Tenancy Models Overview

NMS supports two deployment modes from one codebase:

| Mode | Tenant count per deployment | Use case | Commercial model |
|---|---|---|---|
| **On-prem** | 1 (single-tenant) | Customer requires data sovereignty, has own infra, or wants full control | One-time license + annual maintenance |
| **SaaS** | Many (multi-tenant, shared schema + RLS) | Customer wants low-capex, fast onboarding, hands-off ops | Monthly/annual subscription per centre |

### 1.1 Why Same Codebase

Building two codebases (one for on-prem, one for SaaS) is unacceptable:
- 2× development cost.
- Feature parity drift.
- Bug fixes applied twice.
- Customer migration between modes impossible.

Instead, NMS is built **multi-tenant-ready** from day one. On-prem deployment is simply a multi-tenant deployment with one tenant.

### 1.2 The Key Insight

> An on-prem single-tenant deployment IS a multi-tenant deployment with `tenant_count = 1`.

Every entity has a `tenant_id` column. Every query is scoped by `tenant_id`. Every RLS policy enforces `tenant_id` match. In on-prem, there's one `tenant_id` value throughout the deployment. In SaaS, there are many.

This means:
- No conditional code paths for "single-tenant vs multi-tenant".
- No schema differences.
- No API differences.
- Only configuration + deployment topology differs.

---

## 2. Multi-Tenancy Strategy: Shared Schema + RLS

### 2.1 Approach

We use **shared schema with `tenant_id` column discrimination**, enforced by:
1. **Application layer**: EF Core global query filter (`HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`).
2. **Database layer**: PostgreSQL Row Level Security (RLS) policies.

This is defense-in-depth: even if a bug in EF Core's filter allows a row to leak, RLS rejects it at the DB layer.

### 2.2 Why Not Schema-per-Tenant

| Approach | Pros | Cons |
|---|---|---|
| **Shared schema + RLS** ✅ | Simple operations, single migration, easy reporting across tenants, lowest cost | One bad query can leak (mitigated by RLS) |
| Schema-per-tenant | Strong isolation | N× migrations, complex operations, hard cross-tenant reporting, expensive (N× connections) |
| Database-per-tenant | Strongest isolation | Most expensive, hardest to operate, doesn't fit on-prem model |

We chose shared schema + RLS because:
- It's the best fit for both SaaS (many tenants, low cost) and on-prem (one tenant, simple ops).
- RLS provides strong isolation guarantees at the DB layer.
- Cross-tenant reporting (for MCG consolidated views in Phase 3) is easy.
- Migrations run once per deployment (not N times).

### 2.3 Why Not Application-Layer-Only

Some frameworks rely only on application-layer filtering (e.g., EF Core's `HasQueryFilter`). This is risky:
- A bug in the filter (e.g., `AsNoTracking()` bypasses it in some versions) → cross-tenant data leak.
- Raw SQL queries (for performance) bypass the filter.
- Background jobs (Hangfire) need to remember to set tenant context.

RLS at the DB layer is non-bypassable (except via `service` role, which is restricted). It's the safety net.

---

## 3. Tenant Lifecycle

### 3.1 SaaS Tenant Lifecycle

```
1. Provisioning (Super Admin / self-service signup)
   ├── Create tenant record
   ├── Create Keycloak realm (or use shared realm with tenant claim)
   ├── Create database schema (no action — shared schema)
   ├── Run seed data (default roles, default settings, default ratio rules)
   ├── Create Super Admin user (customer's first admin)
   ├── Send welcome email with onboarding link
   └── Start billing (Stripe subscription or manual invoice)

2. Onboarding (Super Admin)
   ├── Complete centre profile (name, logo, address, branding)
   ├── Configure ratio rules per age band
   ├── Configure weekend days, holidays, working hours
   ├── Create rooms, programs, groups
   ├── Create staff accounts (with role assignments)
   ├── Create students (with guardians)
   ├── Configure billing (fee plans, invoice schedule)
   └── Go live (start using with real data)

3. Active
   ├── Daily operations
   ├── Billing (monthly SaaS fee + per-student fees)
   ├── Support (in-app, email)
   └── Monitoring (uptime, performance, errors)

4. Suspension (rare)
   ├── Triggers: non-payment, abuse, customer request
   ├── Tenant status → 'suspended'
   ├── All logins blocked (refresh tokens revoked)
   ├── Data retained for 90 days (recovery window)
   └── Reactivation possible within 90 days

5. Archival / Cancellation
   ├── Triggers: customer request after suspension, legal hold release
   ├── Tenant status → 'archived'
   ├── Data retained per compliance (proposed: 7 years for attendance, 10 for medical)
   ├── Soft-deleted (is_deleted = true) but data preserved
   ├── Backups retained per policy
   └── Eventually hard-deleted after retention period (with legal sign-off)
```

### 3.2 On-Prem Tenant Lifecycle

```
1. Sales + Contract
   ├── Customer signs license agreement
   ├── Implementation services scoped
   └── Deployment target identified (customer cloud or on-prem servers)

2. Deployment
   ├── Provision infrastructure (Terraform)
   ├── Deploy NMS (Helm install)
   ├── Configure Keycloak realm
   ├── Configure customer's SMTP, storage, identity federation (if any)
   ├── Run seed data
   └── Create Super Admin (customer's designated admin)

3. Onboarding (same as SaaS)

4. Active
   ├── Customer operates the system
   ├── We provide support per SLA
   ├── Updates: customer controls cadence (we notify, they apply)
   └── Monitoring: customer's responsibility (we provide tooling)

5. License Expiry / Termination
   ├── Customer continues to use existing version (no support)
   ├── OR customer migrates to SaaS (data migration services)
   ├── OR customer decommissions (data exported, deleted per contract)
   └── Source code escrow (if contractual) released to customer
```

---

## 4. Tenant Context Propagation

### 4.1 Request Flow

```
1. Client makes API request with JWT in Authorization header
2. ASP.NET Core auth middleware validates JWT (via Keycloak public key)
3. JWT contains 'tenant_id' claim (set by Keycloak mapper based on user's realm)
4. TenantContextMiddleware extracts tenant_id, stores in ITenantContext (scoped service)
5. EF Core DbContext uses ITenantContext.TenantId in global query filter
6. Before any DB query, middleware executes: SELECT set_config('app.tenant_id', '<uuid>', true)
7. PostgreSQL RLS policies enforce tenant_id = current_setting('app.tenant_id')::uuid
8. Query results are guaranteed tenant-scoped
```

### 4.2 Implementation

```csharp
// ITenantContext.cs
public interface ITenantContext
{
    Guid TenantId { get; set; }
    string TenantSlug { get; set; }
    bool IsInitialized { get; }
}

// TenantContext.cs (Scoped)
public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; }
    public bool IsInitialized => TenantId != Guid.Empty;
}

// TenantContextMiddleware.cs
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        NmsDbContext db,
        ICurrentUser currentUser)
    {
        // Skip for unauthenticated endpoints
        var path = context.Request.Path.Value ?? "";
        if (IsUnauthenticatedPath(path))
        {
            await _next(context);
            return;
        }

        // Extract tenant_id from JWT
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        tenantContext.TenantId = tenantId;

        // Set RLS context (transaction-scoped)
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");

        await _next(context);
    }

    private static bool IsUnauthenticatedPath(string path) =>
        path.StartsWith("/api/v1/auth/login") ||
        path.StartsWith("/api/v1/auth/refresh") ||
        path.StartsWith("/api/v1/auth/forgot-password") ||
        path.StartsWith("/api/v1/auth/reset-password") ||
        path.StartsWith("/api/v1/auth/bootstrap") ||
        path.StartsWith("/api/v1/auth/accept-invitation") ||
        path.StartsWith("/api/v1/public/");
}
```

### 4.3 Background Jobs (Hangfire)

Background jobs run outside the HTTP request pipeline, so tenant context must be set explicitly:

```csharp
// TenantAwareJobFilter.cs (Hangfire filter)
public class TenantAwareJobFilter : IClientFilter, IServerFilter
{
    public void OnPerforming(PerformingContext context)
    {
        var tenantId = context.GetJobParameter<Guid>("TenantId");
        if (tenantId == Guid.Empty) return;

        // Set tenant context for this job
        var tenantContext = context.Items["ServiceProvider"]
            .GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        // Set RLS context
        var db = context.Items["ServiceProvider"]
            .GetRequiredService<NmsDbContext>();
        db.Database.ExecuteSqlInterpolated(
            $"SELECT set_config('app.tenant_id', {tenantId.ToString()}, true)");
    }
}

// When enqueuing a job:
_backgroundJobClient.Enqueue<GenerateInvoicesJob>(job => 
    job.ExecuteAsync(tenantId, CancellationToken.None));
// Job parameters include TenantId automatically via filter
```

### 4.4 RabbitMQ Consumers

Same pattern — consumer sets tenant context before processing message:

```csharp
public class MediaUploadedConsumer : IConsumer<MediaUploadedEvent>
{
    private readonly ITenantContext _tenantContext;
    private readonly NmsDbContext _db;

    public async Task Consume(ConsumeContext<MediaUploadedEvent> context)
    {
        _tenantContext.TenantId = context.Message.TenantId;
        
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {context.Message.TenantId.ToString()}, true)");
        
        // Process message...
    }
}
```

All events published to RabbitMQ include `TenantId` in the message envelope.

---

## 5. Database RLS Setup

### 5.1 Enable RLS on All Tenant Tables

```sql
-- Function to set tenant context
CREATE OR REPLACE FUNCTION set_tenant_context(p_tenant_id UUID)
RETURNS void AS $$
BEGIN
  PERFORM set_config('app.tenant_id', p_tenant_id::text, true);
END;
$$ LANGUAGE plpgsql;

-- Function to get current tenant (returns sentinel if not set, matches nothing)
CREATE OR REPLACE FUNCTION current_tenant_id()
RETURNS UUID AS $$
BEGIN
  RETURN COALESCE(
    NULLIF(current_setting('app.tenant_id', true), '')::UUID,
    '00000000-0000-0000-0000-000000000000'::UUID
  );
END;
$$ LANGUAGE plpgsql STABLE;

-- Apply RLS to every table with tenant_id column
DO $$
DECLARE
  t RECORD;
BEGIN
  FOR t IN
    SELECT table_name 
    FROM information_schema.columns
    WHERE column_name = 'tenant_id'
      AND table_schema = 'public'
      AND table_name NOT IN ('tenant')  -- tenant table itself
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', t.table_name);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY;', t.table_name);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I;', t.table_name);
    EXECUTE format('
      CREATE POLICY tenant_isolation ON %I
        USING (tenant_id = current_tenant_id())
        WITH CHECK (tenant_id = current_tenant_id());
    ', t.table_name);
  END LOOP;
END $$;
```

### 5.2 Service Role (Bypass RLS)

For migrations, Hangfire jobs that span tenants, and platform ops team:

```sql
-- Service role bypasses RLS (used by migrations, cross-tenant jobs, ops team)
CREATE ROLE service LOGIN BYPASSRLS PASSWORD '<from secrets manager>';

-- Application role (RLS enforced)
CREATE ROLE app LOGIN PASSWORD '<from secrets manager>';
GRANT USAGE ON SCHEMA public TO app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO app;

-- Default privileges for future tables
ALTER DEFAULT PRIVILEGES IN SCHEMA public 
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public 
  GRANT USAGE ON SEQUENCES TO app;
```

**Connection string in production**:
- App uses `app` role (RLS enforced).
- Migrations use `service` role (BYPASSRLS).
- Hangfire cross-tenant jobs use `service` role.
- Platform ops team uses `service` role for forensic queries (audited at infra level).

### 5.3 Migration Runner

EF Core migrations use `service` role:

```csharp
// MigrationRunner.cs (separate console app or init container)
var connectionString = $"Host=...;Database=...;Username=service;Password={servicePassword};...";
var options = new DbContextOptionsBuilder<NmsDbContext>()
    .UseNpgsql(connectionString)
    .Options;
var db = new NmsDbContext(options);
await db.Database.MigrateAsync();
```

---

## 6. On-Prem Single-Tenant Deployment

### 6.1 Configuration

```jsonc
// appsettings.json (on-prem)
{
  "Deployment": {
    "Mode": "OnPrem",
    "SingleTenantMode": true
  },
  "MultiTenancy": {
    "Enabled": true,  // Still enabled — just one tenant
    "DefaultTenantId": "<fixed-uuid-set-at-install>"  // Pre-provisioned
  },
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "..."  // From customer's secrets
  },
  // ...
}
```

### 6.2 Bootstrap Process

1. Customer installs NMS via Helm chart on their K8s cluster (or docker-compose for smaller deployments).
2. Helm chart runs init container that:
   - Creates database if not exists.
   - Runs migrations.
   - Provisions the single tenant (with fixed UUID, configurable in values.yaml).
   - Creates Keycloak realm for the tenant.
   - Generates bootstrap Super Admin invitation token.
3. Customer's designated Super Admin opens bootstrap URL, sets up account.
4. Onboarding wizard starts.

### 6.3 Single-Tenant Optimizations

Even though the codebase is multi-tenant, on-prem deployments benefit from:
- **No tenant context overhead**: `tenant_id` is always the same value. RLS policies always match. No performance impact.
- **Simpler backups**: One database, one backup job.
- **Simpler monitoring**: No need for per-tenant dashboards (only one tenant).
- **Custom branding**: Customer's branding applied at tenant level (same as SaaS).

### 6.4 Updating

- Customer pulls new Docker image from our registry (or air-gapped registry mirror).
- Helm upgrade runs migrations + restarts pods.
- Zero downtime (blue/green deployment).
- Customer controls cadence (we notify of new releases; customer applies when ready).
- LTS branches: we maintain 2 LTS versions (e.g., 1.x and 2.x) for customers who don't want bleeding edge.

### 6.5 Customer Identity Federation

On-prem customers may have existing Active Directory / LDAP:

```
Keycloak (our instance, on customer's infra)
  ↓ User Federation
Customer's Active Directory / LDAP
```

- Staff authenticate via AD credentials (no separate password).
- Parents still use Keycloak local accounts (no AD account).
- Optional: SSO via SAML for staff (if customer has SAML IdP).

Configuration in Keycloak:
- User Federation → LDAP → Configure connection (URL, bind DN, bind password).
- Mappers: map AD `sAMAccountName` to Keycloak `username`, AD `memberOf` to Keycloak roles.

---

## 7. SaaS Multi-Tenant Deployment

### 7.1 Configuration

```jsonc
// appsettings.json (SaaS)
{
  "Deployment": {
    "Mode": "SaaS",
    "MultiTenantMode": true
  },
  "MultiTenancy": {
    "Enabled": true,
    "DefaultTenantId": null  // Resolved per-request from JWT
  },
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "..."  // From AWS Secrets Manager
  },
  // ...
}
```

### 7.2 Tenant Provisioning Flow (Self-Service Signup)

```
1. Nursery owner visits https://nms.ae, clicks "Start Free Trial"
2. Signs up with email + nursery name + phone
3. Backend:
   a. Creates tenant record (status='active', subscription_tier='trial', subscription_ends_at=now+30d)
   b. Creates Keycloak realm (e.g., 'bright-futures-nursery')
   c. Creates Keycloak client (nms-web, nms-mobile) in realm
   d. Creates Super Admin user in Keycloak
   e. Sends welcome email with onboarding link
   f. Creates Stripe customer + starts trial subscription
4. Super Admin clicks onboarding link → sets password → goes through onboarding wizard
5. Trial expires in 30 days:
   - If converted to paid: subscription_tier updated, billing starts
   - If not: tenant status → 'suspended' (90-day recovery window)
```

### 7.3 Tenant Provisioning UI (Phase 3 MCG)

For Multi-Centre Groups, the Group Owner can provision new centres:

```
1. Group Owner logs in to MCG admin portal
2. Navigates to "Centres" → "Add Centre"
3. Fills form: centre name, address, branding, default programs, fee plans
4. Backend:
   a. Creates new tenant with parent_tenant_id = MCG's tenant_id
   b. Creates Keycloak realm (or sub-realm)
   c. Assigns Group Owner as Super Admin of new centre
   d. Provisions default settings (inherited from MCG where configured)
5. New centre appears in MCG dashboard
6. Group Owner can assign staff to new centre
```

### 7.4 Tenant Isolation Verification

Automated tests verify isolation:

```csharp
[Test]
public async Task ParentFromTenantA_CannotAccessStudentFromTenantB()
{
    // Arrange
    var tenantA = await CreateTestTenantAsync();
    var tenantB = await CreateTestTenantAsync();
    var studentInB = await CreateTestStudentAsync(tenantB.Id);
    var parentInA = await CreateTestParentAsync(tenantA.Id);
    
    // Set tenant context to A
    _tenantContext.TenantId = tenantA.Id;
    await SetRlsContextAsync(tenantA.Id);
    
    // Act
    var result = await _getStudentHandler.Handle(new GetStudentByIdQuery(studentInB.Id), default);
    
    // Assert
    Assert.IsFalse(result.IsSuccess);
    Assert.AreEqual(404, result.Error.StatusCode);  // 404, not 403
}
```

These tests run in CI on every PR.

### 7.5 Cross-Tenant Operations (MCG Only)

Phase 3 MCG features require cross-tenant queries:
- Consolidated dashboard (sum across centres).
- Cross-centre reports.
- Group Owner viewing any centre's data (with explicit authorization).

**Implementation:**
- Group Owner JWT has `tenant_roles: [{centre_id, role: 'group_owner'}, ...]`.
- Backend endpoint `/api/v1/mcg/dashboard` uses `service` role (BYPASSRLS) to query across tenants.
- Authorization layer verifies user has `group_owner` role on each requested centre.
- All cross-tenant access audited at higher level.
- Never expose cross-tenant data via standard per-tenant endpoints.

---

## 8. Keycloak Realm Strategy

### 8.1 Phase 1: One Realm per Deployment

```
SaaS Deployment:
  Keycloak instance
    ├── Realm: tenant-a (Bright Futures Nursery)
    │   ├── Client: nms-web
    │   ├── Client: nms-mobile
    │   └── Users: staff + parents of Bright Futures
    ├── Realm: tenant-b (Happy Kids Nursery)
    │   └── ...
    └── Realm: tenant-c (...)

On-Prem Deployment:
  Keycloak instance (customer's infra)
    └── Realm: bright-futures (single tenant)
        └── ...
```

**Why one realm per tenant (not one shared realm):**
- Complete isolation: realm-level user lists, sessions, role definitions.
- Per-tenant Keycloak configuration (e.g., custom login theme, MFA policy).
- Per-tenant user federation (some tenants may have AD, others not).
- Easier to delete a tenant (delete the realm).

### 8.2 Phase 3: Multi-Realm for MCG

For Multi-Centre Groups (one MCG, multiple centres):

```
MCG: Bright Futures Group (5 centres)
  Keycloak realm: bright-futures-group
    ├── Users: group owner, group director, group IT
    ├── Group-level roles: group_owner, group_director
    └── Centre-level roles (per user, per centre):
        ├── Layla: centre_admin at Centre 1, parent at Centre 3
        ├── Ahmed: teacher at Centre 1 + Centre 2
        └── ...
```

Cross-centre SSO: User logs in once to realm, JWT contains `tenant_roles` array with roles per centre. User can switch between centres without re-auth.

### 8.3 Realm Configuration per Tenant

Each realm has:
- Login theme: NMS default or custom (white-label for enterprise).
- MFA policy: configurable per tenant (mandatory for Super Admin + IT, optional for others).
- Password policy: min 12 chars, complexity, history (5), expiry (90 days optional).
- Session settings: access token 15 min, refresh token 7 days, max 5 sessions.
- Brute force protection: 5 fails → 15-min lockout.
- Social login: optional (Google, Microsoft) — disabled by default, configurable per tenant.

---

## 9. Per-Tenant Configuration

### 9.1 Configuration Storage

Per-tenant configuration stored in `tenant` table + `setting` table:

```sql
CREATE TABLE setting (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  key TEXT NOT NULL,  -- e.g., 'attendance.barcode_enabled', 'billing.default_due_days'
  value JSONB NOT NULL,
  -- Universal columns...
  UNIQUE (tenant_id, key)
);
```

### 9.2 Configuration Hierarchy

Settings resolved in order (later wins):
1. **Default** (in code, `SettingDefaults.cs`)
2. **Tenant-level** (in `setting` table)
3. **User-level** (in `user_setting` table, for personal preferences like language, notification prefs)

### 9.3 Configuration Categories

| Category | Example keys | Phase |
|---|---|---|
| Centre Profile | `centre.name`, `centre.logo_media_id`, `centre.branding_primary_color` | MVP |
| Attendance | `attendance.barcode_enabled`, `attendance.grace_period_minutes`, `attendance.auto_absent_time`, `attendance.supervisor_override_threshold_hours` | MVP |
| Ratio Rules | `ratio.rules` (JSONB: per age band per room) | MVP |
| Notifications | `notifications.critical.sms_enabled`, `notifications.operational.push_default` | MVP |
| Billing | `billing.default_due_days`, `billing.reminder_schedule`, `billing.currency`, `billing.tax_rate_percent` | Phase 1 Sprint 7 |
| Calendar | `calendar.weekend_days`, `calendar.working_hours_start`, `calendar.working_hours_end`, `calendar.holidays_ics_url` | Phase 1 Sprint 11 |
| Parent Portal | `parent_portal.daily_report_time`, `parent_portal.allowed_activity_types`, `parent_portal.photo_watermark_enabled` | MVP |
| Data Retention | `retention.attendance_years`, `retention.medical_years`, `retention.assessment_years` | MVP (configurable within legal min) |
| Branding | `branding.login_screen_image`, `branding.email_template_header_image` | MVP |
| Integrations | `integrations.smtp.*`, `integrations.fcm.*`, `integrations.payment_gateway.*` (Phase 3) | Phase 1 + Phase 3 |

### 9.4 Configuration UI

Centre Admin → Settings → per-category pages. Changes audited.

Super Admin → Settings → can edit all categories.
Other roles → limited settings access (e.g., parent can only edit own notification preferences).

---

## 10. Data Migration Between Modes

### 10.1 On-Prem → SaaS Migration

Customer starts on-prem, later wants to move to SaaS:
1. Provision new SaaS tenant.
2. Export on-prem database (per-tenant dump).
3. Transform dump (UUID remapping if needed, tenant_id update to new SaaS tenant_id).
4. Import into SaaS database.
5. Migrate Keycloak users (export from on-prem realm, import to SaaS realm).
6. Re-issue parent invitations (new SaaS realm = new credentials).
7. Cutover: DNS change, customer starts using SaaS URL.

Tooling: `infra/scripts/migrate-onprem-to-saas.sh` (built in Phase 2).

### 10.2 SaaS → On-Prem Migration

Customer moves from SaaS to on-prem (rare, but possible):
1. Provision on-prem deployment at customer's infra.
2. Export SaaS tenant data (per-tenant dump).
3. Import into on-prem database.
4. Migrate Keycloak users.
5. Cutover.

### 10.3 Cross-SaaS-Tenant Migration

Customer merges two SaaS tenants (e.g., two nurseries merge into one):
1. Choose surviving tenant.
2. Export other tenant's data.
3. Transform (re-assign tenant_id to surviving tenant, handle ID conflicts).
4. Import.
5. Archive non-surviving tenant.

This is a professional services engagement, not self-service.

---

## 11. Tenant Monitoring & Operations

### 11.1 Per-Tenant Metrics

Grafana dashboard per tenant:
- Active users (staff + parents) per day.
- API call count per day.
- Storage usage.
- Billing status.
- Error rate (4xx + 5xx).
- Notification delivery rate.

### 11.2 Platform-Level Monitoring

Grafana dashboard for platform ops:
- Total tenants, active/suspended/archived.
- Total users across all tenants.
- Database connection pool usage.
- K8s pod count + resource usage.
- API latency p50/p95/p99 across all tenants.
- Cross-tenant error rate.
- Backup status.

### 11.3 Noisy Neighbor Prevention

In SaaS, one tenant's heavy usage shouldn't impact others:
- Per-tenant rate limiting (100 req/min/user, configurable per-tier).
- Per-tenant API quota (Tier 1: 10k/day, Tier 4: 100k/day).
- Database connection pool: shared, but PgBouncer transaction mode prevents one tenant from exhausting.
- Background jobs: per-tenant queue priority. Large tenants don't block small tenants' jobs.
- Storage quota per tenant (50GB default, configurable per tier).

---

## 12. Backup & Restore Per Tenant

### 12.1 SaaS Backups

- **Database**: Daily snapshot of full database + WAL archiving (5-min RPO).
- **Tenant-specific restore**: For accidental data deletion by one tenant, use PITR (Point-In-Time Recovery) to a temporary restored DB, extract tenant data, import back. **Complex operation, professional services only.**
- **Object storage**: S3 versioning + cross-region replication. Per-object restore.

### 12.2 On-Prem Backups

- **Database**: Customer's responsibility (we provide pgBackRest config + runbook).
- **Object storage**: Customer's responsibility (MinIO replication or backup tool).
- **Recovery**: Customer's responsibility (we provide runbook).

### 12.3 Backup Encryption

All backups encrypted at rest (AWS KMS for SaaS, customer-managed keys for on-prem). Backup decryption keys stored separately from backups (secrets manager).

---

## 13. Tenant Delete / Right to Erasure

### 13.1 Soft Delete (Default)

Tenant cancellation → `tenant.status = 'archived'`, all data soft-deleted (`is_deleted = true`). Data retained per compliance (7+ years for attendance, 10+ for medical).

### 13.2 Hard Delete (After Retention)

After retention period + legal sign-off:
- Hard delete all tenant's rows from all tables (cascading).
- Delete Keycloak realm.
- Delete S3 objects with tenant's prefix.
- Delete from backups (next backup cycle).

This is irreversible. Logged at infrastructure level (CloudTrail) for audit.

### 13.3 Right to Erasure (PDPL/GDPR)

Parent requests erasure of their child's data:
- Soft-delete student record (already supported).
- After retention period (or with legal sign-off), hard-delete.
- Some records exempt: safeguarding records (legal hold), financial records (tax law).
- Erasure request logged in audit, processed within 30 days.
- Parent notified when complete.

---

## 14. Multi-Tenancy Testing Strategy

### 14.1 Automated Tests

- **Unit tests**: Tenant context mocked, verify business logic.
- **Integration tests**: Real PostgreSQL with RLS enabled, verify isolation.
- **E2E tests**: Two browser sessions (different tenants) in parallel, verify no cross-contamination.
- **Pen tests**: Attempt cross-tenant access with crafted JWTs, ID enumeration.

### 14.2 Test Matrix

| Test | What it verifies |
|---|---|
| ParentTenantA_access_StudentTenantB → 404 | Cross-tenant isolation |
| ParentA_access_StudentA_NotOwned → 404 | Parent-child ownership |
| StaffTenantA_access_StaffTenantB → 404 | Cross-tenant staff isolation |
| BackgroundJob_with_TenantId → only processes that tenant | Tenant context in jobs |
| RabbitMQConsumer_with_TenantId → only processes that tenant | Tenant context in consumers |
| RLSpolicy_blocks_raw_SQL | RLS at DB layer works |
| ServiceRole_bypasses_RLS | Service role works for migrations |

### 14.3 Load Testing Per-Tenant

k6 load tests simulate 500 concurrent users across 20 tenants (Phase 3 target). Verify:
- Per-tenant latency stays under 2s.
- No tenant's performance degrades due to another tenant's load.
- Database connection pool doesn't exhaust.

---

**Next file to read**: `11-DEVOPS-INFRASTRUCTURE.md` for K8s, Terraform, CI/CD, and observability.
