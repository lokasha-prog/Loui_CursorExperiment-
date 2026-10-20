# 🚀 NMS Project — Master Cursor Execution Prompt

> **Paste this entire file into Cursor's chat (or use as `@` context) to bootstrap and execute the NMS project step-by-step.**
>
> The Cursor agent will read the project specs, follow the conventions, build the code, and update the PMO dashboard as it progresses.

---

## 📋 PROJECT CONTEXT (Read this first)

You are about to build the **Nursery Management System (NMS) — Bright Futures**, a full-stack platform for UAE early childhood centres. The project is **completely specified** — your job is to execute, not design.

### What's Already Built (Don't Redo These)

A complete specification + visual mockup pack exists in `docs/prerequisites/` and `docs/screens/`:

| Asset Type | Count | Location | Purpose |
|---|---|---|---|
| `.md` spec files | 22 | `docs/prerequisites/*.md` | Build-ready specifications |
| Spec HTML demos | 22 | `docs/screens/spec-*.html` | Same specs, rendered beautifully |
| Web screen mockups | 25 | `docs/screens/screen-*.html` | Visual target for each UI screen |
| Mobile persona mockups | 7 | `docs/screens/mobile-*.html` | Phone mockups per role |
| Phase 3 previews | 2 | `docs/screens/phase3-*.html` | Multi-centre + payment flow |
| PMO dashboard | 1 | `docs/screens/project-pmo.html` | **Status tracker — UPDATE THIS after every sprint** |
| Master index | 1 | `docs/screens/index.html` | Browse all 58 pages |
| Cursor rules | 1 | `.cursorrules` (paste from spec-18) | Coding conventions |

### Tech Stack (LOCKED — Do Not Suggest Alternatives)

- **Backend**: .NET 8 (ASP.NET Core) + C# 12 + EF Core 8 + PostgreSQL 16
- **Real-time**: SignalR + Redis backplane
- **Queue**: RabbitMQ 3.13 via MassTransit
- **Identity**: Keycloak 24 (OAuth 2.0 / OIDC)
- **Storage**: S3-compatible (AWS S3 for SaaS, MinIO for on-prem)
- **Web**: React 18 + Vite + TypeScript + shadcn/ui + Tailwind CSS 3
- **Mobile**: React Native 0.74 + Expo SDK 51 (Bare Workflow)
- **Infra**: Kubernetes + Terraform + GitHub Actions
- **Observability**: Prometheus + Grafana + Loki + OpenTelemetry

### Commercial Model

Same codebase supports both:
- **On-prem** (single-tenant, customer's UAE infra)
- **SaaS** (multi-tenant, AWS ME Central)

---

## 🎯 EXECUTION PROTOCOL

### Step 0: Bootstrap (do this ONCE before any coding)

```
1. Read these files in order (they're in docs/screens/ — open as HTML in browser OR read the .md sources in docs/prerequisites/):

   MANDATORY (read all):
   - spec-00-master-brief.html        → project overview, file map, locked decisions
   - spec-02-prd-mvp.html             → MVP scope (build this first!)
   - spec-05-tech-stack.html          → concrete stack + scaffolding commands
   - spec-06-architecture.html        → modular monolith, module boundaries
   - spec-07-database-schema.html     → entities, RLS, JSONB i18n, audit log
   - spec-09-auth-security.html       → Keycloak, RBAC, parent-child ownership
   - spec-10-multi-tenancy.html       → shared schema + RLS, on-prem vs SaaS
   - spec-18-cursor-rules.html        → coding conventions (PASTE INTO .cursorrules)
   - spec-19-roadmap-sprints.html     → sprint-by-sprint plan (30 sprints)
   - project-pmo.html                 → current status dashboard

   VISUAL REFERENCES (open in browser when implementing each screen):
   - screen-login.html                → login UI target
   - screen-dashboard.html            → Centre Admin dashboard target
   - screen-attendance.html           → barcode check-in target
   - screen-activity-feed.html        → 8 activity types target
   - screen-messaging.html            → shared inbox target
   - screen-billing.html              → billing KPIs target
   - mobile-teacher.html              → teacher mobile app target
   - mobile-parent-feed.html          → parent mobile app target

2. Confirm the scaffolding commands from spec-05-tech-stack.html §11 work locally:
   - dotnet 8 SDK installed
   - Node.js 20 + pnpm 9 installed
   - Docker Desktop running
   - Expo CLI installed

3. Create the monorepo:
   mkdir nms && cd nms
   git init
   mkdir -p backend web mobile infra docs/prerequisites docs/screens

4. Copy ALL assets into the repo:
   - Copy docs/prerequisites/*.md (22 files) from /home/z/my-project/download/nms-prerequisites/
   - Copy docs/screens/*.html (58 files) from /home/z/my-project/download/nms-all-screens/
   - Paste spec-18-cursor-rules.html content into .cursorrules at repo root

5. Run the scaffolding commands from spec-05-tech-stack.html §11 (one block):
   - dotnet new sln -n NMS
   - dotnet new webapi, classlib for each project (NMS.Api, NMS.Domain, NMS.Application, NMS.Infrastructure, NMS.Modules, NMS.Shared)
   - pnpm create vite web --template react-ts
   - npx create-expo-app mobile --template typescript
   - Add all dependencies listed in spec-05-tech-stack.html §1

6. Set up Docker Compose for local dev (from spec-11-devops.html §11.2):
   - PostgreSQL 16 on :5432
   - Redis 7 on :6379
   - RabbitMQ 3.13 on :5672 (mgmt :15672)
   - Keycloak 24 on :8080 (admin/admin)
   - MinIO on :9000 (console :9001)
   - MailHog on :1025 (UI :8025)

7. Initial commit:
   git add -A
   git commit -m "Sprint 0: Initial monorepo scaffold with prerequisites + cursor rules"

8. UPDATE THE PMO DASHBOARD:
   - Open docs/screens/project-pmo.html
   - Edit the PROJECT_STATE JavaScript object at the bottom:
     - Set sprints[0].status = "done"
     - Set sprints[0].pct = 100
     - Set sprints[1].status = "active"
     - Set sprints[1].pct = 5
     - Set overallPct = 5
     - Update lastUpdated timestamp
   - Save the file
   - Commit: "PMO: Sprint 0 complete, Sprint 1 started"
```

---

### Step 1+: Sprint-by-Sprint Execution

For each sprint, follow this exact loop:

```
SPRINT N EXECUTION LOOP:

1. READ THE SPEC
   - Open docs/screens/spec-19-roadmap-sprints.html
   - Find the section for Sprint N
   - Read the deliverables list + acceptance criteria
   - Read the linked spec files (e.g., for Sprint 1: spec-02-prd-mvp §3.2, spec-07-database-schema §2.2, spec-08-api-spec §15)

2. READ THE VISUAL TARGET
   - Open the corresponding screen-*.html in a browser
   - Note the layout, components, data shown, interactions
   - This is the visual spec — match it closely

3. IMPLEMENT
   - Follow conventions in .cursorrules (CRITICAL — re-read if needed)
   - Backend: create entities → EF Core config → migration → DTOs → CQRS handlers → controllers → unit tests → integration tests
   - Frontend: generate API client from OpenAPI → create TanStack Query hooks → build components matching the visual target → add tests
   - Mobile (Sprint 15+): use same patterns as web, RN-specific components

4. ENFORCE NON-NEGOTIABLES (from .cursorrules):
   ✓ Every entity has tenant_id + RLS policy
   ✓ EF Core global query filter for tenant + soft delete
   ✓ Parent-child ownership check on EVERY parent-facing endpoint (return 404, not 403)
   ✓ All CSS uses logical properties (ms-*, me-*, text-start — NEVER ml-*, mr-*, text-left)
   ✓ All UI strings via react-i18next t('key') — NEVER hardcoded English
   ✓ All DB translatable fields as JSONB {"en": "...", "ar": "..."}
   ✓ Money as integer AED fils (bigint) — NEVER floating-point
   ✓ Audit log on every mutation (CREATE/UPDATE/DELETE)
   ✓ Tests: 80% backend coverage, 75% web, 70% mobile

5. RUN TESTS
   - dotnet test backend/NMS.sln
   - cd web && pnpm test
   - cd mobile && pnpm test (Sprint 15+)
   - All tests must pass before commit

6. UPDATE PMO DASHBOARD
   - Open docs/screens/project-pmo.html
   - Update PROJECT_STATE:
     - Set sprints[N].pct = 100, status = "done"
     - Set sprints[N+1].status = "active", pct = 5
     - Update overallPct (calculate: doneSprints/30 * 100)
     - Update lastUpdated timestamp
     - Update updatedBy = "Cursor Agent"
   - Commit: "PMO: Sprint N complete, Sprint N+1 started"

7. GIT COMMIT (conventional commits)
   - feat(<module>): <description>
   - test(<module>): <description>
   - docs(pmo): sprint N status update
   - Example: "feat(centre-mgmt): student + staff + room CRUD with RBAC and tests"

8. SUMMARY TO USER
   - What was built
   - Files created/modified (count)
   - Tests added (count, coverage)
   - Any deviations from spec (with reason)
   - Next sprint preview
```

---

## 🏃 SPRINT-BY-SPRINT PROMPTS

After Step 0 bootstrap, use these prompts one sprint at a time. **Do NOT batch sprints** — complete one fully before starting the next.

### Sprint 1 — Centre Management + Auth (Weeks 3-4)

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint 1 section)
- docs/screens/spec-02-prd-mvp.html (§3.2 Centre Management)
- docs/screens/spec-07-database-schema.html (§2.1 Tenant, §2.2 Centre Management entities)
- docs/screens/spec-08-api-spec.html (§15.1 Auth, §15.2-15.4 Students/Staff/Rooms)
- docs/screens/spec-09-auth-security.html (§1 Identity, §2 RBAC, §3 Permission matrix)
- docs/screens/spec-10-multi-tenancy.html (§3 Tenant context, §4 RLS setup)
- docs/screens/screen-login.html (visual target for login)
- docs/screens/screen-student-profile.html (visual target for student detail)
- docs/screens/screen-staff-list.html (visual target for staff list)
- docs/screens/screen-rooms-programs.html (visual target for rooms)

Implement Sprint 1 per spec-19:
1. Keycloak integration in backend (JWT validation, role-based auth)
2. Keycloak JS adapter in web frontend
3. Login page (match screen-login.html) + auth flow (login, logout, refresh)
4. 5 roles: super_admin, centre_admin, teacher, parent, receptionist
5. Tenant context middleware + RLS setup on all tenant tables
6. EF Core entities: Student, Staff, Room, Program, StudentGroup, Label, Guardian, StudentGuardian, AppUser, Role, UserRole
7. EF Core migrations for all entities + RLS policies
8. Audit log interceptor + audit_log table (append-only trigger)
9. API endpoints: students CRUD, staff CRUD, rooms CRUD, programs CRUD, groups CRUD, labels CRUD, guardians CRUD
10. Web frontend: login page, dashboard shell, students list+create, rooms list+create, staff list+create
11. i18n setup (EN + AR), language switcher, RTL support
12. shadcn/ui setup with theme (match the DNA in screen-login.html)
13. Unit tests for all entities + handlers (target 80% coverage)
14. Integration tests for all endpoints
15. Parent-child ownership tests (parent A cannot access parent B's child → 404)
16. Cross-tenant isolation tests (staff A tenant A cannot access tenant B → 404)

Follow conventions in .cursorrules. Use the directory structure specified.
Match the visual design in screen-login.html, screen-student-profile.html, screen-staff-list.html, screen-rooms-programs.html.

After completing, update docs/screens/project-pmo.html PROJECT_STATE:
- sprints[1].status = "done", pct = 100
- sprints[2].status = "active", pct = 5
- overallPct = 10
- lastUpdated = current timestamp

Commit with: "feat(centre-mgmt): Sprint 1 — student/staff/room/program/guardian CRUD with RBAC, RLS, audit, tests"
```

### Sprint 2 — Attendance + Dashboard (Weeks 5-6)

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint 2 section)
- docs/screens/spec-02-prd-mvp.html (§3.3 Attendance)
- docs/screens/spec-07-database-schema.html (§2.3 Attendance, §4 Materialized views)
- docs/screens/spec-08-api-spec.html (§15.5 Attendance, §15.6 Leaves)
- docs/screens/spec-06-architecture.html (§4.2 Module: Attendance, §5 Real-time)
- docs/screens/screen-attendance.html (visual target)
- docs/screens/screen-dashboard.html (visual target)

Implement Sprint 2 per spec-19:
1. EF Core entities: AttendanceRecord (partitioned by month), AttendanceEvent (immutable), LeaveRequest, LeaveType
2. Migrations for attendance entities + monthly partitioning
3. API endpoints: check-in, check-out, mark-absent, attendance list, attendance forecast, monthly report
4. Barcode input handling (HID keyboard emulation on web)
5. SignalR hub setup + Redis backplane
6. Real-time ratio calculation service
7. Ratio breach detection + event publication
8. Dashboard page (match screen-dashboard.html): KPI cards, live ratio widget, pending actions, recent activity, quick actions
9. WebSocket-driven dashboard updates (real-time via SignalR)
10. Materialized views: mv_daily_attendance_summary, mv_live_ratio
11. Hangfire setup for background jobs
12. Leave request workflow (create, approve, reject)
13. Unit + integration tests
14. Performance test: dashboard p95 < 2s (k6 smoke test)

After completing, update PMO dashboard:
- sprints[2].status = "done", pct = 100
- sprints[3].status = "active", pct = 5
- overallPct = 15

Commit: "feat(attendance): Sprint 2 — barcode check-in, live ratio via SignalR, dashboard, leaves"
```

### Sprint 3 — Parent Communication + Messaging (Weeks 7-8)

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint 3)
- docs/screens/spec-02-prd-mvp.html (§3.4 Parent Communication, §3.5 Messaging)
- docs/screens/spec-07-database-schema.html (§2.4 Media, §2.5 Messaging)
- docs/screens/spec-08-api-spec.html (§15.7 Activities, §15.8 Messaging, §15.9 Media)
- docs/screens/screen-activity-feed.html (visual target — match the 8 activity types + approval workflow)
- docs/screens/screen-messaging.html (visual target — match shared inbox + thread view)

Implement Sprint 3:
1. EF Core entities: Media, ActivityPost, ActivityPostStudent, ActivityPostMedia, DailyReport, MessageThread, Message, MessageReadReceipt, ThreadParticipant
2. Presigned URL upload flow (POST /media/presign → client uploads to S3 → POST /activities with media IDs)
3. Content moderation pipeline: ClamAV scan + AWS Rekognition (or Azure Content Safety)
4. Media Processing module: consumes MediaUploaded events, scans, updates scan_status
5. Activity post creation + approval workflow (PENDING → APPROVED/REJECTED → parent visibility)
6. 8 activity types: announcement, photo, video, diary, food, toilet, medical, mood
7. Parent activity feed (chronological, cursor paginated, real-time via SignalR)
8. Daily report generation (Hangfire job at 3:30 PM local) + email + push
9. Messaging: shared inbox, threads, read receipts, broadcast
10. Parent messaging UI + staff messaging UI (match screen-messaging.html)
11. Unit + integration tests

After completing, update PMO:
- sprints[3] done, sprints[4] active, overallPct = 20

Commit: "feat(parent-comms): Sprint 3 — activity feed (8 types), approval workflow, media pipeline, messaging"
```

### Sprint 4 — Notifications + Audit + Emergency Reports (Weeks 9-10)

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint 4)
- docs/screens/spec-02-prd-mvp.html (§3.7 Notifications, §3.8 Audit)
- docs/screens/spec-07-database-schema.html (§2.7 Notifications, §2.8 Audit Log)
- docs/screens/spec-08-api-spec.html (§15.10 Notifications, §15.12 Settings/Audit)
- docs/screens/spec-15-notifications.html (full notification system spec — 3 tiers, channels, fallback)
- docs/screens/screen-notifications.html (visual target)
- docs/screens/screen-emergency-report.html (visual target — one-click PDF)
- docs/screens/screen-settings-audit.html (visual target — audit log viewer)

Implement Sprint 4:
1. EF Core entities: Notification, NotificationDelivery, NotificationPreference, NotificationTemplate
2. Notification module: consumes events from other modules, creates notifications, dispatches to channels
3. 3 tiers: Critical (push + in-app + email fallback), Operational (push + in-app), Informational (email + optional in-app)
4. Notification dispatcher (Hangfire job): processes queued deliveries, retries with backoff
5. FCM integration for push notifications
6. SES integration for transactional email
7. In-app notifications: bell icon, notification center, mark-as-read
8. User notification preferences (Settings → Notifications)
9. Audit log viewer (Super Admin): filterable by user, date, entity, action
10. Emergency child report: one-click PDF per student + bulk per room (QuestPDF)
11. Mobile app skeleton: login, dashboard, attendance scan, activity feed, messaging, push notifications
12. Unit + integration tests

After completing, update PMO:
- sprints[4] done, sprints[5] active, overallPct = 25

Commit: "feat(notifications): Sprint 4 — 3-tier system, audit log viewer, emergency reports, mobile skeleton"
```

### Sprint 5 — Pilot Deployment + UAT + Go-Live (Weeks 11-12)

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint 5)
- docs/screens/spec-02-prd-mvp.html (§4 MVP Acceptance Criteria, §5 Dependencies)
- docs/screens/spec-11-devops.html (full DevOps spec — Terraform, Helm, CI/CD, observability)
- docs/screens/spec-21-delivery-checklist.html (go-live readiness checklist)
- docs/screens/project-pmo.html (current status)

Implement Sprint 5:
1. Production infrastructure provisioned (Terraform): EKS, RDS, ElastiCache, Amazon MQ, S3, CloudFront, Route53, KMS, Secrets Manager, WAF
2. Helm charts for nms-api, nms-web, Keycloak
3. CI/CD pipeline complete: PR → dev → staging → production (manual approval)
4. Observability: Prometheus, Grafana, Loki, Alertmanager, Sentry (web + mobile)
5. Backups: daily RDS snapshot + WAL archiving
6. Staging environment deployed with seed data
7. UAT with internal team (1 week) — fix all P0 + P1 bugs
8. Pilot nursery #1 onboarding: data migration, staff training, parent invitations
9. Go-live at pilot nursery #1
10. In-app feedback button
11. Daily check-ins with pilot nursery during week 12
12. Pilot feedback survey sent to staff + parents
13. Pilot retrospective document

Exit criteria (from spec-02 §4):
- Pilot nursery live with ≥10 staff + ≥30 parents active
- ≥80% of expected attendance logged in system
- 0 critical bugs open, ≤3 high bugs open
- Parent portal p95 < 2.5s
- Ratio breach alert delivery < 30s
- Pilot user NPS ≥ 30
- ≥60% of pilot users "would recommend"

After completing, update PMO:
- sprints[5] done, sprints[6] active, overallPct = 35
- pilotNurseries = 1
- phasesLive = 1 (MVP pilot live)

Commit: "feat(pilot): Sprint 5 — production deploy, pilot nursery #1 live, MVP complete"
```

### Sprints 6-18 — Phase 1 Full

For each sprint, use this template (replace N, MODULE, FILES):

```
Read these files:
- docs/screens/spec-19-roadmap-sprints.html (Sprint N section)
- docs/screens/spec-03-prd-phase1.html (relevant module section)
- docs/screens/spec-08-api-spec.html (relevant endpoints)
- docs/screens/screen-<MODULE>.html (visual target)

Implement Sprint N per spec-19:
1. <deliverables from spec-19 for this sprint>
2. <more deliverables>

Match the visual target in screen-<MODULE>.html.
Follow conventions in .cursorrules.

After completing, update PMO:
- sprints[N] done, sprints[N+1] active, overallPct = <calculate>

Commit: "feat(<module>): Sprint N — <description>"
```

Sprint mapping for Phase 1:
- **Sprint 6**: Pilot Feedback + Stabilization
- **Sprint 7-8**: Billing & Accounting (fee plans, invoices, payments, reminders) → `screen-billing.html`
- **Sprint 9-10**: Enquiry + Tour + Settings → `screen-enquiry-pipeline.html`, `screen-tour-management.html`
- **Sprint 11-12**: Calendar + Forms + Tools → `screen-calendar.html`, `screen-forms-builder.html`, `screen-admissions-process.html`, `screen-id-cards.html`, `screen-certificates.html`
- **Sprint 13-14**: Reports + SMS + RTL QA → `screen-reports-analytics.html`, `screen-notification-preferences.html`
- **Sprint 15-16**: Mobile App Full → `mobile-teacher.html`, `mobile-parent-feed.html`, `mobile-admin.html`, `mobile-accounts.html`, `mobile-receptionist.html`
- **Sprint 17-18**: QA + Go-Live (pen test, load test, WCAG audit, UAT, production deploy, first paid customers)

### Sprints 19-30 — Phase 2 + Phase 3

Follow the same pattern, referencing:
- **Phase 2 (19-26)**: `spec-04-prd-phase2-3.html` §1-3 (Learning & Assessment, mobile maturity)
- **Phase 3 (27-30)**: `spec-04-prd-phase2-3.html` §5-10 (multi-centre, payments, ERP, Open API)
- Visual targets: `phase3-multi-centre.html`, `phase3-payment-flow.html`, `mobile-parent-payments.html`, `mobile-superadmin.html`

---

## ⚠️ CRITICAL RULES (Do Not Violate)

### 1. Read Before You Code
**Always** read the relevant spec file + visual mockup before implementing. The specs are detailed and authoritative. If you guess, you'll violate conventions.

### 2. Update PMO After Every Sprint
This is non-negotiable. Open `docs/screens/project-pmo.html`, edit `PROJECT_STATE`, commit. The dashboard is the source of truth for project status.

### 3. One Sprint at a Time
Never batch sprints. Complete one fully (including tests + PMO update + commit) before starting the next.

### 4. Match the Visual DNA
Every screen must match the corresponding `screen-*.html` or `mobile-*.html` mockup:
- Warm terracotta `#E07A5F` + sage `#81B29A` + amber `#E9C46A` palette
- Fredoka (headings) + Nunito (body) + JetBrains Mono (code) + IBM Plex Sans Arabic (RTL)
- Nursery-card surfaces, pill badges, dot status indicators, soft radii
- Same component patterns (cards, pills, dots, avatars, buttons)

### 5. Bilingual + RTL First-Class
- All UI strings via `t('key')` in react-i18next
- All CSS uses logical properties (`ms-*`, `me-*`, `text-start`)
- All DB translatable fields as JSONB `{"en": "...", "ar": "..."}`
- Test every screen in both LTR and RTL

### 6. Multi-Tenancy + Parent-Child Ownership
- Every entity has `tenant_id` + RLS policy
- EF Core global query filter for tenant + soft delete
- Parent-child ownership check on EVERY parent-facing endpoint (return 404, not 403)

### 7. Tests Required
- 80% backend coverage, 75% web, 70% mobile
- Unit + integration + E2E (Playwright for web, Detox for mobile)
- All tests must pass before commit

### 8. Conventional Commits
- `feat(<module>): <description>`
- `test(<module>): <description>`
- `docs(pmo): sprint N status update`
- `fix(<module>): <description>`

---

## 🆘 When Stuck

If you're unsure about something:

1. **Read more spec files** — the answer is probably there
2. **Look at existing code** — follow established patterns
3. **Write a test first** — clarify expected behavior before implementing
4. **Add an ADR** — if making a non-trivial decision, document it in `/docs/adr/` using the Nygard format from `spec-20-adrs.html`
5. **Ask the user** — for architecture decisions or scope changes

---

## 📊 Quick Reference: PMO Update Pattern

After every sprint, this is the exact edit to make in `docs/screens/project-pmo.html`:

```javascript
// Find the PROJECT_STATE object (near the bottom of the file)
// Update these fields:

const PROJECT_STATE = {
  lastUpdated: "2026-XX-XX HH:MM GST",  // current timestamp
  updatedBy: "Cursor Agent",
  overallPct: <NEW_PERCENT>,  // (completedSprints / 30) * 100, rounded

  sprints: {
    // ... previous sprints unchanged ...
    <N>:   { status: "done",    pct: 100, title: "...", notes: "...", deliverables: "..." },
    <N+1>: { status: "active",  pct: 5,   title: "...", notes: "...", deliverables: "..." },
    // ... remaining sprints unchanged ...
  },

  risks: [
    // update if new risks emerge or old ones resolve
  ],

  pilotNurseries: <COUNT>,  // increment when pilot goes live (Sprint 5)
  openBugs: <COUNT>,         // update as bugs are found/fixed
  phasesLive: <COUNT>        // 0=MVP not live, 1=MVP live, 2=Phase 1, 3=Phase 2, 4=Phase 3
};
```

Then commit: `docs(pmo): sprint N complete, sprint N+1 started`

---

## 🎬 START HERE

If you're seeing this prompt for the first time, respond with:

```
✓ Acknowledged. I will execute the NMS project per this protocol.

Starting Step 0 (Bootstrap):
1. Reading mandatory spec files...
2. Confirming local toolchain...
3. Scaffolding monorepo...
4. Copying assets...
5. Setting up Docker Compose...
6. Initial commit...
7. Updating PMO dashboard...

Proceed? (y/n)
```

Then wait for user confirmation before proceeding.

**Begin.**
