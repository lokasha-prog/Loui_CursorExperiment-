# 20 — Architecture Decision Records (ADRs)

> Nygard-format ADRs for the 10 most significant technical decisions in NMS. Future ADRs go in `/docs/adr/` in the repo.

---

## ADR Template

```markdown
# ADR-XXX: [Decision Title]

## Status
[Proposed | Accepted | Deprecated | Superseded by ADR-YYY]

## Context
[Why is this decision needed? What problem are we solving? What constraints?]

## Decision
[What did we decide? Be specific.]

## Alternatives Considered
[What else did we consider? Why didn't we choose them?]

## Consequences
[Positive: ...]
[Negative: ...]
[Neutral: ...]

## Compliance
[How do we verify the decision is followed?]
```

---

## ADR-001: Use .NET 8 (ASP.NET Core) for Backend

### Status
Accepted

### Context
The source NMS Pre-Development Planning v1.1 left the backend technology open. We need to select a concrete stack before Sprint 0 begins. The backend must support:
- Real-time WebSocket communication (SignalR).
- Background jobs (Hangfire).
- Message queue integration (RabbitMQ via MassTransit).
- Multi-tenancy with PostgreSQL RLS.
- OAuth 2.0 / OIDC via Keycloak.
- PDF generation, Excel export, email sending.
- High performance (p99 < 500ms).
- Long-term support (3+ years).
- Strong AI-coding tool support (Cursor, Copilot).

### Decision
Use **.NET 8 (ASP.NET Core)** with C# 12 as the backend framework.

### Alternatives Considered

1. **Spring Boot (Java)** — Source planning doc's first choice.
   - Pros: Mature, huge ecosystem, source planning doc aligns.
   - Cons: Verbose, slower dev loop, JVM startup time, less AI-coding-friendly, more boilerplate.
   - Rejected: Productivity loss outweighs ecosystem maturity for our team size.

2. **Node.js + NestJS** — JavaScript everywhere.
   - Pros: Same language as frontend, fast dev loop.
   - Cons: Type system weaker than C#, ORM (Prisma/TypeORM) less mature than EF Core, less enterprise-ready.
   - Rejected: EF Core + C# type system + LINQ is more productive for transactional CRUD app.

3. **Python + FastAPI** — Fast dev, great for AI features.
   - Pros: Fast development, excellent for AI (we have AI features in Phase 2).
   - Cons: Slower runtime, weaker typing, not ideal for transactional systems with complex business logic.
   - Rejected: AI features are isolated (we'll call OpenAI via HTTP from any backend). Don't pick backend based on AI features.

4. **Go + standard library** — Maximum performance.
   - Pros: Fastest runtime, smallest footprint, simplest deployment.
   - Cons: ORM ecosystem weak, less productive for CRUD-heavy app, less AI-coding-friendly.
   - Rejected: We don't have extreme performance needs. Productivity > raw performance for our use case.

5. **Laravel (PHP)** — Fast dev, cheap hosting.
   - Pros: Rapid development, huge community.
   - Cons: PHP language limitations, weaker type system, less enterprise adoption in UAE market.
   - Rejected: UAE enterprise customers expect .NET or Java.

### Consequences

**Positive**:
- Top-tier performance (consistently top of TechEmpower benchmarks).
- Excellent tooling (Visual Studio, Rider, VS Code with C# Dev Kit).
- 3-year LTS support (until November 2026 — covers Phase 1 + Phase 2).
- AI tools (Cursor, Copilot) very familiar with .NET.
- EF Core is the best mainstream ORM.
- Native SignalR support for real-time.
- Strong type system catches bugs at compile time.

**Negative**:
- Historically Microsoft-ecosystem reputation (though truly cross-platform now).
- Smaller community than Node.js for some niche libraries.
- Some UAE enterprises have more Java expertise than .NET (rare).

**Neutral**:
- Need to train team on .NET 8 specific features (minimal APIs, primary constructors, etc.).

### Compliance
- Verified: all backend code in `backend/` uses .NET 8 features.
- `backend/Directory.Packages.props` pins .NET 8 versions.
- CI checks target framework is `net8.0`.

---

## ADR-002: Use PostgreSQL 16 with RLS for Multi-Tenancy

### Status
Accepted

### Context
NMS must support multi-tenancy (one DB, multiple tenants) for SaaS mode, AND single-tenancy for on-prem. The same codebase must serve both. Database choice affects:
- Multi-tenancy isolation strategy.
- JSON support for i18n fields.
- Partitioning for high-volume tables (attendance, audit log).
- Full-text search (for messaging, resource centre).
- Connection pooling.
- Backup + restore.
- Cost (SaaS) + on-prem deployability.

### Decision
Use **PostgreSQL 16** with shared schema + `tenant_id` column + Row Level Security (RLS) policies for multi-tenancy.

### Alternatives Considered

1. **MySQL 8** — Familiar, cheaper.
   - Pros: Familiar, broad hosting support.
   - Cons: No RLS, weaker JSON support, Oracle ownership concerns.
   - Rejected: No RLS is a dealbreaker for our multi-tenancy model.

2. **SQL Server** — Excellent .NET integration.
   - Pros: First-class EF Core support, RLS support.
   - Cons: Expensive licensing (even on Linux), lock-in to Microsoft ecosystem.
   - Rejected: Cost + lock-in. PostgreSQL is free + open-source + equally capable.

3. **MongoDB** — Schema flexibility.
   - Pros: Schema flexibility great for evolving entities.
   - Cons: No transactions across collections (kills billing accuracy), weak relational queries.
   - Rejected: Billing + attendance need ACID transactions. MongoDB is wrong fit.

4. **CockroachDB** — Multi-region by default.
   - Pros: Multi-region native, PostgreSQL-compatible.
   - Cons: Newer, smaller ecosystem, overkill for Phase 1-2 (we're single-region UAE).
   - Rejected: Revisit in Phase 4+ when multi-region is needed.

5. **Schema-per-tenant in PostgreSQL** — Stronger isolation.
   - Pros: Stronger isolation per tenant.
   - Cons: N× migrations, complex operations, hard cross-tenant reporting, expensive (N× connections).
   - Rejected: Operational complexity too high. RLS provides adequate isolation with simpler ops.

6. **Database-per-tenant** — Strongest isolation.
   - Pros: Strongest possible isolation.
   - Cons: Most expensive, hardest to operate, doesn't fit on-prem single-tenant model.
   - Rejected: Overkill. RLS + app-layer filtering is sufficient.

### Consequences

**Positive**:
- RLS provides defense-in-depth at DB layer (app bug can't leak data).
- JSONB enables clean i18n field storage (`{"en": "...", "ar": "..."}`).
- Declarative partitioning for `attendance_record` + `audit_log`.
- Mature ecosystem (EF Core provider, PgBouncer, pg_stat_statements).
- Free + open-source.
- Same DB works for on-prem (single tenant) + SaaS (multi-tenant).

**Negative**:
- RLS policies add complexity to schema management.
- Need to remember `set_config('app.tenant_id', ...)` per request (mitigated by middleware).
- Some PostgreSQL features (e.g., certain index types) less mature than Oracle/SQL Server.

**Neutral**:
- Team needs PostgreSQL expertise (most common in any case).

### Compliance
- All tenant tables have RLS enabled.
- Migration runner verifies RLS policies after each migration.
- Integration tests verify cross-tenant isolation.
- Documented in `10-MULTI-TENANCY.md`.

---

## ADR-003: Shared Schema + tenant_id + RLS (Multi-Tenancy Model)

### Status
Accepted

### Context
The source SRS Open Question #1 asks: single-tenancy or multi-tenancy? The source planning doc §3.4 recommends "shared schema with tenant_id + RLS" but notes it should be validated via PoC. Loui Okasha's comment on the SRS explicitly states: "We would [want] to ensure that application is designed from the beginning with Multi-tenancy in mind."

The decision must:
- Support SaaS (many tenants per deployment).
- Support on-prem (one tenant per deployment).
- Use the same codebase for both.
- Provide strong isolation guarantees.
- Allow cross-tenant reporting (for Phase 3 MCG).

### Decision
Use **shared schema with `tenant_id` column + PostgreSQL Row Level Security (RLS)**, with EF Core global query filter as defense-in-depth.

Every entity has `tenant_id` (UUID, foreign key to `tenant` table). Every query is scoped by `tenant_id` via:
1. EF Core global query filter: `HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`.
2. PostgreSQL RLS policy: `USING (tenant_id = current_setting('app.tenant_id')::uuid)`.

The application sets `app.tenant_id` per request via middleware. Background jobs set it explicitly before DB queries.

### Alternatives Considered

1. **Schema-per-tenant** — Separate schema per tenant in same DB.
   - Pros: Stronger isolation, easier to reason about.
   - Cons: N× migrations (one per tenant per migration), complex cross-tenant reporting, more connection pool pressure.
   - Rejected: Operational complexity.

2. **Database-per-tenant** — Separate DB per tenant.
   - Pros: Strongest isolation.
   - Cons: Most expensive, doesn't fit on-prem model, hardest to operate.
   - Rejected: Overkill.

3. **Application-layer filtering only** (no RLS).
   - Pros: Simplest.
   - Cons: A bug in EF Core filter (e.g., `AsNoTracking()` edge case) leaks data. No safety net.
   - Rejected: Insufficient security for child data.

4. **Multi-tenant by separate deployment** (one deployment per tenant).
   - Pros: Strong isolation, simple model.
   - Cons: Expensive (N deployments), defeats SaaS economics, doesn't scale to 20+ centres.
   - Rejected: Doesn't fit SaaS commercial model.

### Consequences

**Positive**:
- One codebase, both deployment modes.
- Defense-in-depth: app filter + DB RLS = bugs don't leak data.
- Cross-tenant reporting possible (using `service` role for MCG consolidated views in Phase 3).
- Simple operations: one DB, one migration, one backup.
- Scales to many tenants without schema overhead.

**Negative**:
- `tenant_id` column on every table (storage overhead — minimal, ~16 bytes/row).
- RLS policies add DB query overhead (~5-10% per query, acceptable).
- Need to remember tenant context in background jobs (mitigated by Hangfire filter).
- Need `service` role for cross-tenant ops (careful access control).

**Neutral**:
- All developers must understand the model (training in Sprint 0).

### Compliance
- All entities implement `ITenantEntity` interface.
- EF Core `OnModelCreating` enforces global query filter.
- Migration post-deploy script verifies RLS policies.
- Integration tests verify cross-tenant denial.
- Documented in `10-MULTI-TENANCY.md`.

---

## ADR-004: Use Keycloak as Identity Provider

### Status
Accepted

### Context
NMS needs an identity provider that supports:
- OAuth 2.0 / OIDC for web + mobile + Open API.
- Multi-realm support (one realm per tenant in Phase 1, multi-realm per MCG in Phase 3).
- Role-based access control.
- MFA (TOTP).
- User federation (LDAP/AD for on-prem customers with existing IdP).
- Social login (optional, configurable per tenant).
- Self-hostable (for on-prem).
- Audit logging.

### Decision
Use **Keycloak 24** as the identity provider.

### Alternatives Considered

1. **Auth0** — Best DX, beautiful UI.
   - Pros: Best developer experience, excellent docs.
   - Cons: Per-user pricing gets expensive at scale, SaaS-only (data residency concern for on-prem customers).
   - Rejected: Cost + on-prem data residency.

2. **AWS Cognito** — Cheap, integrates with AWS.
   - Pros: Cheap, AWS-native.
   - Cons: Poor DX, limited customization, dated UI, weak federation.
   - Rejected: DX is too poor; we'd spend weeks customizing.

3. **Azure AD B2C** — Cheap, integrates with Azure.
   - Pros: Cheap, Azure-native.
   - Cons: Complex config, poor DX, UI customization painful.
   - Rejected: DX issues similar to Cognito.

4. **IdentityServer (Duende)** — .NET-native.
   - Pros: .NET-native, OIDC standard.
   - Cons: Licensing cost for commercial use, smaller community than Keycloak.
   - Rejected: Cost + smaller ecosystem.

5. **Custom (rolling our own)** — Full control.
   - Pros: Full control.
   - Cons: Most expensive, security risk, don't roll your own auth.
   - Rejected: Standard advice — never roll your own auth.

### Consequences

**Positive**:
- Open-source, free, no per-user cost.
- Multi-realm support fits our tenant model.
- MFA built-in (TOTP).
- User federation (LDAP/AD) for on-prem customers.
- Self-hostable (runs in our K8s cluster).
- Industry-standard OIDC/OAuth 2.0.
- Mature (10+ years of development).

**Negative**:
- Java app, more resource-heavy than alternatives (~1 GB RAM per replica).
- UI is dated (we use our own login pages via JS adapter, so users don't see it).
- More ops overhead than managed services (Cognito, Auth0).

**Neutral**:
- Need to manage Keycloak upgrades (quarterly).

### Compliance
- Keycloak deployed via Helm chart in `keycloak` namespace.
- One realm per tenant (Phase 1), multi-realm per MCG (Phase 3).
- Custom login UI (we don't use Keycloak's built-in themes).
- JWT validated in .NET via `Keycloak.AuthServices.Authentication` package.
- Documented in `09-AUTH-SECURITY.md`.

---

## ADR-005: Modular Monolith for Phases 1–2, Extraction-Ready for Phase 3

### Status
Accepted

### Context
NMS has 14 functional modules with significant cross-module communication. Team size is 4–6 engineers in Phase 1, growing to 8–10 in Phase 2. We need to balance:
- Development speed (microservices add distributed systems complexity).
- Transactional integrity (billing + attendance need cross-module transactions).
- Future scalability (Notification + Media Processing may need independent scaling).
- Team size (microservices require larger teams for ops overhead).

### Decision
Build NMS as a **modular monolith** for Phases 1–2, with **Notification** and **Media Processing** modules designed for extraction to separate services in Phase 3 (decision gate at Sprint 25/30).

### Modular Monolith Principles

1. **Explicit module boundaries** — each module has a public interface (CQRS handlers + events).
2. **No direct cross-module DB access** — modules access only their own tables.
3. **Shared kernel** — minimal: `Result<T>`, `Pagination`, `TenantContext`, `ICurrentUser`, base entity.
4. **Module communication** — in-process MediatR for queries/commands; in-process event bus for events. Both abstractions swap to RabbitMQ when extracted.
5. **Module deployment** — single deployable for now. When extracted, the module's project becomes its own service.

### Extraction-Ready Modules

**Notifications**:
- All triggers via RabbitMQ events (not direct MediatR calls) — same event contracts work cross-service when extracted.
- Owns `notification` + `notification_delivery` tables.
- Has its own connection pool to external services (FCM, SES, Twilio).
- Stateless — easy to scale horizontally.

**Media Processing**:
- All triggers via RabbitMQ events.
- Owns scan_result data.
- Can be extracted without touching other modules.

### Alternatives Considered

1. **Microservices from day 1**.
   - Pros: Independent scaling, independent deployment, clear boundaries enforced.
   - Cons: Distributed transactions (sagas) for billing + attendance, distributed tracing overhead, service mesh complexity, network failure modes, larger team required.
   - Rejected: Team too small. Distributed systems overhead kills productivity.

2. **Single monolith (no modules)**.
   - Pros: Simplest, fastest dev.
   - Cons: No boundaries, hard to extract later, becomes unmaintainable.
   - Rejected: We know we'll need to extract Notifications + Media Processing. Start with modules.

3. **Microservices for Notifications + Media only, monolith for rest**.
   - Pros: Independent scaling for the two modules that need it most.
   - Cons: Still need distributed transactions for billing/attendance; premature distribution.
   - Rejected: Start with all-in-one, extract only when justified (Phase 3 decision gate).

### Consequences

**Positive**:
- Fast development (in-process calls, no network).
- ACID transactions across modules (billing + attendance).
- Single deployable (simple CI/CD).
- Clear module boundaries (tested via NetArchTest).
- Extraction path is clear (RabbitMQ + clean interfaces).

**Negative**:
- Single point of failure (mitigated by K8s HA + multi-AZ).
- All modules scale together (mitigated by HPA + extraction when needed).
- Need discipline to maintain module boundaries.

**Neutral**:
- NetArchTest enforces module boundaries in CI.

### Compliance
- Architecture tests (NetArchTest) in `NMS.ArchitectureTests` project.
- CI gate: PR fails if module boundary violated.
- Documented in `06-ARCHITECTURE.md` §1 + §4.

---

## ADR-006: Use SignalR with Redis Backplane for Real-Time

### Status
Accepted

### Context
NMS needs real-time updates for:
- Dashboard (live ratios, attendance summary, pending actions).
- Messaging (new message notifications).
- Activity feed (new activity posts visible to parents).
- Notifications (in-app banner for Critical tier).

The transport must:
- Work in .NET (native integration preferred).
- Auto-negotiate transport (WebSocket → SSE → long-polling fallback).
- Scale horizontally (multiple API pods).
- Work on mobile (React Native).

### Decision
Use **ASP.NET Core SignalR** with **Redis backplane** (StackExchange.Redis).

### Alternatives Considered

1. **Raw WebSockets**.
   - Pros: Maximum control, no abstraction overhead.
   - Cons: Re-implement everything SignalR gives us (reconnection, fallback, message types, auth).
   - Rejected: Too much custom code to maintain.

2. **Server-Sent Events (SSE) only**.
   - Pros: Simpler than WebSockets, works over HTTP.
   - Cons: One-way only (server → client), no native .NET support, mobile support patchy.
   - Rejected: We need bidirectional (client subscribes to specific rooms).

3. **GraphQL Subscriptions**.
   - Pros: Type-safe, integrates with GraphQL API.
   - Cons: We're not using GraphQL for the rest of the API (REST). Adds complexity.
   - Rejected: Inconsistent with REST API approach.

4. **Third-party real-time service (Pusher, Ably)**.
   - Pros: Managed, no infra to run.
   - Cons: Cost, data residency (some don't have UAE region), vendor lock-in.
   - Rejected: Cost + data residency.

### Consequences

**Positive**:
- Native .NET integration (no extra libraries).
- Auto-negotiate transport (WebSocket → SSE → long-polling).
- Redis backplane enables multi-pod scaling.
- `@microsoft/signalr` client works in both web (React) and mobile (React Native).
- Built-in auth (JWT in query string on connect).
- Built-in reconnection (exponential backoff).

**Negative**:
- Sticky sessions required for SSE/long-polling fallback (configured at load balancer).
- Redis adds dependency (mitigated by HA Redis).
- WebSocket connection limit per pod (~5000 concurrent, mitigated by HPA).

**Neutral**:
- Team needs to learn SignalR patterns (groups, methods, hub).

### Compliance
- Single hub: `/hubs/tenant`.
- Groups per tenant + role + user + room.
- Redis backplane configured in `Program.cs`.
- SignalR client shared between web + mobile (same npm package).
- Documented in `06-ARCHITECTURE.md` §5 + `08-API-SPEC.md` §13.

---

## ADR-007: REST + OpenAPI 3.0, Cursor Pagination

### Status
Accepted

### Context
NMS needs an API style that:
- Works for web, mobile, and third-party (Phase 3 Open API).
- Is AI-coding-friendly (Cursor can generate clients from spec).
- Scales to many entities + endpoints.
- Supports pagination efficiently (no OFFSET performance issues).
- Has good tooling (code generation, docs UI, testing).

### Decision
Use **REST + OpenAPI 3.0** with **cursor-based pagination**. GraphQL evaluated for Phase 2 mobile only if REST payload size becomes a problem on slow mobile networks.

### Alternatives Considered

1. **GraphQL**.
   - Pros: Client fetches exactly what it needs, single endpoint.
   - Cons: Complexity (schema design, N+1 queries, authz per field), different from REST conventions, AI tools less mature with GraphQL.
   - Rejected: Overkill for our use case. We have well-defined entities, not complex graph queries.

2. **gRPC**.
   - Pros: Best performance, type-safe.
   - Cons: Binary protocol (hard to debug), no browser support without gRPC-Web, less ecosystem.
   - Rejected: Overkill. We don't have extreme performance needs.

3. **OFFSET pagination**.
   - Pros: Simple.
   - Cons: Performance degrades on deep pages (`OFFSET 100000` scans 100000 rows).
   - Rejected: Doesn't scale.

### Consequences

**Positive**:
- OpenAPI 3.0 spec auto-generated from .NET annotations.
- Frontend + mobile clients auto-generated via `openapi-typescript-codegen`.
- Cursor pagination scales to millions of records.
- Standard REST conventions (resources, verbs, status codes).
- AI tools (Cursor) very familiar with REST + OpenAPI.

**Negative**:
- Multiple round-trips for related data (mitigated by `?include=` parameter for common cases).
- Over-fetching for mobile (mitigated by mobile-specific endpoints if needed; GraphQL evaluated Phase 2).

**Neutral**:
- Need to maintain OpenAPI spec (auto-generated, so low overhead).

### Compliance
- All endpoints under `/api/v1/`.
- OpenAPI spec at `/openapi.json` (dev/staging) — not exposed in production.
- Scalar API docs UI at `/api-docs` (dev/staging).
- Frontend + mobile clients regenerated on API change.
- Documented in `08-API-SPEC.md`.

---

## ADR-008: React Native + Expo Bare Workflow for Mobile

### Status
Accepted

### Context
NMS needs mobile apps for staff + parents. Requirements:
- iOS + Android from one codebase.
- Offline support (emergency report cache, attendance queue).
- Push notifications (FCM + APNs).
- Camera for barcode scanning.
- Secure storage for tokens.
- Code sharing with web (TypeScript, i18n, API client, types).
- AI-coding-friendly.
- OTA updates for fast iteration.

### Decision
Use **React Native 0.74 + Expo SDK 51 (Bare Workflow)**.

### Alternatives Considered

1. **React Native (Bare, no Expo)**.
   - Pros: Maximum native control.
   - Cons: Lose Expo tooling (EAS Build, OTA updates, config plugins).
   - Rejected: Expo's tooling is too valuable to give up.

2. **Flutter**.
   - Pros: Excellent performance, beautiful UIs, single codebase.
   - Cons: Dart is niche language, no code sharing with web (React), smaller ecosystem in UAE.
   - Rejected: No code sharing with web is a dealbreaker for our team size.

3. **Native (Swift + Kotlin)**.
   - Pros: Best performance + UX, full platform access.
   - Cons: Two codebases, double the dev cost, slower iteration.
   - Rejected: Cost. We can achieve 90% of native UX with React Native.

4. **PWA only**.
   - Pros: One codebase with web.
   - Cons: No offline cache (critical for emergency reports), no push on iOS (until recently, still patchy), can't access some native APIs.
   - Rejected: Emergency report offline cache is mandatory. PWA can't deliver.

5. **Expo Managed Workflow**.
   - Pros: Simplest, no native code.
   - Cons: Can't use custom native modules (we need vision-camera for barcode, WatermelonDB for offline, expo-secure-store for tokens).
   - Rejected: Need native modules. Bare workflow gives us Expo's productivity + native flexibility.

### Consequences

**Positive**:
- iOS + Android from one codebase.
- Code sharing with web (TypeScript types, i18n strings, Zod schemas, API client).
- Expo tooling: EAS Build (cloud builds), OTA updates (fast iteration), config plugins.
- React Native is well-documented in AI training data.
- NativeWind (Tailwind for RN) gives consistent styling with web.
- Large ecosystem (most RN libraries work in Expo Bare).

**Negative**:
- Some native modules require config plugins (managed by Expo).
- Performance slightly below native for complex animations (mitigated by Reanimated 3).
- iOS App Store review adds 1-3 days per native code update (mitigated by OTA for JS-only changes).

**Neutral**:
- Team needs React Native expertise.

### Compliance
- `mobile/` project uses Expo SDK 51 Bare Workflow.
- EAS Build for cloud builds.
- OTA updates for JS-only changes.
- Native modules: `react-native-vision-camera`, `watermelondb`, `expo-secure-store`, `expo-notifications`.
- Documented in `13-MOBILE-APP.md`.

---

## ADR-009: Store Money as Integer AED Fils (Never Floating-Point)

### Status
Accepted

### Context
NMS handles financial data (invoices, payments, expenses, fee plans). Floating-point arithmetic introduces precision errors:
- `0.1 + 0.2 = 0.30000000000000004` in floating-point.
- Over thousands of invoices, these errors accumulate.
- Accounting discrepancies are unacceptable.

### Decision
Store all monetary amounts as **integer AED fils** (1 AED = 100 fils) using `bigint` in PostgreSQL.

Examples:
- 250.00 AED → `25000` fils.
- 0.05 AED → `5` fils.
- 1,234.56 AED → `123456` fils.

Display layer formats to decimal: `25000 / 100 = 250.00 AED`.

### Alternatives Considered

1. **`numeric(10, 2)` (decimal)**.
   - Pros: Direct representation of decimal numbers.
   - Cons: Slower arithmetic than integer, still susceptible to rounding in division operations.
   - Rejected: Integer is simpler + faster.

2. **`float` / `double precision`**.
   - Pros: Native, fast.
   - Cons: Precision errors (the original problem).
   - Rejected: Unacceptable for financial data.

3. **`money` type** (PostgreSQL-specific).
   - Pros: Designed for money.
   - Cons: Limited operator support, locale-dependent output formatting.
   - Rejected: Limited ecosystem support.

### Consequences

**Positive**:
- No precision errors (integer arithmetic is exact).
- Fast arithmetic.
- Simple mental model.
- Easy to verify totals (sum of line items = invoice total).

**Negative**:
- Need to convert for display (`amount_fils / 100`).
- Need to convert for external APIs (payment gateways expect decimal).
- Currency conversion (multi-currency in Phase 4+) needs care.

**Neutral**:
- All developers must follow the convention.

### Compliance
- All monetary columns use `bigint` with `_fils` suffix (e.g., `amount_fils`, `total_fils`).
- Display layer formats via `Intl.NumberFormat('en-AE', { style: 'currency', currency: 'AED' })`.
- Unit tests verify arithmetic: `20000 + 5000 == 25000` (exact).
- Documented in `07-DATABASE-SCHEMA.md` §1.5.

---

## ADR-010: JSONB for i18n Fields + react-i18next for UI Strings

### Status
Accepted

### Context
NMS must support English + Arabic as first-class languages. The i18n strategy must handle:
- **DB-stored user content** (room names, program names, activity captions, newsletter content, email templates, notification titles/bodies).
- **UI strings** (button labels, form labels, validation messages, error messages).
- **RTL layout** (Arabic is right-to-left).
- **Per-user locale preference** (each user picks their language).
- **Fallback** (default to English if Arabic missing).

### Decision
Use **two complementary approaches**:

1. **DB-stored user content**: JSONB columns with `{ "en": "...", "ar": "..." }` structure.
2. **UI strings**: `react-i18next` + `i18next` with JSON translation files per locale.

### DB i18n Implementation

```sql
-- Table column
display_name JSONB NOT NULL DEFAULT '{"en": "", "ar": ""}'

-- Read in user's locale
SELECT display_name->>'ar' as display_name FROM room WHERE id = $1;

-- Read both
SELECT display_name->>'en' as display_name_en,
       display_name->>'ar' as display_name_ar
FROM room WHERE id = $1;
```

### UI String Implementation

```tsx
// public/locales/en/translation.json
{ "students": { "title": "Students" } }

// public/locales/ar/translation.json
{ "students": { "title": "الطلاب" } }

// Component
import { useTranslation } from 'react-i18next';
function StudentsPage() {
  const { t } = useTranslation();
  return <h1>{t('students.title')}</h1>;
}
```

### Alternatives Considered

1. **Separate translation tables** (e.g., `room_translation` with `room_id`, `locale`, `name`).
   - Pros: Normalized.
   - Cons: Extra JOINs, complex queries, harder to maintain.
   - Rejected: JSONB is simpler + faster.

2. **One column per locale** (`name_en`, `name_ar`).
   - Pros: Simple queries.
   - Cons: Schema changes when adding languages, inflexible.
   - Rejected: JSONB is more flexible.

3. **Auto-translation service** (like parent.app's 198 languages).
   - Pros: No manual translation needed.
   - Cons: Quality issues, doesn't handle RTL well, doesn't handle domain-specific terms, privacy concerns (sending content to translation API).
   - Rejected for Phase 1: EN + AR natively is sufficient for UAE. Evaluate in Phase 2 for non-Arabic, non-English parents.

4. **Single-language DB + UI translation only**.
   - Pros: Simplest DB.
   - Cons: Room names, program names need to be in user's language. Can't translate "Toddlers Room" to "غرفة الأطفال" via UI strings (it's user-entered content).
   - Rejected: Doesn't meet requirement.

### Consequences

**Positive**:
- DB-stored content is natively bilingual.
- UI strings are cleanly separated (no hardcoded English in components).
- Adding a third language (Phase 4+) is straightforward (add JSONB key, add translation file).
- Both EN + AR work natively, no auto-translation crutch.

**Negative**:
- JSONB columns slightly more complex queries (vs. flat columns).
- Need to handle missing translations (fallback to English).
- Slightly more storage (JSONB overhead, minimal).

**Neutral**:
- All developers must follow the convention.

### Compliance
- All translatable DB columns use JSONB with `en` + `ar` keys.
- All UI strings via `t('key')` in react-i18next.
- ESLint rule forbids hardcoded English in components.
- RTL is first-class (CSS logical properties only, see `14-UX-UI-DESIGN.md` §10).
- Visual regression tests in both LTR + RTL.
- Documented in `07-DATABASE-SCHEMA.md` §1.4 + `12-WEB-FRONTEND.md` §6.

---

## Future ADRs

As the project evolves, new ADRs will be added in `/docs/adr/` in the repo. Likely future ADRs:

- ADR-011: Payment gateway selection (Phase 3).
- ADR-012: ERP integration adapter pattern (Phase 3).
- ADR-013: Multi-region deployment strategy (Phase 4+).
- ADR-014: Elasticsearch adoption (if PostgreSQL FTS insufficient).
- ADR-015: Service extraction — Notifications (if Sprint 25 decision is "yes").
- ADR-016: Service extraction — Media Processing (if Sprint 25 decision is "yes").

Each new ADR follows the Nygard template at the top of this file.

---

**Next file to read**: `21-DELIVERY-CHECKLIST.md` — go-live readiness + handover checklist.
