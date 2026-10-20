# NMS — Master Brief & File Index

> **Nursery Management System (NMS)** — Pre-requisite documentation pack for vibe-coding the product with **Cursor**.
> Target stack: **.NET 8 + React + React Native (Expo)**. Delivery modes: **On-prem (single-tenant) + SaaS (multi-tenant)**.

---

## 1. What This Pack Is

This is the **single source of truth** that you (the developer / AI coding agent in Cursor) must read before writing any code for the Nursery Management System. It distills two upstream documents — the *NMS Pre-Development Planning v1.1* and the *NMS Solution Requirements v1.2* — into build-ready specifications tuned for an AI-first development workflow.

The pack is **opinionated**: where the upstream docs left technology choices open, this pack commits to a concrete, modern, AI-coding-friendly stack so Cursor can produce working code without endless re-prompting. Every commitment is justified in `20-ADRs.md`.

The pack is **staged**: it defines an MVP pilot first (1–2 nurseries, ~10 weeks), then expands to full Phase 1 (38 weeks), Phase 2 (16 weeks), and Phase 3 (8 weeks). Build MVP, validate, then expand — do not attempt all phases at once.

---

## 2. Project Identity

| Attribute | Value |
|---|---|
| Product name | Nursery Management System (NMS) |
| Domain | Early Childhood Education (ECC) center operations |
| Geographic focus | UAE (primary), GCC expansion (secondary) |
| Languages | English (default) + Arabic (RTL, first-class) |
| Regulatory frame | UAE PDPL (Federal Decree-Law No. 45 of 2021), KHDA (Dubai), ADEK (Abu Dhabi), GDPR-aligned |
| Commercial model | Dual: **On-prem single-tenant license** (sold per nursery org) + **SaaS multi-tenant subscription** (per-center monthly fee) |
| Reference competitor | parent.app — treated as the **minimum acceptable feature baseline**; UX/UI must exceed it |
| Build approach | Modular monolith (Phases 1–2), extraction-ready services (Phase 3) |

---

## 3. Tech Stack at a Glance

| Layer | Technology | Why |
|---|---|---|
| Backend | **.NET 8 (ASP.NET Core Minimal APIs + C#)** | High performance, strong typing, EF Core, native DI, excellent tooling |
| ORM | **EF Core 8** with Code-First Migrations | Productive, supports Postgres-specific features (RLS, JSONB) |
| Database | **PostgreSQL 16** | RLS for multi-tenancy, JSONB for i18n, partitioning, mature ecosystem |
| Cache / Real-time | **Redis 7** (StackExchange.Redis) | Caching, signalR backplane, rate limiting |
| Real-time transport | **ASP.NET Core SignalR** (with Redis backplane) | Native to .NET, auto-negotiate, fallback to long-polling |
| Message queue | **RabbitMQ 3.13** (via MassTransit) | Background jobs, retries, DLQs, sagas for billing |
| Object storage | **S3-compatible** (AWS S3 / MinIO for on-prem) | Media, documents, signed URLs, versioning |
| Identity | **Keycloak 24** (OAuth 2.0 / OIDC) | Multi-realm, RBAC, MFA, social login |
| Web frontend | **React 18 + Vite + TypeScript** | Fast, AI-friendly, huge ecosystem |
| UI library | **shadcn/ui + Tailwind CSS 3 + Radix UI** | Copy-paste components, fully accessible, RTL-ready |
| State | **TanStack Query** (server) + **Zustand** (client) | Pragmatic, no Redux boilerplate |
| Forms | **React Hook Form + Zod** | Type-safe, performant |
| Mobile | **React Native 0.74 + Expo SDK 51 (Bare Workflow)** | iOS + Android from one codebase, native modules when needed |
| Mobile UI | **NativeWind** (Tailwind for RN) + **React Native Paper** | Consistent styling, accessible primitives |
| PDF generation | **QuestPDF** (.NET) + **@react-pdf/renderer** (web) | Fluid C# DSL, no iText licensing headaches |
| Monitoring | **OpenTelemetry → Prometheus + Grafana + Loki** | Vendor-neutral, .NET native support |
| Containerization | **Docker + Kubernetes** | Standardized, reproducible |
| IaC | **Terraform** | Multi-cloud (AWS for SaaS, customer infra for on-prem) |
| CI/CD | **GitHub Actions** + **Helm** | Familiar, ecosystem-rich |

> Full rationale and alternatives considered: see `05-TECH-STACK.md` and `20-ADRs.md`.

---

## 4. File Map

Read files in numbered order. Each file is self-contained but builds on the previous.

| # | File | Purpose | Read first? |
|---|---|---|---|
| 00 | `00-MASTER-BRIEF.md` | This file — index, project identity, how to use the pack | ✅ Always |
| 01 | `01-PRODUCT-VISION.md` | Vision, personas, commercial model, success metrics | ✅ |
| 02 | `02-PRD-MVP.md` | MVP pilot scope, user stories, acceptance criteria | ✅ |
| 03 | `03-PRD-PHASE1-FULL.md` | All 13 Phase 1 modules, full feature spec | ✅ after MVP |
| 04 | `04-PRD-PHASE2-3.md` | Learning/Assessment, payments, ERP, multi-center | When expanding |
| 05 | `05-TECH-STACK.md` | Stack details, versions, alternatives, why | ✅ |
| 06 | `06-ARCHITECTURE.md` | Modular monolith, module boundaries, C4 diagrams | ✅ |
| 07 | `07-DATABASE-SCHEMA.md` | Entities, RLS, partitioning, JSONB i18n, audit log | ✅ |
| 08 | `08-API-SPEC.md` | REST conventions, OpenAPI, cursor pagination, SignalR | ✅ |
| 09 | `09-AUTH-SECURITY.md` | Keycloak realms, RBAC matrix, parent-child ownership | ✅ |
| 10 | `10-MULTI-TENANCY.md` | Tenant_id + RLS, on-prem vs SaaS modes, provisioning | ✅ |
| 11 | `11-DEVOPS-INFRASTRUCTURE.md` | K8s, Terraform, CI/CD stages, env strategy, observability | ✅ |
| 12 | `12-WEB-FRONTEND.md` | React architecture, i18n RTL, shadcn/ui, state, routing | ✅ for FE |
| 13 | `13-MOBILE-APP.md` | React Native + Expo, offline emergency cache, push | ✅ for mobile |
| 14 | `14-UX-UI-DESIGN.md` | Design system, parent.app baseline + elevated patterns | ✅ for FE/mobile |
| 15 | `15-NOTIFICATIONS.md` | 3-tier system, channels, fallback, no-ack policy | When building notifications |
| 16 | `16-TESTING-QUALITY.md` | Unit/integration/E2E, coverage gates, load testing | Before CI setup |
| 17 | `17-COMPLIANCE-LEGAL.md` | UAE PDPL, KHDA/ADEK, data residency, retention | ✅ before go-live |
| 18 | `18-CURSOR-RULES.md` | `.cursorrules` content, project structure, agent workflow | ✅ Always — paste into Cursor |
| 19 | `19-ROADMAP-SPRINTS.md` | Sprint 0-18 breakdown, MVP gating, deliverables | For planning |
| 20 | `20-ADRs.md` | Architecture Decision Records (10 key decisions) | Reference |
| 21 | `21-DELIVERY-CHECKLIST.md` | Go-live readiness, pilot exit criteria, handover | For release |

---

## 5. How to Use This Pack with Cursor

### 5.1 Initial Setup (do this once)

1. **Create the project repo** and open it in Cursor.
2. **Copy `18-CURSOR-RULES.md`** content verbatim into `.cursorrules` at the repo root. This file tells the Cursor agent how to navigate this pack, what conventions to follow, and what not to do.
3. **Copy the entire `nms-prerequisites/` directory** into `/docs/prerequisites/` in the repo. The agent will reference these files by relative path.
4. **Create the directory structure** defined in `18-CURSOR-RULES.md` §3 (monorepo layout with `backend/`, `web/`, `mobile/`, `infra/`, `docs/`).
5. **Bootstrap each app** with the commands in `05-TECH-STACK.md` §11 (scaffolding commands).

### 5.2 Building the MVP (Sprint 0 → Sprint 5)

When prompting Cursor to build the MVP, use this pattern:

```
Read /docs/prerequisites/00-MASTER-BRIEF.md, /docs/prerequisites/02-PRD-MVP.md,
/docs/prerequisites/06-ARCHITECTURE.md, /docs/prerequisites/07-DATABASE-SCHEMA.md,
and /docs/prerequisites/08-API-SPEC.md.

Then implement Sprint 1 deliverables: Centre Management module — Student, Staff, Room,
Program, Group, Label, Guardian entities. Generate EF Core entities, DbContext with
tenant-scoping filter, controllers, DTOs, and unit tests. Follow the conventions in
.cursorrules.
```

Always cite the file paths so the agent loads the right context. Always name the sprint so scope is bounded.

### 5.3 Expanding to Phase 1 / 2 / 3

After MVP pilot sign-off, follow the sprint plan in `19-ROADMAP-SPRINTS.md`. Each sprint has:
- Deliverables list
- Files to read
- Acceptance criteria
- Definition of done

Prompt Cursor one sprint at a time. Do not batch sprints — the agent loses coherence beyond ~2 weeks of scope per prompt.

### 5.4 Handling Cross-Cutting Work

When the work touches multiple files (e.g. "add a new notification type"), prompt Cursor with:
- The specific files to read
- The change to make
- The tests to update
- The ADR to write (if architectural)

See `18-CURSOR-RULES.md` §6 for the standard prompt template.

---

## 6. Reading Order for Different Roles

### 6.1 If you are the **Tech Lead / Architect**
Read everything in order. Pay special attention to `06-ARCHITECTURE.md`, `07-DATABASE-SCHEMA.md`, `09-AUTH-SECURITY.md`, `10-MULTI-TENANCY.md`, `11-DEVOPS-INFRASTRUCTURE.md`, `20-ADRs.md`.

### 6.2 If you are a **Backend Developer**
Read: `00`, `01`, `02` (or `03`), `05`, `06`, `07`, `08`, `09`, `10`, `15`, `16`, `18`. Skip frontend/mobile.

### 6.3 If you are a **Frontend Developer (Web)**
Read: `00`, `01`, `02` (or `03`), `08`, `09`, `12`, `14`, `16`, `18`. Skim `06` and `07` for context.

### 6.4 If you are a **Mobile Developer**
Read: `00`, `01`, `02` (or `03`), `08`, `09`, `13`, `14`, `16`, `18`. Skim `06` for context.

### 6.5 If you are a **DevOps Engineer**
Read: `00`, `05`, `06`, `10`, `11`, `16`, `17`, `18`, `21`. Skim `09` for Keycloak setup.

### 6.6 If you are the **Product Owner / Sponsor**
Read: `00`, `01`, `02`, `19`, `21`. Skim `03` and `04` for forward visibility. Skip technical files unless reviewing a specific decision.

---

## 7. Key Decisions Already Made (So You Don't Re-Litigate Them)

These decisions are **locked** for this build. Changing them after Sprint 1 will cause significant rework. If you disagree, raise it before Sprint 0 ends.

1. **Stack**: .NET 8 + React + React Native (Expo). Not Spring Boot, not Laravel, not Vue. (ADR-001)
2. **Database**: PostgreSQL 16 with RLS-based multi-tenancy. Not schema-per-tenant, not MongoDB. (ADR-002)
3. **Multi-tenancy model**: Shared schema + `tenant_id` column + RLS policies. Same codebase supports both on-prem (single-tenant, tenant_id = the only tenant) and SaaS (multi-tenant). (ADR-003)
4. **Identity**: Keycloak with one realm per deployment in Phase 1, multi-realm in Phase 3. (ADR-004)
5. **Architecture**: Modular monolith for Phases 1–2. Notification and Media Processing modules designed for extraction but not extracted until Phase 3. (ADR-005)
6. **Real-time**: SignalR with Redis backplane. Not raw WebSockets, not SSE-only. (ADR-006)
7. **API style**: REST + OpenAPI 3.0, cursor-based pagination. GraphQL evaluated for Phase 2 mobile only. (ADR-007)
8. **Mobile**: React Native + Expo bare workflow. Native modules only for barcode scanning and offline SQLite. (ADR-008)
9. **Currency**: All financial amounts stored as integer cents (AED fils). Never floating-point. (ADR-009)
10. **i18n**: JSONB columns for translatable DB fields, react-i18next for UI strings. RTL is first-class — non-RTL libraries are rejected. (ADR-010)

Full rationale and alternatives: see `20-ADRs.md`.

---

## 8. What This Pack Does NOT Decide

These remain open and must be resolved with stakeholders before the relevant sprint:

| Open item | Owner | Required by |
|---|---|---|
| UAE cloud provider (AWS ME Central vs Azure UAE North vs local) | Sponsor + IT + Legal | Sprint 0 end |
| Payment gateway (Phase 3 — Telr / PayTabs / Network International / Stripe) | Sponsor + Product | Phase 3 kickoff |
| Data retention periods (proposed: attendance 7y, medical 10y, assessments 5y) | Legal counsel | Before go-live |
| Pilot nursery names + user representatives | Sponsor | Sprint 0 end |
| Production sizing (proposed: 500 students / 50 staff / center, 20 centers, 500 concurrent) | Architect + stakeholders | Sprint 17 |
| Ratio rules per age band (UAE-specific regulatory requirements) | Centre Director + Compliance | Sprint 3 |
| SMS provider for Critical-tier fallback | Product + Ops | Sprint 13 |
| Push notification provider (FCM vs FCM+APNs direct) | Mobile Lead | Sprint 15 |
| ERP/accounting integration target (Phase 3) | Product + Accounts | Phase 3 kickoff |

---

## 9. Document Conventions

- **Must / Should / May** used per RFC 2119.
- Code blocks use the language of implementation (C# for backend, TSX for web, TS for mobile).
- File paths are POSIX-style and relative to repo root unless absolute.
- UUIDs are v4 throughout the data model.
- All monetary amounts are integer AED fils (1 AED = 100 fils) unless explicitly stated.
- Dates are ISO 8601 (`2026-05-17T10:30:00+04:00` for UAE timezone).
- Arabic strings are shown in code as: `{ "en": "Morning Room", "ar": "غرفة الصباح" }`.

---

## 10. Next Action

If you are the developer opening this pack for the first time:

1. Read this file completely.
2. Read `01-PRODUCT-VISION.md` and `02-PRD-MVP.md` completely.
3. Read `18-CURSOR-RULES.md` and paste its contents into `.cursorrules`.
4. Read `05-TECH-STACK.md` and run the scaffolding commands in §11.
5. Read `06-ARCHITECTURE.md` and `07-DATABASE-SCHEMA.md`.
6. Start Sprint 0 per `19-ROADMAP-SPRINTS.md`.

Welcome to the project.
