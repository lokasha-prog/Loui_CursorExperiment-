# 19 — Roadmap & Sprints

> Sprint-by-sprint breakdown. MVP first (Sprints 0–5), then Phase 1 full (Sprints 6–18), then Phase 2 (Sprints 19–26), then Phase 3 (Sprints 27–30).

---

## 1. Roadmap Overview

| Phase | Sprints | Weeks | Outcome |
|---|---|---|---|
| **MVP Pilot** | 0–5 | 1–12 | Pilot live at 1–2 nurseries |
| **Phase 1 Full** | 6–18 | 13–38 | All 13 modules live, first paid customers |
| **Phase 2** | 19–26 | 39–54 | Learning & Assessment, mobile maturity |
| **Phase 3** | 27–30 | 55–62 | Multi-centre, payments, ERP, Open API |

Each sprint is **2 weeks**. Sprint 0 is the setup sprint. Total Phase 1: 38 weeks (19 sprints).

---

## 2. MVP Pilot (Sprints 0–5)

### Sprint 0 — Setup & Planning (Weeks 1–2)

**Goal**: Environment ready, scope locked, pilot nurseries signed.

**Deliverables**:
- [ ] Monorepo scaffolded: `backend/` (.NET 8), `web/` (Vite+React+TS), `mobile/` (Expo+RN+TS), `infra/`, `docs/`.
- [ ] `.cursorrules` in place.
- [ ] Docker Compose for local dev (PostgreSQL 16, Redis 7, RabbitMQ 3.13, Keycloak 24, MinIO, MailHog).
- [ ] CI/CD pipeline skeleton (GitHub Actions): build, test, lint, typecheck on PR.
- [ ] Keycloak realm configured for first tenant.
- [ ] EF Core initial migration (tenant + app_user + role tables).
- [ ] First commit on `main`.
- [ ] Pilot nursery #1 signed (LOI or pilot agreement).
- [ ] Pilot nursery #2 in conversation (target: signed by Sprint 2).
- [ ] Legal counsel engaged for PDPL review.
- [ ] Cloud provider selected (AWS ME Central recommended).
- [ ] Tech Lead + Product Owner + 2 backend + 1 frontend + 1 mobile + 1 QA identified.

**Exit criteria**:
- Local dev environment runs (`docker compose up` + `dotnet watch run` + `pnpm dev` all succeed).
- CI passes on a "hello world" PR.
- Pilot nursery #1 committed.

**Files to read first**: `00-MASTER-BRIEF.md`, `02-PRD-MVP.md`, `05-TECH-STACK.md`, `11-DEVOPS-INFRASTRUCTURE.md`, `18-CURSOR-RULES.md`.

---

### Sprint 1 — Centre Management + Auth (Weeks 3–4)

**Goal**: User can log in, create rooms/students/staff, see them in a list.

**Deliverables**:
- [ ] Keycloak integration in backend (JWT validation, role-based auth).
- [ ] Keycloak JS adapter in web frontend.
- [ ] Login page + auth flow (login, logout, refresh).
- [ ] Role-based access control (5 roles: super_admin, centre_admin, teacher, parent, receptionist).
- [ ] Tenant context middleware + RLS setup.
- [ ] EF Core entities: Student, Staff, Room, Program, StudentGroup, Label, Guardian, StudentGuardian, AppUser, Role, UserRole.
- [ ] EF Core migrations for all entities.
- [ ] RLS policies on all tenant tables.
- [ ] Audit log interceptor + audit_log table (append-only trigger).
- [ ] API endpoints: students CRUD, staff CRUD, rooms CRUD, programs CRUD, groups CRUD, labels CRUD, guardians CRUD.
- [ ] Web frontend: login page, dashboard shell, students list + create, rooms list + create, staff list + create.
- [ ] i18n setup (EN + AR), language switcher, RTL support.
- [ ] shadcn/ui setup with theme (per `14-UX-UI-DESIGN.md`).
- [ ] Unit tests for all entities + handlers (target 80% coverage).
- [ ] Integration tests for all endpoints.
- [ ] Parent-child ownership tests (parent A cannot access parent B's child).

**Exit criteria**:
- Centre Admin can log in, create rooms, programs, students, staff, guardians.
- Teacher can log in, see rooms they're assigned to.
- Parent can log in, see only their children.
- All tests pass on CI.
- Both LTR and RTL UI tested.

**Files to read first**: `02-PRD-MVP.md` §3.2, `07-DATABASE-SCHEMA.md` §2.2, `08-API-SPEC.md` §15, `09-AUTH-SECURITY.md`, `12-WEB-FRONTEND.md`, `14-UX-UI-DESIGN.md`.

---

### Sprint 2 — Attendance + Dashboard (Weeks 5–6)

**Goal**: Staff can check students in/out, see live ratios on dashboard.

**Deliverables**:
- [ ] EF Core entities: AttendanceRecord (partitioned), AttendanceEvent (immutable), LeaveRequest, LeaveType.
- [ ] Migrations for attendance entities + monthly partitioning.
- [ ] API endpoints: check-in, check-out, mark-absent, attendance list, attendance forecast, monthly report.
- [ ] Barcode input handling (HID keyboard emulation on web).
- [ ] Mobile camera scanning (react-native-vision-camera + MLKit) — early mobile skeleton.
- [ ] SignalR hub setup + Redis backplane.
- [ ] Real-time ratio calculation service.
- [ ] Ratio breach detection + event publication.
- [ ] Dashboard page: KPI cards, attendance summary, live ratio widget, pending actions widget.
- [ ] WebSocket-driven dashboard updates (real-time).
- [ ] Materialized views: `mv_daily_attendance_summary`, `mv_live_ratio`.
- [ ] Hangfire setup for background jobs.
- [ ] Leave request workflow (create, approve, reject).
- [ ] Unit tests + integration tests.
- [ ] Performance test: dashboard p95 < 2s (k6 smoke test).

**Exit criteria**:
- Staff can check in/out students via barcode + manual button.
- Dashboard shows live ratios, updates in real-time.
- Ratio breach triggers Critical notification (push + in-app banner).
- Leave request workflow functional.

**Files to read first**: `02-PRD-MVP.md` §3.3, `07-DATABASE-SCHEMA.md` §2.3, `08-API-SPEC.md` §15.5, `06-ARCHITECTURE.md` §4.2 + §5.

---

### Sprint 3 — Parent Communication + Messaging (Weeks 7–8)

**Goal**: Teachers can post activities, parents see them; staff + parents can message.

**Deliverables**:
- [ ] EF Core entities: Media, ActivityPost, ActivityPostStudent, ActivityPostMedia, DailyReport.
- [ ] EF Core entities: MessageThread, Message, MessageReadReceipt, ThreadParticipant.
- [ ] Migrations for all entities.
- [ ] Presigned URL upload flow (POST /media/presign → client uploads to S3 → POST /activities with media IDs).
- [ ] Content moderation pipeline: ClamAV scan + AWS Rekognition (or Azure Content Safety).
- [ ] Media Processing module: consumes MediaUploaded events, scans, updates scan_status.
- [ ] Activity post creation + approval workflow (PENDING → APPROVED/REJECTED → parent visibility).
- [ ] 8 activity types: announcement, photo, video, diary, food, toilet, medical, mood.
- [ ] Parent activity feed (chronological, cursor paginated, real-time via SignalR).
- [ ] Daily report generation (Hangfire job at 3:30 PM local) + email + push.
- [ ] Messaging: shared inbox, threads, read receipts, broadcast.
- [ ] Parent messaging UI + staff messaging UI.
- [ ] Mobile app skeleton: login, dashboard, attendance scan, activity feed, messaging.
- [ ] Unit + integration tests.

**Exit criteria**:
- Teacher can post photo activity, it gets auto-approved after scan, parent sees it.
- Parent can message staff, staff see it in shared inbox, staff reply, parent sees reply.
- Daily report generated at 3:30 PM, emailed to parent.

**Files to read first**: `02-PRD-MVP.md` §3.4 + §3.5, `07-DATABASE-SCHEMA.md` §2.4 + §2.5, `08-API-SPEC.md` §15.7 + §15.8.

---

### Sprint 4 — Notifications + Audit + Emergency Reports (Weeks 9–10)

**Goal**: Notification system live (3 tiers), audit log viewer, emergency reports.

**Deliverables**:
- [ ] EF Core entities: Notification, NotificationDelivery, NotificationPreference, NotificationTemplate.
- [ ] Notification module: consumes events from other modules, creates notifications, dispatches to channels.
- [ ] 3 tiers: Critical (push + in-app + SMS — but SMS deferred to Sprint 13; Phase 1 MVP uses push + in-app + email fallback), Operational (push + in-app), Informational (email + optional in-app).
- [ ] Notification dispatcher (Hangfire job): processes queued deliveries, retries with backoff.
- [ ] Critical tier fallback chain (push fail → email).
- [ ] FCM integration for push notifications.
- [ ] SES integration for transactional email.
- [ ] In-app notifications: bell icon, notification center, mark-as-read.
- [ ] User notification preferences (Settings → Notifications).
- [ ] Audit log viewer (Super Admin): filterable by user, date, entity, action.
- [ ] Emergency child report: one-click PDF per student + bulk per room.
- [ ] PDF generation via QuestPDF.
- [ ] Mobile app: push notification handling, offline emergency report cache (WatermelonDB).
- [ ] Unit + integration tests.

**Exit criteria**:
- Ratio breach triggers Critical notification (push + in-app + email if push fails).
- Parent receives daily report email.
- Super Admin can view audit log + filter.
- Emergency report PDF generated in < 2s per student.
- Mobile app receives push notifications.

**Files to read first**: `02-PRD-MVP.md` §3.7 + §3.8, `07-DATABASE-SCHEMA.md` §2.7 + §2.8, `08-API-SPEC.md` §15.10 + §15.12, `15-NOTIFICATIONS.md`.

---

### Sprint 5 — Pilot Deployment + UAT + Go-Live (Weeks 11–12)

**Goal**: MVP deployed to pilot nursery, real users, structured feedback loop.

**Deliverables**:
- [ ] Production infrastructure provisioned (Terraform): EKS, RDS, ElastiCache, Amazon MQ, S3, CloudFront, Route53, KMS, Secrets Manager, WAF.
- [ ] Helm charts for nms-api, nms-web, Keycloak.
- [ ] CI/CD pipeline complete: PR → dev → staging → production (manual approval).
- [ ] Observability: Prometheus, Grafana, Loki, Alertmanager, Sentry (web + mobile).
- [ ] Backups: daily RDS snapshot + WAL archiving.
- [ ] Staging environment deployed with seed data.
- [ ] UAT with internal team (1 week).
- [ ] Bug fixes from UAT.
- [ ] Pilot nursery #1 onboarding: data migration from existing system, staff training, parent invitations.
- [ ] Go-live at pilot nursery #1.
- [ ] In-app feedback button.
- [ ] Daily check-ins with pilot nursery during week 12.
- [ ] Pilot feedback survey sent to staff + parents.
- [ ] Pilot retrospective document.

**Exit criteria** (MVP pilot exit gates, see `02-PRD-MVP.md` §4):
- Pilot nursery live with ≥10 staff + ≥30 parents active.
- ≥80% of expected attendance logged in system.
- 0 critical bugs open, ≤3 high bugs open.
- Parent portal p95 < 2.5s.
- Ratio breach alert delivery < 30s.
- Pilot user NPS ≥ 30.
- ≥60% of pilot users "would recommend".

**Files to read first**: `02-PRD-MVP.md` §4, `11-DEVOPS-INFRASTRUCTURE.md`, `21-DELIVERY-CHECKLIST.md`.

---

## 3. Phase 1 Full (Sprints 6–18)

After MVP pilot is live + stable, expand to full Phase 1.

### Sprint 6 — Pilot Feedback + Stabilization (Weeks 13–14)

**Goal**: Address pilot feedback, stabilize, prepare for Phase 1 expansion.

**Deliverables**:
- [ ] Triage pilot feedback (bugs + feature requests).
- [ ] Fix critical + high bugs from pilot.
- [ ] Performance optimization based on real load data.
- [ ] Documentation: admin guide, parent guide, teacher quick-start.
- [ ] Sprint 7–18 plan finalized with Product Owner.
- [ ] Pilot nursery #2 onboarding (if signed).
- [ ] On-prem deployment option prepared (Helm chart + Terraform for customer infra).

---

### Sprint 7–8 — Billing & Accounting (Weeks 15–18)

**Goal**: Full billing module — fee plans, auto invoice generation, payment recording, reminders.

**Deliverables**:
- [ ] Fee Plan CRUD + Fee Components + Discounts + Tax Rates.
- [ ] Student → Fee Plan association (with effective date ranges).
- [ ] Invoice auto-generation (Hangfire recurring job, monthly).
- [ ] Manual invoice creation.
- [ ] Invoice lifecycle: Draft → Issued → Partially Paid → Paid → Overdue → Cancelled.
- [ ] Payment recording (manual: cash, cheque, bank transfer).
- [ ] Credit balance tracking for overpayments.
- [ ] Automated fee reminders (configurable schedule, email + push).
- [ ] Email templates for invoices + reminders.
- [ ] PDF invoice generation (QuestPDF, bilingual).
- [ ] Billing dashboard (total billed, collected, overdue, outstanding, revenue trend).
- [ ] Expense recording.
- [ ] Reports: monthly revenue, overdue invoice list.
- [ ] Phase 1 Sprint 7+: payment link generation (manual payment recording only; gateway in Phase 3).

**Files to read first**: `03-PRD-PHASE1-FULL.md` §8.

---

### Sprint 9–10 — Enquiry + Tour Management + Settings (Weeks 19–22)

**Goal**: CRM pipeline for enquiries, tour scheduling, full settings module.

**Deliverables**:
- [ ] Enquiry pipeline: 6 stages (No Stage → Interested → Contacted → Tour Appointment → Toured → Enrolled).
- [ ] Enquiry CRUD + stage transitions + stage history log.
- [ ] Enquiry source tracking + analytics.
- [ ] Convert enquiry to student (preserves source for analytics).
- [ ] Online self-service enquiry form (public, no auth, reCAPTCHA protected).
- [ ] Tour appointment scheduling + calendar view.
- [ ] Staff assignment + block-off management.
- [ ] Tour confirmation email (Phase 1: email only, no SMS).
- [ ] Post-tour follow-up task auto-creation.
- [ ] Settings module full:
  - Programme settings
  - Access control + custom roles
  - User role management
  - Notification preferences (per user)
  - Language toggle
  - Centre profile (name, logo, branding)
  - Ratio rules (per age band per room)
  - Attendance settings
  - Billing settings
  - Calendar settings
  - Parent portal settings
  - Data retention settings

**Files to read first**: `03-PRD-PHASE1-FULL.md` §9 + §10 + §15.

---

### Sprint 11–12 — Calendar + Forms + Tools (Weeks 23–26)

**Goal**: Full calendar, form builder + admissions, ID card + certificate generators.

**Deliverables**:
- [ ] 3 calendar types: School, Admin, Staff.
- [ ] Specialized views: Staff Schedule, Student Schedule, Staff Leaves, Birthday Calendars (student + staff).
- [ ] Calendar event CRUD: Regular, Holiday, Staff-only.
- [ ] Recurrence support (RFC 5545 RRULE).
- [ ] Room scoping for events.
- [ ] ICS export for parent subscription.
- [ ] Form builder: 5 form types (Enquiry, Admission, Check-in/out, Incident, Custom).
- [ ] Configurable fields (text, textarea, number, date, dropdown, radio, checkbox, file, signature).
- [ ] Form versioning + status (Draft/Live/Archived).
- [ ] Form analytics (submissions, completion rate, drop-off).
- [ ] Multi-step admission checklist.
- [ ] Automated form delivery in admission workflow.
- [ ] ID card generator: 10+ templates, customization, bulk export, barcode.
- [ ] Certificate generator: 10+ templates, customization, bulk export.

**Files to read first**: `03-PRD-PHASE1-FULL.md` §11 + §12 + §13.

---

### Sprint 13–14 — Reports + Notifications Full + SMS + Emergency (Weeks 27–30)

**Goal**: Full reports catalogue, SMS notifications, RTL QA pass.

**Deliverables**:
- [ ] Reports module: all defined reports (attendance monthly, billing, enquiry conversion, app adoption, audit log export, daily sign-in sheet).
- [ ] Async report generation (Hangfire) + status notifications.
- [ ] Excel export (ClosedXML) + PDF export (QuestPDF).
- [ ] Report history (recently generated, 30-day retention).
- [ ] SMS notifications (Critical tier fallback): Twilio or Unifonic integration.
- [ ] Per-tenant SMS quota tracking.
- [ ] Emergency evacuation report: bulk export per room + whole centre + offline cache on mobile.
- [ ] Newsletter builder: rich text editor, AI-assisted content generation, scheduling, analytics.
- [ ] Resource Centre: document library with versioning, per-parent download log.
- [ ] Arabic RTL full regression QA pass on every screen.
- [ ] Performance optimization (load test with k6 at 100 concurrent users).
- [ ] Security hardening pass.

**Files to read first**: `03-PRD-PHASE1-FULL.md` §14 + §16, `15-NOTIFICATIONS.md`.

---

### Sprint 15–16 — Mobile App Full (Weeks 31–34)

**Goal**: Full mobile apps (staff + parent) with offline capabilities + push notifications.

**Deliverables**:
- [ ] Staff mobile app full:
  - Login + biometric unlock
  - Dashboard (live ratio, attendance summary)
  - Attendance check-in/out with camera barcode scan
  - Parent Communication: activity feed, post creation (photo upload)
  - Messaging inbox
  - Push notification handling (all 3 tiers)
  - Emergency report offline cache (SQLite/WatermelonDB, daily refresh)
  - Profile + settings
- [ ] Parent mobile app full:
  - Login + biometric unlock
  - Child activity feed (real-time via push + SignalR)
  - Daily report (PDF + in-app view)
  - Messaging with staff
  - Notifications (all 3 tiers)
  - Profile + settings + notification preferences + language toggle
- [ ] iOS App Store submission + review.
- [ ] Google Play Store submission + review.
- [ ] EAS Build + OTA updates configured.
- [ ] Push notification delivery verification (FCM + APNs).
- [ ] Offline sync engine (WatermelonDB): queued check-ins, queued activity posts.

**Files to read first**: `13-MOBILE-APP.md`.

---

### Sprint 17–18 — QA + Go-Live (Weeks 35–38)

**Goal**: Production-ready Phase 1, first paid customers.

**Deliverables**:
- [ ] Full regression test suite (Playwright E2E + Detox mobile).
- [ ] External penetration test (OWASP Top 10 + business logic).
- [ ] Load test: dashboard < 2s at 500 concurrent users (k6).
- [ ] WCAG 2.1 AA accessibility audit on parent portal.
- [ ] Arabic RTL full regression (native Arabic speaker QA).
- [ ] PDPL/KHDA compliance review (legal counsel sign-off).
- [ ] UAT with pilot nursery staff + new customer UAT.
- [ ] Runbook + incident response documentation.
- [ ] Customer onboarding documentation.
- [ ] Production deployment.
- [ ] Go-live.
- [ ] First paid on-prem customer deployment.
- [ ] First 3 SaaS tenants onboarded.
- [ ] Phase 1 retrospective.

**Exit criteria** (Phase 1 commercial targets, see `01-PRODUCT-VISION.md` §8.2):
- 2 paid on-prem deployments.
- 3 paid SaaS tenants.
- AED 600,000 ARR.
- 5+ centres live.
- NRR ≥ 110%.
- Trial → paid conversion ≥ 25%.
- CSAT ≥ 4.5/5.

**Files to read first**: `16-TESTING-QUALITY.md`, `17-COMPLIANCE-LEGAL.md`, `21-DELIVERY-CHECKLIST.md`.

---

## 4. Phase 2 (Sprints 19–26)

### Sprint 19–22 — EYFS Learning & Assessment (Weeks 39–46)

**Goal**: Full EYFS framework, observations, assessments, learning journeys.

**Deliverables**:
- [ ] Curriculum framework: EYFS (7 development areas, all age bands, all milestones).
- [ ] Lessons module: CRUD with development area, age band, room tagging.
- [ ] Observation records: formal, informal, concern types. Media attachments.
- [ ] AI-assisted observation capture (voice-to-text via OpenAI Whisper, suggested milestones).
- [ ] Assessment templates per age group (Infants, Toddlers, Preschool, FS1).
- [ ] Assessment runs per student per term.
- [ ] Dual report views: Staff (full clinical detail) + Parent (sanitized transformation layer).
- [ ] Parent report approval workflow (staff must approve before parent visibility).
- [ ] Learning Journey: chronological portfolio per student.
- [ ] Reports: Assessment Progress, Assessment Snapshot, Comparison, Class Milestone, Student Milestone.
- [ ] Mobile app: observation capture (photo + voice dictation).

**Files to read first**: `04-PRD-PHASE2-3.md` §2.

---

### Sprint 23–24 — Montessori + Assessment Reports (Weeks 47–50)

**Goal**: Montessori framework, full assessment report suite.

**Deliverables**:
- [ ] Montessori curriculum framework: Practical Life, Sensorial, Language, Mathematics, Cultural.
- [ ] Montessori milestones + observations.
- [ ] Assessment report generation (QuestPDF) for all 5 report types.
- [ ] Parent-view assessment report PDF (sanitized).
- [ ] Learning Journey PDF export (parent-friendly, for school transition).
- [ ] Comparison report (cross-period, cross-student).
- [ ] Class milestone report (room/group aggregate).

---

### Sprint 25–26 — Mobile Maturity + Performance + Service Extraction Decision (Weeks 51–54)

**Goal**: Mobile app full maturity, performance hardening, decide on service extraction.

**Deliverables**:
- [ ] Mobile app: full offline support (attendance queue, activity post queue, messaging queue).
- [ ] Mobile app: Learning Journey view.
- [ ] Mobile app: observation capture with AI dictation.
- [ ] Performance optimization: p99 API < 500ms at 500 concurrent users.
- [ ] Database optimization: index review, materialized view refresh tuning.
- [ ] 99.5% uptime over 3-month measurement window.
- [ ] Decision gate: extract Notification Service? (Extract if queue depth consistently > 1000 or latency > 5s p95).
- [ ] Decision gate: extract Media Processing Service? (Extract if scan latency > 30s p95 or false-positive rate > 5%).
- [ ] If extraction decided: ADR + extraction plan for Phase 3.

**Exit criteria** (Phase 2):
- L&A module live in ≥3 pilot nurseries.
- 80%+ of pilot teachers using digital observations weekly.
- Mobile app 4.5+ star rating on iOS + Google Play.
- p99 API latency < 500ms at 500 concurrent.
- 99.5% uptime over 3-month measurement.

---

## 5. Phase 3 (Sprints 27–30)

### Sprint 27 — Multi-Centre Support (Weeks 55–56)

**Goal**: Multi-Centre Group (MCG) support, cross-centre dashboard.

**Deliverables**:
- [ ] Multi-realm Keycloak configuration.
- [ ] MCG entity + parent_tenant_id hierarchy.
- [ ] Group-level roles (group_owner, group_director, group_it).
- [ ] Cross-centre SSO (login once, access all centres in MCG).
- [ ] Tenant provisioning UI (Group Owner can create new centres).
- [ ] MCG consolidated dashboard (KPIs across all centres).
- [ ] Cross-centre reports (consolidated attendance, revenue, enrolment).
- [ ] Student transfer between centres within MCG (data migration with audit).
- [ ] Staff assignment to multiple centres with different roles per centre.

**Files to read first**: `04-PRD-PHASE2-3.md` §5.

---

### Sprint 28 — Payment Gateway Integration (Weeks 57–58)

**Goal**: Online payment for parents via UAE payment gateway.

**Deliverables**:
- [ ] Payment gateway selection finalized (Telr recommended, PayTabs alternative).
- [ ] Gateway integration: payment session creation, webhook receiver.
- [ ] Parent payment flow: invoice → click "Pay Now" → hosted payment page → success/failure.
- [ ] Webhook signature verification.
- [ ] Idempotent webhook handler.
- [ ] Payment recording (auto from webhook).
- [ ] Refund flow (Centre Admin / Accounts initiated).
- [ ] Saved cards (gateway-side tokenization).
- [ ] Apple Pay / Google Pay support.
- [ ] Reconciliation job (daily, gateway vs. our records).
- [ ] Payment methods management (per parent).
- [ ] Mobile app: invoice view + payment flow (deep link to gateway).

**Files to read first**: `04-PRD-PHASE2-3.md` §6.

---

### Sprint 29 — ERP Integration + Open API (Weeks 59–60)

**Goal**: ERP/accounting sync + third-party Open API.

**Deliverables**:
- [ ] ERP adapter pattern (`IErpAdapter` interface).
- [ ] Xero adapter (if customer requests).
- [ ] QuickBooks Online adapter (if customer requests).
- [ ] Zoho Books adapter (if customer requests).
- [ ] Chart of accounts mapping UI (per tenant).
- [ ] Sync: invoices, payments, expenses, customer records → ERP.
- [ ] Sync: payment status updates ← ERP.
- [ ] Open API: OAuth 2.0 client credentials flow.
- [ ] Open API endpoints: students, attendance, invoices, payments, reports.
- [ ] Webhook publishing (student.created, attendance.checked_in, invoice.paid, etc.).
- [ ] Webhook signing (HMAC-SHA256).
- [ ] Developer portal (docs + sandbox + code samples).
- [ ] Rate limiting per application (1000 req/hour).

**Files to read first**: `04-PRD-PHASE2-3.md` §7 + §8.

---

### Sprint 30 — Scale + Service Extraction + Phase 3 Go-Live (Weeks 61–62)

**Goal**: 20 centres, 500 concurrent users, 99.9% uptime, Phase 3 complete.

**Deliverables**:
- [ ] Elasticsearch (if PostgreSQL FTS insufficient — decision gate).
- [ ] Extract Notification Service (if Phase 2 decision was "yes").
- [ ] Extract Media Processing Service (if Phase 2 decision was "yes").
- [ ] Distributed tracing across extracted services (OpenTelemetry + Tempo).
- [ ] Multi-region DR (AWS eu-central-1 as DR region).
- [ ] 99.9% uptime validation over 3-month measurement window.
- [ ] Load test: 20 centres, 500 concurrent users, p99 < 3s.
- [ ] External penetration test (payment + multi-tenancy + Open API focus).
- [ ] SOC 2 Type II audit initiation.
- [ ] Phase 3 go-live.
- [ ] Phase 3 retrospective.
- [ ] Phase 4 planning (GCC expansion).

**Exit criteria** (Phase 3):
- 1 MCG customer with 5+ centres live.
- 100+ online payments processed.
- 1 ERP integration active.
- 1 third-party Open API integration built.
- 99.9% uptime over 3-month measurement.
- 20 centres, 500 concurrent users, p99 < 3s.

---

## 6. Sprint Ceremonies

### 6.1 Sprint Planning (Day 1 of sprint)

- 2 hours max.
- Product Owner presents sprint goal + stories.
- Team estimates stories (Planning Poker or T-shirt sizing).
- Capacity check (vacations, holidays).
- Stories assigned to developers.

### 6.2 Daily Standup (every day, 15 min)

- What did I do yesterday?
- What will I do today?
- What's blocking me?
- Async option: post in Slack #standup before 10 AM.

### 6.3 Mid-Sprint Review (Day 7)

- 1 hour.
- Demo work-in-progress to Product Owner.
- Adjust scope if needed (add/remove stories).
- Identify blockers.

### 6.4 Sprint Review (Day 10)

- 1 hour.
- Demo completed work to stakeholders.
- Product Owner accepts/rejects stories.
- Feedback captured for next sprint.

### 6.5 Sprint Retrospective (Day 10)

- 1 hour.
- What went well?
- What didn't go well?
- What will we change next sprint?
- Action items with owners.

---

## 7. Estimation Guidelines

| Story size | Effort | Examples |
|---|---|---|
| XS | < 1 day | Bug fix, copy change, small UI tweak |
| S | 1–2 days | New endpoint, new component, small feature |
| M | 3–5 days | New entity + CRUD + tests, complex UI screen |
| L | 1–1.5 weeks | New module (e.g., Tour Management), major feature |
| XL | 2+ weeks | Cross-cutting initiative (e.g., payment gateway, multi-centre) — break into multiple sprints |

Team velocity: ~30-40 story points per sprint (4-6 developers).

---

## 8. Risk Management

### 8.1 Top Risks Per Phase

**MVP risks** (Sprints 0–5):
- Pilot nursery pulls out (mitigation: 2 candidates lined up).
- Arabic RTL breaks in production (mitigation: mandatory RTL QA every sprint).
- Content moderation false positives (mitigation: manual override + appeal flow).
- Parents don't adopt web app (mitigation: magic-link login, daily report email drives first login).
- Pilot scope creep (mitigation: clear MVP scope doc shared with pilot).

**Phase 1 risks** (Sprints 6–18):
- Billing calculation errors (mitigation: extensive unit tests for invoice generation, manual reconciliation in staging).
- Mobile app rejection by App Store (mitigation: follow App Store guidelines, provide demo account, prepare for review feedback).
- Performance under load (mitigation: k6 load tests every sprint from Sprint 11).
- Security vulnerability discovered (mitigation: SAST + SCA + DAST in CI, pen test before go-live).

**Phase 2 risks** (Sprints 19–26):
- EYFS framework complexity (mitigation: domain expert consult, iterative validation with pilot teachers).
- AI observation capture accuracy (mitigation: human review always required, AI is assistive only).
- Mobile app performance with offline sync (mitigation: thorough testing of conflict resolution).

**Phase 3 risks** (Sprints 27–30):
- Payment gateway integration complexity (mitigation: use gateway's hosted page, not inline; start with sandbox early).
- Multi-tenant data leakage at scale (mitigation: rigorous testing of RLS, automated cross-tenant tests in CI).
- ERP integration varies per customer (mitigation: adapter pattern, scope per customer).

### 8.2 Risk Review

Risk register reviewed at sprint planning. New risks added as identified. Risks assigned owners + mitigation plans.

---

**Next file to read**: `20-ADRs.md` — Architecture Decision Records documenting the major technical decisions.
