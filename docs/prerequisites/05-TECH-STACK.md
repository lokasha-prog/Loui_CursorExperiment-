# 05 — Tech Stack

> Concrete technology selection for NMS. Replaces the "open" technology choices in the upstream planning doc with a specific, modern, AI-coding-friendly stack.

---

## 1. Stack Summary

| Layer | Technology | Version | Why |
|---|---|---|---|
| Backend | .NET (ASP.NET Core) | 8.0 LTS | High performance, strong typing, EF Core, native DI, excellent tooling, 3-year LTS support |
| Backend language | C# | 12 | Modern features (records, primary constructors, source generators) |
| ORM | EF Core | 8.0 | Code-first migrations, Postgres-specific features (RLS, JSONB), LINQ |
| Database | PostgreSQL | 16 | RLS for multi-tenancy, JSONB for i18n, declarative partitioning, mature |
| Cache + Real-time backplane | Redis | 7.2 | Caching, SignalR backplane, rate limiting counters, pub/sub |
| Real-time transport | ASP.NET Core SignalR | 8.0 | Native .NET, auto-negotiate, fallback to long-polling, Redis backplane for scale |
| Message queue | RabbitMQ | 3.13 | Background jobs, retries, DLQs, sagas for billing |
| Message bus abstraction | MassTransit | 8.2 | Saga support, sagas for billing workflow, clean abstraction over RabbitMQ |
| Object storage | S3-compatible (AWS S3 for SaaS, MinIO for on-prem) | latest | Media, documents, signed URLs, versioning |
| Identity | Keycloak | 24.0 | Multi-realm, RBAC, MFA, social login, OIDC/OAuth 2.0 |
| Web frontend | React | 18.3 | Mature, huge ecosystem, AI-coding-friendly |
| Web framework | Vite | 5.4 | Fast dev server, modern build, no Create React App cruft |
| Web language | TypeScript | 5.5 | Type safety, refactor confidence, AI-friendly |
| Web UI library | shadcn/ui + Radix UI + Tailwind CSS | latest | Copy-paste components, fully accessible, RTL-ready |
| Web state | TanStack Query v5 + Zustand | latest | Server state (TanStack) + client state (Zustand) — pragmatic, no Redux |
| Web forms | React Hook Form + Zod | latest | Type-safe, performant, schema-first |
| Web i18n | react-i18next + i18next | latest | Mature, RTL-ready, lazy loading |
| Web routing | React Router v6.4+ (or TanStack Router) | latest | Mature, type-safe with data loaders |
| Web charts | Recharts + Visx | latest | Recharts for standard, Visx for custom |
| Web PDF | @react-pdf/renderer | latest | Client-side PDF generation for parent-facing reports |
| Mobile framework | React Native | 0.74 | Cross-platform iOS + Android, mature |
| Mobile toolchain | Expo SDK 51 (Bare Workflow) | 51 | EAS Build, OTA updates, native modules when needed |
| Mobile language | TypeScript | 5.5 | Same as web |
| Mobile UI | NativeWind (Tailwind for RN) + React Native Paper | latest | Consistent styling with web, accessible primitives |
| Mobile state | TanStack Query v5 + Zustand | latest | Same as web |
| Mobile forms | react-hook-form + zod | latest | Same as web |
| Mobile i18n | i18next + react-i18next | latest | Same as web |
| Mobile navigation | Expo Router v3 | latest | File-based routing, deep linking |
| Mobile offline | WatermelonDB / SQLite | latest | Offline-first local DB for emergency cache + offline attendance |
| Mobile push | Expo Notifications + FCM + APNs | latest | Unified API for iOS + Android |
| Mobile barcode | react-native-vision-camera + MLKit | latest | Camera scanning with MLKit barcode detection |
| Backend PDF | QuestPDF | 2024.3.0 | Fluent C# DSL, no iText licensing, MIT-licensed |
| Backend Excel | ClosedXML | 0.104.2 | XLSX generation, MIT-licensed, no Excel dependency |
| Backend email | FluentEmail + MailKit | latest | Templated emails, multi-provider (SES / SMTP / Azure Communication) |
| Backend scheduling | Hangfire | 1.8.x | Recurring jobs (daily report, invoice generation), dashboard UI |
| Backend validation | FluentValidation | 11.x | Complex validation rules, async, i18n error messages |
| Backend mapping | Mapster | 7.4 | Faster than AutoMapper, simpler API |
| Backend logging | Serilog | 4.0 | Structured JSON logging, sinks for Loki + Console + File |
| Backend OpenTelemetry | OpenTelemetry.Instrumentation.* | 1.10.x | Vendor-neutral observability |
| Backend auth | Keycloak.AuthServices.Authentication | latest | Keycloak JWT validation, role-based authorization |
| Backend testing | xUnit + FluentAssertions + Moq + Testcontainers | latest | Standard .NET testing stack |
| Backend API doc | Scalar / Swashbuckle (OpenAPI 3.0) | latest | OpenAPI generation + beautiful docs UI |
| Containerization | Docker | 24+ | Standardized packaging |
| Orchestration | Kubernetes | 1.30+ | Auto-scaling, rolling deploys, namespace isolation |
| IaC | Terraform | 1.9+ | Multi-cloud (AWS for SaaS, customer infra for on-prem) |
| CI/CD | GitHub Actions + Helm | latest | Familiar, ecosystem-rich |
| Monitoring | Prometheus + Grafana + Loki | latest | Vendor-neutral observability stack |
| Tracing | OpenTelemetry Collector + Tempo | latest | Distributed tracing (critical when services extract in Phase 3) |
| Secrets | AWS Secrets Manager (SaaS) / HashiCorp Vault (on-prem) | latest | Never in env vars or code |
| Container registry | GitHub Container Registry (ghcr.io) / customer registry | latest | Integrated with GitHub Actions |
| DNS | AWS Route 53 (SaaS) / customer DNS | latest | Latency routing, health-check failover |
| CDN | AWS CloudFront (SaaS) / Cloudflare (on-prem option) | latest | UAE edge PoPs, signed URLs, WAF |
| Load testing | k6 | latest | Modern, scriptable, CI-integratable |
| E2E testing | Playwright | latest | Multi-browser, fast, modern API |
| Static analysis | SonarCloud / SonarQube | latest | Quality gate, coverage gate |
| Dependency scanning | OWASP Dependency Check + GitHub Dependabot | latest | CVE monitoring, PR-level alerts |

---

## 2. Stack Alternatives Considered

For each layer, here's what we considered and why we chose what we chose. Full ADRs in `20-ADRs.md`.

### 2.1 Backend Framework

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **.NET 8 (ASP.NET Core)** ✅ | Best performance of any web framework; C# 12 is modern; EF Core is excellent; LTS 3 years; AI tools very familiar with it | Historically Microsoft-ecosystem, but now truly cross-platform | **Selected** |
| Spring Boot (Java) | Source planning doc's first choice; mature; huge ecosystem | Verbose; JVM startup time; slower dev loop; less AI-coding-friendly | Rejected |
| Node.js + NestJS | JS everywhere; fast dev loop; great for small services | Type safety weaker than C#; ORM (Prisma/TypeORM) less mature than EF Core | Rejected |
| Python + Django/FastAPI | Fast dev; great for AI features | Slower runtime; weaker typing; not ideal for transactional systems | Rejected |
| Go + standard library | Fastest; simplest; smallest footprint | ORM ecosystem weak; less productive for CRUD-heavy app; less AI-coding-friendly | Rejected |
| Laravel (PHP) | Fast dev; huge community; cheap hosting | PHP language limitations; weaker type system; less enterprise adoption in UAE | Rejected |

**Rationale**: .NET 8 wins on performance (consistently top of TechEmpower benchmarks), developer productivity (C# 12 is concise), type safety, EF Core is the best mainstream ORM, and Cursor/Copilot work exceptionally well with it. LTS until November 2026 gives us Phase 1 + Phase 2 coverage.

### 2.2 Database

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **PostgreSQL 16** ✅ | RLS for multi-tenancy (defense-in-depth); JSONB for i18n fields; declarative partitioning; mature; great .NET support | Slightly more setup than MySQL | **Selected** |
| MySQL 8 | Familiar; cheaper managed options | No RLS; weaker JSON support; Oracle ownership concerns | Rejected |
| SQL Server | Excellent .NET integration; RLS support | Expensive licensing; lock-in | Rejected |
| MongoDB | Schema flexibility; great for content | No transactions across collections; weak relational queries; not suitable for billing | Rejected |
| CockroachDB | Multi-region by default | Newer; smaller ecosystem; overkill for Phase 1–2 | Rejected (revisit Phase 3+) |

**Rationale**: PostgreSQL's RLS is the killer feature for multi-tenant SaaS — defense-in-depth at the database layer means even a bug in EF Core's tenant filter can't leak data across tenants. JSONB enables clean i18n field storage without separate translation tables. Partitioning scales audit_log and attendance tables.

### 2.3 Web Frontend Framework

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **React 18 + Vite** ✅ | Huge ecosystem; AI tools excellent; shadcn/ui is React-only; TanStack Query is best-in-class | More boilerplate than Svelte | **Selected** |
| Next.js 14 | SSR/SSG; great for marketing pages; React Server Components | Overkill for an authenticated dashboard app; RSC adds complexity; not needed for SPA-style admin | Rejected (could be used for marketing site, separate from app) |
| Vue 3 + Nuxt | Approachable; great DX | Smaller ecosystem; fewer component libraries; less AI-coding-friendly | Rejected |
| SvelteKit | Smallest bundle; great DX | Smallest ecosystem; shadcn/ui not available; fewer AI training examples | Rejected |
| Angular | Full framework; opinionated | Heaviest; steepest learning curve; less AI-coding-friendly | Rejected |

**Rationale**: For an authenticated dashboard SPA (which NMS is), Vite + React is the most productive, AI-friendly choice. shadcn/ui gives us accessible, beautiful, copy-paste components without library lock-in. We don't need SSR for an admin app.

### 2.4 Mobile Framework

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **React Native 0.74 + Expo SDK 51 (Bare)** ✅ | Cross-platform; huge ecosystem; code sharing with web (TypeScript, i18n, state); Expo tools (EAS Build, OTA updates) | Some native modules still required (barcode, SQLite) — manageable with Expo Bare | **Selected** |
| React Native (Bare, no Expo) | Maximum native control | More boilerplate; lose Expo tooling | Rejected |
| Flutter | Excellent performance; beautiful UIs; single codebase | Dart is a niche language; no code sharing with web; smaller ecosystem in UAE | Rejected |
| Native (Swift + Kotlin) | Best performance + UX; full platform access | Two codebases; double the dev cost; slower iteration | Rejected |
| PWA only | One codebase with web | No offline cache; no push on iOS; can't access some native APIs | Rejected (we need offline emergency cache) |

**Rationale**: Expo Bare Workflow gives us 90% of Expo's productivity (EAS Build, OTA updates, config plugins) while allowing native modules when needed (react-native-vision-camera for barcode, WatermelonDB for offline). Code sharing with web (TypeScript types, i18n strings, Zod schemas, some hooks) is a massive productivity win.

### 2.5 Identity Provider

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **Keycloak 24** ✅ | Open source; multi-realm; MFA built-in; OIDC/OAuth 2.0 standard; SSO; user federation (LDAP/AD) | Java app, more resource-heavy than alternatives; UI dated | **Selected** |
| Auth0 | Best DX; beautiful UI; excellent docs | Per-user pricing gets expensive at scale; SaaS-only (data residency concern) | Rejected |
| AWS Cognito | Cheap; integrates with AWS | Poor DX; limited customization; dated UI; weak federation | Rejected |
| Azure AD B2C | Cheap; integrates with Azure | Complex config; poor DX; UI customization painful | Rejected |
| IdentityServer (Duende) | .NET native; OIDC standard | Licensing cost for commercial use; smaller community than Keycloak | Rejected |
| Custom (rolling our own) | Full control | Most expensive; security risk; don't roll your own auth | Rejected |

**Rationale**: Keycloak is the best open-source identity provider. Multi-realm support is essential for our SaaS model (one realm per centre in Phase 1, multi-realm for MCG in Phase 3). On-prem customers can federate with their existing AD via LDAP. The UI is dated but we use our own login pages (Keycloak JS adapter) so users never see Keycloak's UI.

---

## 3. Version Pinning Strategy

All versions pinned in:
- `backend/Directory.Packages.props` (central package management)
- `web/package.json` (exact versions, no `^`)
- `mobile/package.json` (exact versions)
- `infra/terraform/versions.tf` (provider versions)
- `infra/docker-compose.yml` (image tags)

**Update policy**:
- Patch versions: auto-update via Dependabot PRs (auto-merge if tests pass).
- Minor versions: manual review monthly, update in batch.
- Major versions: spike + ADR before upgrade. Never upgrade a major version mid-sprint.

---

## 4. Backend Project Structure

```
backend/
├── src/
│   ├── NMS.Api/                  # ASP.NET Core API entry point
│   │   ├── Program.cs            # App bootstrap, DI, middleware
│   │   ├── Controllers/          # Thin controllers (or use minimal APIs)
│   │   └── appsettings.json
│   ├── NMS.Domain/               # Domain entities, no dependencies
│   │   ├── Students/
│   │   ├── Attendance/
│   │   ├── Billing/
│   │   └── ...                   # One folder per module
│   ├── NMS.Application/          # Use cases, CQRS handlers, DTOs
│   │   ├── Students/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   └── DTOs/
│   │   └── ...
│   ├── NMS.Infrastructure/       # EF Core, RabbitMQ, S3, external services
│   │   ├── Persistence/
│   │   │   ├── NmsDbContext.cs
│   │   │   ├── Configurations/   # EF Core IEntityTypeConfiguration
│   │   │   └── Migrations/
│   │   ├── Messaging/
│   │   ├── Storage/
│   │   ├── Identity/
│   │   └── External/
│   ├── NMS.Modules/              # Module-specific code (modular monolith)
│   │   ├── Dashboard/
│   │   ├── CentreManagement/
│   │   ├── Attendance/
│   │   ├── ParentCommunication/
│   │   ├── Messaging/
│   │   ├── Billing/
│   │   ├── Enquiry/
│   │   ├── Tours/
│   │   ├── Calendar/
│   │   ├── Forms/
│   │   ├── Tools/
│   │   ├── Reports/
│   │   ├── Settings/
│   │   ├── Notifications/
│   │   ├── Learning/             # Phase 2
│   │   └── Payments/             # Phase 3
│   └── NMS.Shared/               # Shared kernel (Result, Pagination, etc.)
├── tests/
│   ├── NMS.UnitTests/
│   ├── NMS.IntegrationTests/
│   └── NMS.ArchitectureTests/    # NetArchTest rules
├── Directory.Packages.props      # Central package management
├── Directory.Build.props         # Common build settings
└── NMS.sln
```

**Conventions:**
- Clean Architecture / Modular Monolith hybrid.
- Each module is a project under `NMS.Modules/` (or a folder if too small).
- Modules communicate via in-process MediatR (CQRS) in Phase 1–2, extracted to RabbitMQ events in Phase 3 if needed.
- No direct DB access from controllers — always via Application layer.
- All DB entities have `tenant_id`, EF Core global query filter enforces tenant isolation.

---

## 5. Web Frontend Project Structure

```
web/
├── public/
│   ├── locales/
│   │   ├── en/translation.json
│   │   └── ar/translation.json
│   └── favicon.ico
├── src/
│   ├── main.tsx                  # App entry
│   ├── App.tsx                   # Root component, router
│   ├── router/                   # React Router config
│   │   ├── index.tsx
│   │   └── routes.tsx
│   ├── api/                      # API client (axios + TanStack Query hooks)
│   │   ├── client.ts
│   │   ├── hooks/
│   │   │   ├── useStudents.ts
│   │   │   └── ...
│   │   └── types/                # Generated from OpenAPI
│   ├── modules/                  # One folder per business module
│   │   ├── dashboard/
│   │   ├── centre-management/
│   │   ├── attendance/
│   │   ├── parent-communication/
│   │   ├── messaging/
│   │   ├── billing/
│   │   ├── enquiry/
│   │   ├── tours/
│   │   ├── calendar/
│   │   ├── forms/
│   │   ├── tools/
│   │   ├── reports/
│   │   ├── settings/
│   │   ├── learning/             # Phase 2
│   │   └── payments/             # Phase 3
│   ├── components/               # Shared components
│   │   ├── ui/                   # shadcn/ui primitives (Button, Input, etc.)
│   │   ├── layout/               # AppShell, Sidebar, Header
│   │   ├── shared/               # EntityTable, FormDialog, etc.
│   │   └── charts/
│   ├── hooks/                    # Shared hooks
│   ├── stores/                   # Zustand stores
│   ├── lib/                      # Utilities
│   │   ├── i18n.ts
│   │   ├── api-client.ts
│   │   ├── auth.ts               # Keycloak JS adapter
│   │   ├── signalr.ts
│   │   └── utils.ts
│   ├── types/                    # Shared TS types
│   └── styles/
│       └── globals.css           # Tailwind directives
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.ts
├── postcss.config.js
└── index.html
```

**Conventions:**
- Feature folders (`src/modules/*`) are self-contained — components, hooks, types, routes all live in the module folder.
- Shared components in `src/components/`.
- API hooks generated from OpenAPI spec via `openapi-typescript-codegen` — never hand-write API client.
- All API calls go through TanStack Query — no direct fetch in components.
- Global state in Zustand only when truly global (auth, theme, locale); per-page state stays in TanStack Query cache.

---

## 6. Mobile App Project Structure

```
mobile/
├── app/                          # Expo Router file-based routing
│   ├── (auth)/
│   │   ├── login.tsx
│   │   └── forgot-password.tsx
│   ├── (staff)/
│   │   ├── dashboard.tsx
│   │   ├── attendance/
│   │   │   ├── index.tsx
│   │   │   └── [roomId].tsx
│   │   ├── activities/
│   │   ├── messaging/
│   │   ├── learning/             # Phase 2
│   │   └── profile.tsx
│   ├── (parent)/
│   │   ├── feed.tsx
│   │   ├── messaging.tsx
│   │   ├── daily-report.tsx
│   │   ├── learning-journey.tsx  # Phase 2
│   │   ├── invoices.tsx          # Phase 3
│   │   └── profile.tsx
│   └── _layout.tsx
├── src/
│   ├── api/                      # Same as web (shared types)
│   ├── modules/                  # Same modules as web (RN components)
│   ├── components/
│   │   ├── ui/                   # RN equivalents of shadcn/ui
│   │   └── shared/
│   ├── hooks/
│   ├── stores/
│   ├── lib/
│   │   ├── i18n.ts              # Shared with web
│   │   ├── auth.ts              # Keycloak RN adapter (expo-auth-session)
│   │   ├── signalr.ts           # @microsoft/signalr client
│   │   ├── offline.ts           # WatermelonDB / SQLite setup
│   │   └── utils.ts
│   └── types/
├── assets/
│   ├── images/
│   └── fonts/
├── app.config.ts                 # Expo config (dynamic per env)
├── package.json
├── tsconfig.json
├── tailwind.config.ts            # NativeWind
└── eas.json                      # EAS Build config
```

**Shared code with web:**
- `src/types/` — all TypeScript types (Zod schemas too).
- `src/lib/i18n.ts` — i18n setup (uses same translation JSON files).
- `src/api/types/` — OpenAPI-generated types.
- `src/stores/` — Zustand stores (where UI-agnostic).

**Not shared (UI code):**
- `src/components/` — different primitives for web (DOM) vs RN (View/Text).
- `src/modules/*/` — UI implementations differ but use shared types + state.

---

## 7. Infrastructure Project Structure

```
infra/
├── terraform/
│   ├── modules/
│   │   ├── vpc/
│   │   ├── eks/
│   │   ├── rds/
│   │   ├── elasticache/
│   │   ├── s3/
│   │   ├── keycloak/
│   │   ├── cloudfront/
│   │   ├── route53/
│   │   └── secrets/
│   ├── environments/
│   │   ├── dev/
│   │   │   ├── main.tf
│   │   │   ├── variables.tf
│   │   │   └── terraform.tfvars
│   │   ├── staging/
│   │   └── production/
│   └── shared/                   # Shared state backend config
├── helm/
│   ├── nms-api/                  # Backend Helm chart
│   ├── nms-web/                  # Web frontend Helm chart
│   ├── nms-mobile-api/           # Mobile-specific API (if extracted)
│   └── keycloak/                 # Keycloak Helm chart values
├── k8s/
│   ├── base/                     # Kustomize base manifests
│   └── overlays/
│       ├── dev/
│       ├── staging/
│       └── production/
├── docker/
│   ├── backend.Dockerfile
│   ├── web.Dockerfile
│   ├── keycloak.Dockerfile       # Custom Keycloak with our theme + providers
│   └── docker-compose.yml        # Local dev environment
└── scripts/
    ├── bootstrap.sh              # First-time setup
    ├── seed-dev-data.sh          # Seed dev DB
    └── backup.sh                 # Manual backup trigger
```

---

## 8. Tooling & Developer Experience

### 8.1 Required Local Tools

- .NET 8 SDK
- Node.js 20 LTS
- pnpm 9+ (web + mobile package manager)
- Docker Desktop (or colima on macOS)
- kubectl + helm
- Terraform 1.9+
- AWS CLI v2 (for SaaS) / customer cloud CLI (for on-prem)
- Expo CLI (`npm install -g expo-cli`)
- Git 2.40+

### 8.2 Recommended IDE Setup

- **Cursor** (primary) with settings synced via `.cursorrules` (see `18-CURSOR-RULES.md`).
- VS Code as fallback (Cursor is VS Code-based, so all settings port).
- Extensions (auto-recommended by `.vscode/extensions.json`):
  - C# Dev Kit
  - Tailwind CSS IntelliSense
  - ESLint
  - Prettier
  - i18n Ally
  - Prisma (if using Prisma — we're not, but useful for schema visualization)
  - REST Client
  - GitLens

### 8.3 Local Dev Environment

`docker compose up` from `infra/docker/` starts:
- PostgreSQL 16 (port 5432)
- Redis 7 (port 6379)
- RabbitMQ 3.13 (ports 5672 + 15672 management UI)
- Keycloak 24 (port 8080, admin/admin)
- MinIO (S3-compatible, ports 9000 + 9001 console)
- MailHog (port 1025 SMTP + 8025 UI) for email testing

Then:
- `dotnet watch run --project backend/src/NMS.Api` — backend hot reload
- `pnpm dev` in `web/` — Vite dev server (port 5173)
- `pnpm start` in `mobile/` — Expo dev server (port 8081)

Full local dev URL: `http://localhost:5173` (web), `http://localhost:8081` (mobile via Expo Go).

### 8.4 Code Generation

| Generator | Source | Output | When to run |
|---|---|---|---|
| `openapi-typescript-codegen` | `backend/openapi.json` | `web/src/api/` + `mobile/src/api/` | On every PR that changes API |
| EF Core migrations | C# entity changes | `backend/src/NMS.Infrastructure/Persistence/Migrations/` | When entities change |
| TypeScript types from Zod | Zod schemas | Inline TS types | Automatic (Zod inference) |
| Helm values schema | `values.yaml` | JSON schema for IDE validation | On chart changes |

### 8.5 Pre-commit Hooks

`.husky/pre-commit` runs:
- `dotnet format` (backend)
- `dotnet build` (backend, fails fast on compile errors)
- `pnpm lint` (web + mobile)
- `pnpm typecheck` (web + mobile)
- `pnpm test:unit` (web + mobile, fast unit tests only)

`.husky/pre-push` runs:
- All pre-commit checks
- `dotnet test` (full backend test suite)
- `pnpm test` (full web + mobile test suite)

---

## 9. Observability Stack Details

### 9.1 Metrics (Prometheus + Grafana)

- Backend exposes `/metrics` endpoint via `prometheus-net.AspNetCore`.
- Metrics: HTTP request rate, latency histogram (p50/p95/p99), error rate, DB pool usage, RabbitMQ queue depth, Redis hit rate, JVM (Keycloak), K8s pod metrics.
- Grafana dashboards: Infrastructure overview, Application performance, Business KPIs (daily attendance, active users, overdue invoices, ratio breaches).
- Alerts via Alertmanager → PagerDuty (critical), Slack #ops-alerts (warning), email (informational).

### 9.2 Logs (Loki + Grafana, or ELK)

- Backend: Serilog with JSON sink → Promtail → Loki → Grafana.
- Frontend: structured console logs in dev, Sentry in production (error + performance monitoring).
- Mobile: Sentry for crash reporting + performance.
- Log retention: 90 days hot (Loki), 365 days cold (S3).
- Log level configurable per module without restart (Spring Boot Actuator equivalent in .NET: `IOptionsMonitor<LoggerFilterOptions>`).

### 9.3 Tracing (OpenTelemetry + Tempo)

- OpenTelemetry.Instrumentation for ASP.NET Core, EF Core, HttpClient, StackExchange.Redis.
- Traces exported to OpenTelemetry Collector → Tempo.
- Trace sampling: 100% for errors, 10% for normal requests in prod (100% in dev/staging).
- Cross-service tracing critical when services extract in Phase 3.

### 9.4 Synthetics

- Heartbeat endpoint `/health/live` and `/health/ready` (Kubernetes probes).
- Synthetic checks (Grafana Synthetics or AWS CloudWatch Synthetics):
  - Login flow every 5 min from UAE region.
  - Dashboard load every 5 min.
  - Parent portal load every 5 min.
  - API health every 1 min.
- Alert if any synthetic fails 3x in a row.

---

## 10. Security Tooling

- **SAST**: SonarCloud on every PR.
- **SCA**: OWASP Dependency Check + GitHub Dependabot on every PR.
- **DAST**: OWASP ZAP scan weekly against staging.
- **Container scanning**: Trivy on every container image push.
- **IaC scanning**: tfsec on every Terraform PR.
- **Secrets scanning**: GitGuardian or GitHub Secret Scanning on every push.
- **Penetration testing**: External pen test before each phase go-live (Sprint 17 for Phase 1, Sprint 26 for Phase 2, Sprint 30 for Phase 3).
- **Bug bounty**: Phase 3+ consideration (HackerOne or Intigriti).

---

## 11. Scaffolding Commands (Sprint 0 Setup)

Run these once to bootstrap the project:

```bash
# 1. Create monorepo structure
mkdir nms && cd nms
git init
mkdir -p backend web mobile infra docs

# 2. Backend: .NET 8 solution
dotnet new sln -n NMS
dotnet new webapi -n NMS.Api -o backend/src/NMS.Api --use-controllers false
dotnet new classlib -n NMS.Domain -o backend/src/NMS.Domain
dotnet new classlib -n NMS.Application -o backend/src/NMS.Application
dotnet new classlib -n NMS.Infrastructure -o backend/src/NMS.Infrastructure
dotnet new classlib -n NMS.Shared -o backend/src/NMS.Shared
dotnet sln add backend/src/**/*.csproj

# Add reference chain
dotnet add backend/src/NMS.Api reference backend/src/NMS.Application backend/src/NMS.Infrastructure
dotnet add backend/src/NMS.Application reference backend/src/NMS.Domain backend/src/NMS.Shared
dotnet add backend/src/NMS.Infrastructure reference backend/src/NMS.Domain backend/src/NMS.Application
dotnet add backend/src/NMS.Domain reference backend/src/NMS.Shared

# Central package management
dotnet new packages-props -o backend/

# 3. Web: Vite + React + TypeScript
pnpm create vite@latest web -- --template react-ts
cd web
pnpm add @tanstack/react-query zustand react-hook-form zod @hookform/resolvers \
  react-router-dom react-i18next i18next i18next-browser-languagedetector \
  @tanstack/react-table @radix-ui/react-* tailwindcss class-variance-authority clsx tailwind-merge
pnpm add -D @types/node typescript @typescript-eslint/eslint-plugin @typescript-eslint/parser \
  eslint eslint-plugin-react eslint-plugin-react-hooks prettier \
  eslint-config-prettier eslint-plugin-prettier vite @vitejs/plugin-react
npx shadcn@latest init
cd ..

# 4. Mobile: Expo + React Native + TypeScript
npx create-expo-app@latest mobile --template typescript
cd mobile
pnpm add @tanstack/react-query zustad react-hook-form zod @hookform/resolvers \
  expo-router expo-secure-store expo-auth-session expo-notifications \
  @microsoft/signalr @react-native-async-storage/async-storage \
  nativewind react-native-paper
pnpm add -D typescript @types/react @types/react-native tailwindcss
cd ..

# 5. Infrastructure
mkdir -p infra/terraform/modules infra/terraform/environments infra/helm infra/k8s infra/docker

# 6. Initial commit
git add -A
git commit -m "Sprint 0: Initial monorepo scaffold (backend .NET 8, web Vite+React+TS, mobile Expo+RN+TS)"
```

Detailed Sprint 0 plan: `19-ROADMAP-SPRINTS.md`.

---

**Next file to read**: `06-ARCHITECTURE.md` for system architecture, module boundaries, and C4 diagrams.
