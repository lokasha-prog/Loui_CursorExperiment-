# 04 — PRD: Phase 2 & Phase 3

> Future scope beyond Phase 1. Read this for forward visibility — do NOT build any of this until Phase 1 is live and stable.
>
> Phase 2 = Sprints 19–26 (~16 weeks). Phase 3 = Sprints 27–30 (~8 weeks).

---

## 1. Phase 2 Scope Summary

Phase 2 adds the **Learning & Assessment module** (the only major functional gap from Phase 1) and brings the **mobile app to full maturity** (offline cache, full notification support, learning journey view, push for all 3 tiers).

### 1.1 Phase 2 Deliverables

| # | Deliverable | Sprint |
|---|---|---|
| 1 | Learning & Assessment module — EYFS framework full implementation | 19–22 |
| 2 | Learning & Assessment module — Montessori framework full implementation | 23–24 |
| 3 | Assessment report generation (staff view + parent view, transformation layer) | 23–24 |
| 4 | Mobile app maturity — offline emergency cache (SQLite) | 25 |
| 5 | Mobile app maturity — full notification support (all 3 tiers, push + in-app + SMS) | 25 |
| 6 | Mobile app maturity — learning journey view (parent + staff) | 26 |
| 7 | Mobile app maturity — push notification handling with FCM + APNs | 26 |
| 8 | Performance optimization under production load (load test, optimize, re-test) | 25–26 |
| 9 | Extract Notification Service if scale justifies (decision gate at Sprint 25) | 26 (optional) |

### 1.2 Phase 2 Exit Criteria

- Learning & Assessment module live in ≥3 pilot nurseries.
- 80%+ of pilot teachers using digital observations weekly.
- Mobile app: 4.5+ star rating on iOS App Store + Google Play.
- p99 API latency < 500ms under 500 concurrent users.
- 99.5% uptime over 3-month measurement window.

---

## 2. Module 14: Learning & Assessment (Phase 2)

### 2.1 Supported Frameworks

- **EYFS** (Early Years Foundation Stage) — UK statutory framework, used in most UAE nurseries.
  - All development areas: CAL, PD, PSED, L, M, UL (Untagged Lessons), Expressive Arts & Design (EAD).
  - All statutory age bands: 8–20, 16–26, 22–36, 30–50, 40–60 months.
  - All milestones per area per age band.
- **Montessori** — full curriculum with areas: Practical Life, Sensorial, Language, Mathematics, Cultural.
- **Extensible** — additional frameworks via configuration (e.g., Reggio Emilia, IB PYP Early Years) added in Phase 3.

### 2.2 Lessons

- Create lessons tagged to: development area, age group, room.
- Fields: Title (i18n), Description (i18n), Development Area, Age Band, Room scoping, Duration, Materials list, Learning objectives, Linked resources (files, links, images).
- Search and filter by room, category, age band, framework.
- Link lessons to circle time activities and observation records.
- Lesson plan templates per age group (Infants, Toddlers, Preschool, FS1, Children's House).

### 2.3 Observation Records

- Create observation records linked to a student and development area.
- Fields: Student, Observer (staff), Date/Time, Type (formal/informal/concern), Development Area, Age Band, Milestones observed (multi-select), Notes (i18n), Media attachments (photos/videos via presigned URL), Concern flag.
- Filter by: Type, Date Range, Student, Room, Development Area. Sort by date or student.
- Concern flag triggers in-app notification to Centre Admin (Operational tier) — escalation workflow.
- Links to: Learning Journey (auto-added), Assessment (manual link if used as evidence).
- AI-assisted observation capture (per parent.app baseline): voice-to-text dictation, suggested milestones based on transcript, photo auto-tagging. (Use OpenAI Whisper + GPT-4 via internal proxy — never expose keys to client.)

### 2.4 Assessments

- Template-based assessments per age group: Infants, Toddlers, Preschool, FS1, Children's House (Montessori).
- Each template defines: development areas covered, milestones per area, scoring approach (binary achieved / not-yet / 3-point scale / narrative).
- Assessment run: per student, per term (or per centre's assessment cadence).
- Filter by: Student, Time Range, Template, Room.
- Dual output: staff view (full clinical detail) + parent view (sanitized transformation).

### 2.5 Learning Journey

- Chronological portfolio per student linking observations, assessments, activities, photos.
- Created by staff; linked to student profile.
- Viewable by parents via parent portal (Phase 1 web) + mobile app (Phase 2).
- Timeline view (chronological) + category view (by development area).
- Exportable as PDF (parent-friendly version) for transitions to school.

### 2.6 Assessment Reports

| Report Type | Description | Audience |
|---|---|---|
| Assessment Progress Report | Individual student progress over time across development areas | Staff + Parent (dual view) |
| Assessment Report | Point-in-time snapshot per student | Staff + Parent (dual view) |
| Comparison Report | Compare student or cohort performance across periods | Staff only |
| Class Milestone Report | Milestone achievement summary across a room or group | Staff only |
| Student Milestone Report | Per-student milestone completion status | Staff + Parent (dual view) |

### 2.7 Report View Variants — Staff vs. Parent

| Variant | Audience | Content | Technical Complexity |
|---|---|---|---|
| **Staff View** | Teachers, Key Persons, Admins | Full clinical detail: all development areas, milestone codes, observation links, concern flags, comparison data, raw scores | High — complex aggregation, cross-entity joins, detailed data grid rendering |
| **Parent View** | Parent / Guardian via parent portal / mobile app | Simplified, plain-language summary: progress highlights, age band placement, positive framing, no clinical codes or internal flags | Medium — requires sanitisation + transformation layer; must not expose internal staff notes or safeguarding flags |

**Implementation:**
- Same underlying data (single storage, no duplication).
- Transformation layer produces parent view from staff view.
- Parent-facing reports reviewed + explicitly approved by staff member before parent visibility.
- Approval workflow: Staff generates → reviews parent variant → approves → parent receives notification.
- Approval logged in audit trail.

### 2.8 EYFS Development Areas Reference

| Code | Development Area |
|---|---|
| UL | Untagged Lessons |
| CAL | Communication and Language |
| PD | Physical Development |
| PSED | Personal, Social and Emotional Development |
| L | Literacy |
| M | Mathematics |
| EAD | Expressive Arts and Design (added in Phase 2 — extends source SRS) |
| UW | Understanding the World (added in Phase 2 — extends source SRS) |

> Note: The source SRS lists only 6 areas. EYFS statutory framework has 7 areas (3 prime + 4 specific). We implement all 7 for compliance.

### 2.9 EYFS Age Bands Reference

| Age Band | Notes |
|---|---|
| 0–11 months | Added for completeness (source SRS starts at 8 months) |
| 8–20 months | Per source SRS |
| 16–26 months | Per source SRS |
| 22–36 months | Per source SRS |
| 30–50 months | Per source SRS |
| 40–60 months | Per source SRS |

> Note: EYFS uses overlapping bands (children move between bands based on development, not strict chronological age). The system must support flexible band assignment per child per area.

---

## 3. Phase 2 Mobile App Maturity

### 3.1 Mobile App Scope (Phase 2 Full)

**Staff mobile app:**
- Full dashboard (live ratio, attendance summary, pending actions).
- Attendance check-in/out with barcode scan (camera-based).
- Parent Communication activity feed (post photos, videos, diary entries).
- Messaging inbox (staff + parent threads, broadcast).
- Notifications (all 3 tiers — push, in-app, SMS for Critical).
- Emergency report offline cache (SQLite local DB, refreshed daily + on-demand).
- Learning & Assessment — observation capture (photo + voice dictation + milestone tagging).
- Learning Journey view.
- Profile + settings.

**Parent mobile app:**
- Child activity feed (real-time via push + SignalR).
- Daily report (PDF + in-app view).
- Messaging with staff.
- Notifications (all 3 tiers).
- Learning Journey view (Phase 2 — when L&A module is live).
- Invoice view + payment (Phase 3 — when payment gateway is integrated).
- Profile + settings + notification preferences + language toggle.

### 3.2 Offline Capabilities (Phase 2)

| Feature | Offline capability |
|---|---|
| Emergency Child Report | Full offline — SQLite cache refreshed daily + on-demand. Critical for evacuation during network outage. |
| Attendance check-in/out | Limited offline — last 24h of student roster cached; check-ins queued locally and synced when back online. Conflict resolution: server timestamp wins. |
| Activity feed viewing | Cached last 7 days of activities for assigned children. New posts queued locally and uploaded when online. |
| Messaging | Read-only offline (cached last 50 messages per thread). New messages queued and sent when online. |
| Learning Journey | Cached last 30 days. |
| Dashboard | No offline — requires real-time data. Show "offline" banner if no connection. |

### 3.3 Push Notification Handling (Phase 2 Full)

- **iOS**: APNs via FCM bridge (Phase 1) → direct APNs (Phase 2 if needed for reliability).
- **Android**: FCM.
- Notification channels: per-tier (Critical, Operational, Informational) + per-module (Attendance, Messaging, Billing, L&A).
- Silent push for data refresh (e.g., new activity posted → silent push → app fetches in background).
- Notification actions: e.g., "Approve" / "Reject" on pending activity notifications (Phase 2).
- Deep linking: tapping a notification opens the relevant screen.

---

## 4. Phase 3 Scope Summary

Phase 3 brings **multi-centre SaaS scale**, **payment gateway**, **ERP/accounting integration**, and **open API**.

### 4.1 Phase 3 Deliverables

| # | Deliverable | Sprint |
|---|---|---|
| 1 | Multi-centre support — multi-realm Keycloak, tenant provisioning UI | 27 |
| 2 | Multi-centre group dashboard — consolidated view across centres | 27 |
| 3 | Payment gateway integration (UAE-capable: Telr / PayTabs / Network International — final selection in Phase 3 kickoff) | 28 |
| 4 | Online payment flow for parents (hosted payment page / inline) | 28 |
| 5 | External accounting ERP API integration (target: Xero / QuickBooks / Zoho Books — final selection with customer) | 29 |
| 6 | Open API for third-party integrations (REST + OAuth 2.0 client credentials) | 29 |
| 7 | Elasticsearch for complex search (if PostgreSQL FTS insufficient at scale) | 30 (conditional) |
| 8 | Extract Notification Service (if Phase 2 load justified) | 30 (conditional) |
| 9 | Extract Media Processing Service (if Phase 2 load justified) | 30 (conditional) |

### 4.2 Phase 3 Exit Criteria

- Multi-centre group: ≥1 customer with 5+ centres live.
- Payment gateway: ≥100 online payments processed.
- ERP integration: ≥1 customer with active sync.
- Open API: ≥1 third-party integration built by external developer.
- 99.9% uptime over 3-month measurement window.
- 20 centres, 500 concurrent users, p99 < 3s.

---

## 5. Phase 3: Multi-Centre Support

### 5.1 Multi-Centre Group Concept

A **Multi-Centre Group** (MCG) is an organization that owns multiple nurseries (e.g., "Bright Futures Nursery Group" with 5 centres across Dubai + Sharjah).

**Hierarchy:**
```
MCG (Multi-Centre Group)
├── Centre 1 (tenant_id: uuid-1)
├── Centre 2 (tenant_id: uuid-2)
├── Centre 3 (tenant_id: uuid-3)
└── ...
```

**Roles at MCG level:**
- **Group Owner** — owns the MCG, billing across all centres, master admin.
- **Group Director** — strategic oversight, consolidated reporting.
- **Group IT** — manages users across centres, integrations.
- **Group Marketing** — cross-centre campaigns.

**Roles at Centre level:** (same as Phase 1) Super Admin, Centre Admin, Teacher, Parent, etc.

### 5.2 Multi-Realm Keycloak

- Phase 1: One realm per deployment (single-centre).
- Phase 3: One realm per MCG (or per centre if MCG opts for isolation).
- Cross-realm SSO: a Group Owner logs in once, can switch between centres without re-auth.
- User can be assigned roles in multiple centres within MCG.

### 5.3 Tenant Provisioning UI

- Self-service tenant creation for Group Owners (within their MCG).
- New centre onboarding wizard: centre name, location, branding, default programs, fee plans, staff import.
- Tenant suspension (group owner can pause a centre temporarily).
- Tenant archival (soft delete — data retained per compliance, access revoked).

### 5.4 Multi-Centre Group Dashboard

- Consolidated KPIs across all centres in MCG: total students, total staff, total revenue, attendance rate average.
- Per-centre comparison: which centres are over/underperforming.
- Drill-down: click a centre → see that centre's dashboard (Phase 1 dashboard).
- Cross-centre reports: consolidated attendance, revenue, enrolment.

### 5.5 Cross-Centre Data Considerations

- **Data isolation**: Each centre's data is strictly isolated via `tenant_id` + RLS. Group Owner can see aggregated/consolidated views but not raw individual records from centres (configurable per MCG policy).
- **Cross-centre students**: When a student transfers between centres within MCG, data is migrated (not duplicated) with full audit trail.
- **Cross-centre staff**: Staff can be assigned to multiple centres with different roles per centre.
- **Consolidated billing**: Group Owner sees one invoice for all centres (SaaS subscription) — separate from per-centre parent billing.

---

## 6. Phase 3: Payment Gateway Integration

### 6.1 Gateway Selection (Phase 3 Kickoff Decision)

UAE-capable gateways to evaluate:

| Gateway | Pros | Cons | Verdict |
|---|---|---|---|
| **Telr** | UAE-focused, multi-currency, easy integration | Smaller ecosystem | Recommended default |
| **PayTabs** | Strong UAE presence, multi-currency, good docs | Higher fees | Alternative |
| **Network International** | Largest UAE acquirer, bank-grade | Heavier integration | Enterprise option |
| **Stripe** | Best DX, but UAE support limited | Limited UAE local payment methods | Not recommended for UAE-primary |
| **Checkout.com** | Strong MENA presence | Enterprise sales cycle | Enterprise option |

**Recommended for Phase 3**: **Telr** as default (easiest integration, UAE-focused) with **PayTabs** as alternative based on customer preference.

### 6.2 Payment Flow

1. Parent receives invoice (via email + in-app notification).
2. Parent clicks "Pay Now" → redirected to hosted payment page (Telr-hosted) OR inline payment modal (if gateway supports).
3. Parent enters card details (never touches NMS servers — gateway tokenizes).
4. Gateway processes payment, returns success/failure to NMS via webhook.
5. NMS updates invoice status to "Paid" (or "Partially Paid" if partial).
6. NMS sends payment receipt to parent via email + in-app notification.
7. NMS records payment in `invoice_payment` table with: amount, method, gateway reference, timestamp.
8. NMS updates billing dashboard.

### 6.3 Payment Features

- Full payment: parent pays full invoice amount.
- Partial payment: parent pays part of invoice; remaining balance stays as "Outstanding".
- Overpayment: parent pays more than invoice → credit balance created, usable against future invoices.
- Refunds: Centre Admin / Accounts role can initiate refund via gateway; recorded in audit log.
- Payment retries: failed payments can be retried by parent.
- Saved cards: gateway-side tokenization (parent opts in to save card for future payments).
- Apple Pay / Google Pay: supported via gateway (Telr supports both).

### 6.4 Reconciliation

- Daily reconciliation job: fetch all gateway transactions for the day, match against `invoice_payment` records.
- Discrepancies flagged for Accounts review.
- Monthly reconciliation report exportable.

### 6.5 Security

- NMS NEVER stores card numbers, CVVs, or sensitive cardholder data.
- All card data handled by gateway — NMS only stores gateway transaction references + tokens (for saved cards).
- PCI DSS compliance: NMS is SAQ-A scope (cardholder data never touches our servers).
- Webhooks signed by gateway; NMS verifies signature before processing.
- Idempotency: webhook handler is idempotent (duplicate webhook doesn't double-credit invoice).

---

## 7. Phase 3: ERP / Accounting Integration

### 7.1 Integration Targets

| Target | Phase 3 status | Notes |
|---|---|---|
| **Xero** | Implement if ≥1 customer requests | Popular with SME nurseries |
| **QuickBooks Online** | Implement if ≥1 customer requests | Popular with accountants |
| **Zoho Books** | Implement if ≥1 customer requests | Popular in UAE SMB |
| **Sage 50 / Sage Intacct** | Defer to Phase 4 unless enterprise customer | Heavier integration |

### 7.2 Integration Scope

- **Push**: NMS → ERP — invoices, payments, expenses, customer (parent) records.
- **Pull**: ERP → NMS — payment status updates (e.g., bank reconciliation marks invoice as paid).
- **Sync frequency**: real-time for invoice/payment creation; hourly for reconciliation; daily for full sync.

### 7.3 Integration Architecture

- Each ERP target is a separate adapter (plugin pattern).
- Adapter implements standard interface: `IErpAdapter` with methods: `PushInvoice`, `PushPayment`, `PushExpense`, `SyncPaymentStatus`.
- Adapter configured per tenant (tenant's chosen ERP + credentials stored in secrets manager).
- Background job (Hangfire / RabbitMQ consumer) processes sync queue.
- Errors logged + retried with exponential backoff; persistent failures surfaced to Accounts role.

### 7.4 Chart of Accounts Mapping

- Each nursery maps NMS fee components to ERP chart of accounts (e.g., Tuition → Account 4000, Meal → Account 4100, Discounts → Account 5000).
- Mapping UI in Settings → Integrations → ERP.
- Per-tenant configuration.

---

## 8. Phase 3: Open API

### 8.1 Open API Scope

- REST API for third-party developers (e.g., parent payroll providers, government reporting tools, custom dashboards).
- OAuth 2.0 client credentials flow (no user context — application-to-application).
- Rate limited: 1000 requests/hour per application per tenant.
- Versioned: `/api/v1/` (same as internal API).
- OpenAPI 3.0 spec published at `api.nms.example.com/openapi.json`.
- Developer portal with docs, code samples, sandbox environment.

### 8.2 Available Endpoints (Phase 3)

| Endpoint | Method | Scope |
|---|---|---|
| `/api/v1/students` | GET, POST, PATCH | students:read, students:write |
| `/api/v1/students/{id}` | GET | students:read |
| `/api/v1/attendance` | GET, POST | attendance:read, attendance:write |
| `/api/v1/invoices` | GET, POST | invoices:read, invoices:write |
| `/api/v1/payments` | GET, POST | payments:read, payments:write |
| `/api/v1/reports/attendance/monthly` | GET | reports:read |
| `/api/v1/reports/billing/summary` | GET | reports:read |

### 8.3 Webhooks

NMS publishes events to registered webhook URLs:
- `student.created`, `student.updated`, `student.withdrawn`
- `attendance.checked_in`, `attendance.checked_out`
- `invoice.created`, `invoice.paid`, `invoice.overdue`
- `payment.received`

Webhooks signed with HMAC-SHA256. Retries with exponential backoff (max 5 attempts over 24h).

---

## 9. Phase 3: Elasticsearch (Conditional)

### 9.1 Trigger to Implement

Implement Elasticsearch only if PostgreSQL full-text search proves insufficient at scale, specifically:
- Search across >1M records takes >500ms p95.
- Complex multi-entity search (e.g., "find all students named Ahmed who started in 2024 in Dubai Marina centre with allergy to peanuts") becomes too slow.

### 9.2 Elasticsearch Scope (if implemented)

- Index: students, staff, activities, messages, enquiries, observations.
- Real-time sync via Postgres logical replication → Logstash → Elasticsearch.
- Search API: `/api/v1/search?q=<query>&types=<types>&filters=...`
- Replaces PostgreSQL FTS in web UI global search.
- Per-tenant index isolation (alias-based or document-level security).

---

## 10. Phase 3: Service Extraction (Conditional)

### 10.1 Notification Service Extraction

**Trigger**: Notification queue depth consistently > 1000 messages, OR notification processing latency > 5s p95, OR Critical notification delivery time > 30s.

**Implementation**:
- Extract Notification module into standalone .NET service.
- Communicates with main app via RabbitMQ events.
- Independently scalable (K8s HPA on queue depth).
- Same database (initially) → extracted database (if write contention).

### 10.2 Media Processing Service Extraction

**Trigger**: Media upload scan latency > 30s p95, OR content moderation false-positive rate > 5%, OR ClamAV CPU usage consistently > 70%.

**Implementation**:
- Extract Media Processing module into standalone .NET service.
- Consumes upload events from RabbitMQ.
- Calls ClamAV + Rekognition.
- Updates `media.scan_status` directly.
- Independently scalable.

---

## 11. Phase 2 & 3 Non-Functional Requirements

| Requirement | Phase 2 | Phase 3 |
|---|---|---|
| Performance | Dashboard p95 < 2s, API p99 < 500ms at 500 concurrent | Dashboard p95 < 2s, API p99 < 500ms at 500 concurrent, 20 centres |
| Availability | 99.5% | 99.9% |
| Scalability | 10 centres, 1000 concurrent users | 20 centres, 500 concurrent users (peak), 10,000 total users |
| Security | + mTLS between services (when extracted) | + Open API security, webhook signing |
| Compliance | + EYFS reporting alignment | + Multi-jurisdiction (GCC expansion readiness) |
| Observability | + Distributed tracing (OpenTelemetry) across services | + Custom dashboards per extracted service |

---

## 12. Post-Phase 3 Roadmap (Indicative Only)

Not committed. For discussion only.

- **Phase 4 (Year 2+)**: GCC expansion (Saudi Arabia, Qatar, Kuwait). Localization for Saudi (Arabic Saudi dialect + VAT 15%). Bahrain, Oman.
- **Phase 5**: AI features — predictive attendance, early intervention recommendations, automated lesson planning.
- **Phase 6**: Marketplace — third-party app integrations (e.g., meal planning, transport tracking, parent payroll).
- **Phase 7**: White-label — sell NMS as white-label to other EdTech providers in MENA.

---

**Next file to read**: `05-TECH-STACK.md` for the concrete .NET 8 + React + React Native stack details.
