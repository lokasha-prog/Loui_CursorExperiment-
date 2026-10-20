# 06 — System Architecture

> How the system is structured: modular monolith, module boundaries, deployment topology, real-time flow, data flow.

---

## 1. Architecture Style: Modular Monolith

### 1.1 Why Modular Monolith (not Microservices)

NMS is built as a **modular monolith** for Phases 1–2, with **extraction-ready** Notification and Media Processing modules for Phase 3 if scale justifies.

**Reasoning:**
- **Team size**: Phase 1 team is 4–6 engineers. Microservices require larger teams for the operational overhead.
- **Transactional integrity**: Billing, attendance, and audit need cross-module transactions. Distributed transactions are painful; in-process transactions are easy and safe.
- **Complexity budget**: Microservices add distributed tracing, service mesh, network failure modes, schema versioning across services. We spend that budget later if needed.
- **Extraction-ready**: Two modules (Notification, Media Processing) are designed with explicit interfaces so they can be extracted to separate services without re-architecture. Decision gate at Phase 3 Sprint 30.

**Modular monolith principles we follow:**
1. **Explicit module boundaries** — each module has a public interface (CQRS handlers + events) and a private implementation.
2. **No direct cross-module DB access** — modules access only their own tables. Cross-module data via in-process event bus or query handlers.
3. **Shared kernel** — minimal: `Result<T>`, `Pagination`, `TenantContext`, `ICurrentUser`, base entity.
4. **Module communication** — in-process MediatR (CQRS) for queries/commands; in-process event bus for events. Both abstractions swap to RabbitMQ when extracted.
5. **Module deployment** — single deployable for now. When extracted, the module's project becomes its own service.

### 1.2 Module List (14 modules)

| # | Module | Extraction candidate? |
|---|---|---|
| 1 | Dashboard | No (read-side concern, uses others' data) |
| 2 | Centre Management | No (master data, transactional) |
| 3 | Attendance & Leaves | No (transactional) |
| 4 | Parent Communication | No (transactional) |
| 5 | Messaging | No (transactional) |
| 6 | Billing & Accounting | No (transactional) |
| 7 | Tour Management | No (transactional) |
| 8 | Enquiry Management | No (transactional) |
| 9 | Calendar | No (transactional) |
| 10 | Forms & Admissions | No (transactional) |
| 11 | Tools (ID/Certificates) | No (stateless PDF generation) |
| 12 | Reports & Analytics | Maybe (extract to reporting service in Phase 3 if reporting load is heavy) |
| 13 | Settings & Configuration | No (transactional) |
| 14 | Learning & Assessment (Phase 2) | No (transactional) |
| - | **Notifications** | **YES — extraction-ready** |
| - | **Media Processing** | **YES — extraction-ready** |

---

## 2. C4 Level 1 — System Context

```
                                    ┌─────────────────────┐
                                    │   Parent (mobile)   │
                                    │  React Native app   │
                                    └──────────┬──────────┘
                                               │ HTTPS
                                               │
┌─────────────────────┐               ┌────────▼────────┐
│  Staff (web+mobile) │ ─── HTTPS ───▶│                 │
│  Admin (web)        │ ─── HTTPS ───▶│       NMS       │◀─── HTTPS ──── Third-Party
│  Accounts (web)     │ ─── HTTPS ───▶│   (this system) │                  Apps (Phase 3)
└─────────────────────┘               │                 │
                                      │  .NET 8 API +   │
                                      │  React Web +    │
                                      │  React Native   │
                                      └─┬─────┬─────┬───┘
                                        │     │     │
                              ┌─────────┘     │     └─────────┐
                              │               │               │
                       ┌──────▼─────┐  ┌──────▼─────┐  ┌──────▼─────┐
                       │ PostgreSQL │  │  Redis     │  │ RabbitMQ   │
                       │   (RLS)    │  │ (cache+    │  │ (queue +   │
                       │            │  │  backplane)│  │  events)   │
                       └────────────┘  └────────────┘  └────────────┘

                       ┌────────────┐  ┌────────────┐  ┌────────────┐
                       │ Keycloak   │  │  S3 /      │  │  CDN       │
                       │ (identity) │  │  MinIO     │  │ (signed    │
                       │            │  │ (storage)  │  │  URLs)     │
                       └────────────┘  └────────────┘  └────────────┘

                       ┌────────────────────────────────────────────┐
                       │ External Services                          │
                       │ - FCM (push)                               │
                       │ - SES / Azure Comm (email)                 │
                       │ - Twilio / Unifonic (SMS, Phase 1 Sprint   │
                       │   13+)                                     │
                       │ - AWS Rekognition / Azure Content Safety   │
                       │   (content moderation)                     │
                       │ - ClamAV (malware scan)                    │
                       │ - OpenAI (AI features, Phase 2+ newsletter │
                       │   generation + observation dictation)      │
                       └────────────────────────────────────────────┘
```

## 3. C4 Level 2 — Container Diagram

```
                    ┌─────────────────────────────────────────────────────┐
                    │                   NMS System                        │
                    │                                                     │
   Browser ────────▶│  ┌──────────────┐  ┌──────────────────────────┐    │
   (Admin/Staff)    │  │  NMS Web     │  │  NMS API (.NET 8)        │    │
                    │  │  (React+Vite)│  │  ┌─────────────────────┐ │    │
                    │  │              │  │  │  Modular Monolith   │ │    │
                    │  │  - SSR: none │  │  │  - 14 modules       │ │    │
                    │  │  - SPA       │  │  │  - CQRS (MediatR)   │ │    │
                    │  │              │  │  │  - Event bus        │ │    │
                    │  └──────┬───────┘  │  └─────────────────────┘ │    │
                    │         │          │                           │    │
                    │         │ HTTPS    │  Hangfire (background     │    │
                    │         │          │  jobs, in-process)        │    │
                    │         ▼          │                           │    │
                    │  ┌──────────────┐  │  SignalR Hub (real-time)  │    │
                    │  │  Keycloak    │  │                           │    │
                    │  │  (OIDC)      │  └─┬─────┬─────┬─────┬───────┘    │
                    │  └──────────────┘    │     │     │     │            │
                    │                      ▼     ▼     ▼     ▼            │
                    │              ┌────────┐ ┌────┐ ┌─────┐ ┌─────┐      │
                    │              │Postgre │ │Redi│ │Rabb │ │ S3  │      │
                    │              │SQL 16  │ │s 7 │ │itMQ │ │     │      │
                    │              │(RLS)   │ │    │ │     │ │     │      │
                    │              └────────┘ └────┘ └─────┘ └─────┘      │
                    └─────────────────────────────────────────────────────┘
                                            │
                                            │ async (events)
                                            ▼
                                    ┌───────────────────┐
                                    │ External Services │
                                    │ FCM, SES, etc.    │
                                    └───────────────────┘
```

### 3.1 Mobile Container

```
   Mobile App (React Native + Expo)
   ├── Staff app
   │   ├── Login (Keycloak via expo-auth-session)
   │   ├── Dashboard (SignalR real-time)
   │   ├── Attendance (camera barcode)
   │   ├── Activities (photo upload via presigned S3 URL)
   │   ├── Messaging (SignalR)
   │   ├── Notifications (FCM)
   │   └── Emergency Cache (SQLite/WatermelonDB)
   └── Parent app
       ├── Feed (SignalR real-time)
       ├── Messaging
       ├── Daily Report
       ├── Notifications
       └── Profile + Settings
```

The mobile app talks to the same NMS API (REST + SignalR) as the web. No separate mobile API in Phase 1–2. GraphQL evaluated for Phase 2 mobile if REST payload size becomes a problem on slow mobile networks.

---

## 4. Module Boundaries (C4 Level 3 — Component View)

Each module exposes:
- **Commands** (write-side, via MediatR `IRequest`):
  - Example: `CreateStudentCommand`, `CheckInStudentCommand`, `GenerateInvoiceCommand`
- **Queries** (read-side, via MediatR `IRequest<TResult>`):
  - Example: `GetStudentByIdQuery`, `GetLiveRatiosQuery`, `GetDailyReportQuery`
- **Events** (published to in-process bus, consumed by other modules):
  - Example: `StudentCreatedEvent`, `StudentCheckedInEvent`, `InvoicePaidEvent`
- **DTOs** (public contracts):
  - Example: `StudentDto`, `AttendanceDto`, `InvoiceDto`

Modules NEVER:
- Call another module's repository directly.
- Read another module's DB tables directly.
- Import another module's internal classes (only DTOs + events + commands/queries).

Modules CAN:
- Dispatch another module's commands/queries via MediatR.
- Subscribe to another module's events.
- Share DTOs from `NMS.Shared`.

### 4.1 Module: Centre Management

**Responsibilities:**
- CRUD: Students, Staff, Rooms, Programs, Groups, Labels, Guardians, Email Templates.
- Student lifecycle state machine.
- Guardian ↔ Student relationships (primary/secondary).
- Staff ↔ Room assignments (time-slot aware).

**Public contracts:**
- Commands: `CreateStudent`, `UpdateStudent`, `TransitionStudentStatus`, `CreateStaff`, `AssignStaffToRoom`, `CreateRoom`, etc.
- Queries: `GetStudentById`, `GetStudentsByRoom`, `GetStaffByRole`, etc.
- Events: `StudentCreated`, `StudentStatusChanged`, `StaffCreated`, `RoomCreated`, etc.

**Consumes events from:**
- (none — master data, doesn't react to others)

**Publishes events consumed by:**
- Attendance (StudentCreated → student becomes available in attendance)
- Parent Communication (StudentCreated → parent account provisioning)
- Messaging (StudentCreated → parent-teacher thread creation)
- Billing (StudentCreated → fee plan association check)
- Calendar (StudentCreated → birthday calendar entry)

### 4.2 Module: Attendance & Leaves

**Responsibilities:**
- Student + staff check-in/out (barcode + manual).
- Leave requests + approvals.
- Real-time ratio calculation.
- Attendance forecasting.
- Sign-in sheet export.

**Public contracts:**
- Commands: `CheckInStudent`, `CheckOutStudent`, `MarkStudentAbsent`, `RequestLeave`, `ApproveLeave`, `RejectLeave`
- Queries: `GetAttendanceForDate`, `GetLiveRatios`, `GetMonthlyAttendanceReport`, `GetAttendanceForecast`
- Events: `StudentCheckedIn`, `StudentCheckedOut`, `StaffCheckedIn`, `StaffCheckedOut`, `RatioBreached`, `RatioRecovered`, `LeaveRequested`, `LeaveApproved`

**Consumes events from:**
- Centre Management (StudentCreated → add to attendance roster; StaffCreated → add to staff attendance)
- Calendar (HolidayEvent → mark all students absent automatically)

**Publishes events consumed by:**
- Dashboard (all events → live KPI updates)
- Notifications (RatioBreached → Critical notification)
- Parent Communication (StudentCheckedOut → trigger daily report generation at end of day)
- Billing (LeaveApproved → prorate invoice if applicable)

### 4.3 Module: Parent Communication

**Responsibilities:**
- Activity feed (8 types: announcement, photo, video, diary, food, toilet, medical, mood).
- Staff approval workflow before parent visibility.
- Daily consolidated report generation.
- Notes & concerns.
- Newsletter builder + AI content generation.
- Resource Centre (document library with versioning).

**Public contracts:**
- Commands: `CreateActivityPost`, `ApproveActivityPost`, `RejectActivityPost`, `UploadMedia`, `GenerateDailyReport`, `CreateNewsletter`, `ScheduleNewsletter`, `PublishNewsletter`
- Queries: `GetActivityFeed`, `GetDailyReport`, `GetNewsletters`, `GetResourceCentreDocuments`
- Events: `ActivityPostCreated`, `ActivityPostApproved`, `ActivityPostRejected`, `MediaUploaded`, `MediaScanCompleted`, `DailyReportGenerated`, `NewsletterPublished`

**Consumes events from:**
- Centre Management (StudentCreated → parent account provisioning)
- Attendance (StudentCheckedOut → trigger daily report)
- Media Processing (MediaScanCompleted → update activity post approval_status)

**Publishes events consumed by:**
- Notifications (ActivityPostApproved → notify parents; DailyReportGenerated → notify parents)
- Messaging (parent replies to activity comment → new message thread)
- Reports & Analytics (daily activity counts → analytics)

### 4.4 Module: Messaging

**Responsibilities:**
- Staff ↔ Parent shared inbox.
- Staff ↔ Staff internal messaging.
- Broadcast announcements.
- Threaded conversations with read receipts.

**Public contracts:**
- Commands: `SendMessage`, `MarkThreadRead`, `CreateBroadcastAnnouncement`, `ArchiveThread`
- Queries: `GetInbox`, `GetThread`, `GetUnreadCount`
- Events: `MessageSent`, `MessageRead`, `ThreadCreated`

**Consumes events from:**
- Centre Management (StudentCreated → create parent-staff thread)

**Publishes events consumed by:**
- Notifications (MessageSent → notify recipient)
- Dashboard (MessageSent → unread count widget)

### 4.5 Module: Billing & Accounting

**Responsibilities:**
- Fee plan management linked to programs.
- Auto invoice generation on schedule (Hangfire recurring job).
- Invoice lifecycle: Draft → Issued → Partially Paid → Paid → Overdue → Cancelled.
- Payment recording (manual in Phase 1; gateway in Phase 3).
- Automated fee reminders (email + push).
- Expense recording.
- Revenue dashboard.

**Public contracts:**
- Commands: `CreateFeePlan`, `UpdateFeePlan`, `GenerateInvoicesForMonth`, `RecordPayment`, `RecordExpense`, `CancelInvoice`, `SendReminder`
- Queries: `GetBillingDashboard`, `GetInvoices`, `GetInvoiceById`, `GetFeePlans`, `GetExpenses`, `GetOverdueInvoices`
- Events: `InvoiceCreated`, `InvoicePaid`, `InvoiceOverdue`, `PaymentRecorded`, `ExpenseRecorded`, `FeePlanCreated`

**Consumes events from:**
- Centre Management (StudentCreated → check fee plan assignment; StudentWithdrawn → final invoice)
- Attendance (LeaveApproved → prorate if applicable)

**Publishes events consumed by:**
- Notifications (InvoiceCreated → notify parent; InvoiceOverdue → reminder notification)
- Dashboard (InvoiceCreated, PaymentRecorded → billing snapshot widget)
- Reports & Analytics (revenue data)

### 4.6 Module: Notifications (Extraction-Ready)

**Responsibilities:**
- Route notification events to correct tier + channel.
- Critical: in-app banner + push + SMS (Sprint 13+).
- Operational: in-app + push.
- Informational: email + optional in-app.
- Retry with exponential backoff.
- Channel fallback (push fail → SMS → email).
- Delivery status tracking (no recipient ack required).
- Dead-letter queue for un-deliverable notifications.

**Public contracts:**
- Commands: `SendNotification` (called by other modules)
- Queries: `GetUserNotifications`, `GetNotificationDeliveryStatus`
- Events: `NotificationSent`, `NotificationDelivered`, `NotificationFailed`

**Extraction-ready design:**
- All communication via RabbitMQ events (not direct MediatR call) — when extracted, same event contracts work cross-service.
- Owns `notification` + `notification_delivery` tables.
- Has its own connection pool to external services (FCM, SES, Twilio).
- Stateless (no in-memory state needed) — easy to scale horizontally.

**Consumes events from:**
- All modules (any event that should trigger a notification)

**Publishes events consumed by:**
- Dashboard (NotificationSent → update notification indicator)

### 4.7 Module: Media Processing (Extraction-Ready)

**Responsibilities:**
- Receive media upload events (from Parent Communication, Forms, Learning).
- Trigger ClamAV scan.
- Trigger content moderation (AWS Rekognition or Azure Content Safety).
- Update `media.scan_status` (PENDING → APPROVED / QUARANTINED).
- Move quarantined files to quarantine bucket.
- Notify uploader of scan outcome.

**Public contracts:**
- Commands: `TriggerMediaScan` (internal)
- Events: `MediaScanCompleted` (with scan_status + reasons)

**Extraction-ready design:**
- All triggers via RabbitMQ events.
- Owns scan_result data.
- Can be extracted without touching other modules.

**Consumes events from:**
- Parent Communication (`MediaUploaded` → trigger scan)
- Forms (`FormSubmitted` with attachment → trigger scan)
- Learning (`ObservationCreated` with media → trigger scan)

**Publishes events consumed by:**
- Parent Communication (`MediaScanCompleted` → update activity post approval_status)
- Forms (`MediaScanCompleted` → update form submission status)
- Learning (`MediaScanCompleted` → update observation status)

### 4.8 Module: Dashboard

**Responsibilities:**
- Read-side aggregation of KPIs across modules.
- Live ratio widget (subscribes to Attendance events).
- Pending actions widget (subscribes to Messaging, Parent Communication, Billing events).
- Billing snapshot widget (subscribes to Billing events).
- Enquiry pipeline widget (subscribes to Enquiry events).

**Implementation:**
- Read-side only — no commands of its own.
- Uses Materialized Views in PostgreSQL for expensive aggregations, refreshed via RabbitMQ-triggered background job.
- Real-time updates via SignalR Hub pushing to connected clients.

### 4.9 Module: Enquiry Management

**Responsibilities:**
- 6-stage CRM pipeline: No Stage → Interested → Contacted → Tour Appointment → Toured → Enrolled.
- Stage history log (append-only).
- Source tracking.
- Convert to Student on enrolment.

**Public contracts:**
- Commands: `CreateEnquiry`, `UpdateEnquiryStage`, `AssignEnquiryToStaff`, `ConvertEnquiryToStudent`, `ArchiveEnquiry`
- Queries: `GetEnquiriesByStage`, `GetEnquiryById`, `GetEnquiryConversionRate`
- Events: `EnquiryCreated`, `EnquiryStageChanged`, `EnquiryConvertedToStudent`, `EnquiryArchived`

### 4.10 Module: Tour Management

**Responsibilities:**
- Tour appointment scheduling linked to enquiries.
- Staff assignment.
- Block-off management.
- Calendar + list views.

**Public contracts:**
- Commands: `CreateTourAppointment`, `UpdateTourAppointment`, `CancelTourAppointment`, `MarkTourCompleted`, `MarkTourNoShow`, `CreateBlockOff`
- Queries: `GetTourAppointments`, `GetToursByDate`, `GetStaffAvailability`
- Events: `TourScheduled`, `TourCompleted`, `TourCancelled`, `TourNoShow`

### 4.11 Module: Calendar

**Responsibilities:**
- 3 calendar types: School, Admin, Staff (per Loui Okasha comment).
- 4 specialized views: Staff Schedule, Student Schedule, Staff Leaves, Birthday Calendars.
- Event types: Regular, Holiday, Staff-only.
- Recurrence handling.
- Room scoping.
- ICS export.

**Public contracts:**
- Commands: `CreateCalendarEvent`, `UpdateCalendarEvent`, `DeleteCalendarEvent`, `ImportHolidays`
- Queries: `GetSchoolCalendar`, `GetAdminCalendar`, `GetStaffCalendar`, `GetUpcomingBirthdays`
- Events: `CalendarEventCreated`, `CalendarEventUpdated`, `CalendarEventDeleted`, `HolidayImported`

### 4.12 Module: Forms & Admissions

**Responsibilities:**
- Form builder (5 types: Enquiry, Admission, Check-in/out, Incident, Custom).
- Form versioning.
- Form analytics (submission count, completion rate, avg time, drop-off).
- Multi-step admission checklist.
- Automated form delivery in admission workflow.

**Public contracts:**
- Commands: `CreateForm`, `UpdateForm`, `PublishForm`, `SubmitForm`, `CreateAdmissionProcess`, `UpdateAdmissionStep`
- Queries: `GetForms`, `GetFormSubmissions`, `GetFormAnalytics`, `GetAdmissionProcessStatus`
- Events: `FormPublished`, `FormSubmitted`, `AdmissionStepCompleted`

### 4.13 Module: Tools (ID/Certificates)

**Responsibilities:**
- ID card generation (10+ templates, bulk export, barcode).
- Certificate generation (10+ templates, bulk export).
- Stateless PDF generation via QuestPDF.

**Public contracts:**
- Commands: `GenerateIdCards`, `GenerateCertificates`
- Queries: `GetIdCardTemplates`, `GetCertificateTemplates`
- Events: (none — pure command/query, no state changes to publish)

### 4.14 Module: Reports & Analytics

**Responsibilities:**
- All defined reports (attendance, billing, enquiry conversion, app adoption, observation summary, audit log export).
- Async report generation (Hangfire job, status updates via SignalR).
- Excel + PDF export.
- Analytics dashboard with charts.
- Recently generated report history.

**Public contracts:**
- Commands: `GenerateReport`, `ScheduleReport`
- Queries: `GetReportHistory`, `GetReportDownloadUrl`
- Events: `ReportGenerated`, `ReportFailed`

### 4.15 Module: Settings & Configuration

**Responsibilities:**
- All settings areas (programme settings, access control, user roles, notification prefs, language, integrations, centre profile, ratio rules, attendance settings, billing settings, calendar settings, parent portal settings, data retention).
- Custom role management.
- Audit Log viewer (Super Admin).
- Multi-tenant safe.

**Public contracts:**
- Commands: `UpdateSetting`, `CreateCustomRole`, `UpdateRolePermissions`, `UpdateNotificationPreferences`
- Queries: `GetSettings`, `GetAuditLog`, `GetRoles`, `GetPermissions`
- Events: `SettingUpdated`, `RoleCreated`, `RoleUpdated`

### 4.16 Module: Learning & Assessment (Phase 2)

**Responsibilities:**
- EYFS + Montessori framework management.
- Lessons, observations, assessments, learning journeys.
- Dual report views (staff + parent) with transformation layer.
- AI-assisted observation capture (Phase 2).

**Public contracts:**
- Commands: `CreateObservation`, `CreateAssessment`, `GenerateAssessmentReport`, `ApproveParentReport`
- Queries: `GetLearningJourney`, `GetObservations`, `GetAssessments`, `GetMilestonesByAgeBand`
- Events: `ObservationCreated`, `AssessmentCompleted`, `ParentReportApproved`

---

## 5. Real-Time Architecture (SignalR)

### 5.1 SignalR Hub Design

Single hub: `/hubs/tenant` — all real-time events for a tenant flow through this hub.

**Groups** (per-tenant isolation):
- `tenant-{tenantId}` — all users in the tenant (admins see everything)
- `tenant-{tenantId}-role-{role}` — role-specific events (e.g., push to all teachers)
- `tenant-{tenantId}-user-{userId}` — user-specific events (e.g., new direct message)
- `tenant-{tenantId}-room-{roomId}` — room-specific events (e.g., ratio change in this room)

**Methods (server → client):**
- `AttendanceUpdated` — check-in/out event
- `RatioUpdated` — ratio change for a room
- `NotificationReceived` — new notification for user
- `MessageReceived` — new message in a thread
- `ActivityPosted` — new activity (parent receives)
- `DashboardRefreshed` — KPI update
- `BillingUpdated` — invoice/payment event

### 5.2 Connection Lifecycle

- Client connects with JWT access token in query string (SignalR requirement).
- On connect: server validates token via Keycloak, extracts `tenant_id` + `user_id` + `roles`, adds connection to appropriate groups.
- On disconnect: server removes connection from groups.
- Token expiry: client receives `OnClose` event, refreshes token via refresh token flow, reconnects with new token.
- Backplane: Redis (StackExchange.Redis) — required for multi-pod scaling. All pods subscribe to Redis pub/sub for cross-pod message delivery.

### 5.3 Fallback

- SignalR auto-negotiates transport: WebSocket → Server-Sent Events → Long-polling.
- Long-polling fallback is acceptable for parents on flaky mobile networks.
- Mobile app uses `@microsoft/signalr` client with automatic reconnect (exponential backoff, max 5 retries).

---

## 6. Data Flow Examples

### 6.1 Student Check-In (Barcode Scan)

```
1. Teacher scans barcode at door (USB HID scanner sends code + Enter)
2. Web app captures input, calls POST /api/v1/attendance/check-in
3. API:
   a. Validates JWT (Keycloak), extracts tenant_id + user
   b. Dispatches CheckInStudentCommand via MediatR
   c. Attendance module:
      - Looks up student by barcode (within tenant scope — RLS enforced)
      - Creates attendance_record (check_in_at = now)
      - Logs to attendance_event (immutable)
      - Publishes StudentCheckedInEvent
   d. Returns 200 OK with attendance DTO
4. In-process event bus publishes StudentCheckedInEvent:
   a. Dashboard module consumes → updates materialized view → pushes via SignalR
   b. Notification module (if Critical alert needed) → queues notification
   c. Billing module (if applicable) → proration event
5. SignalR hub pushes AttendanceUpdated to:
   - tenant-{id}-role-centre_admin (dashboard)
   - tenant-{id}-room-{roomId} (room view)
6. Client UIs update in real-time
```

### 6.2 Photo Activity Post (with Content Moderation)

```
1. Teacher selects photos in web app, writes caption, clicks "Post"
2. Web app:
   a. Requests presigned upload URLs from API: POST /api/v1/media/presign
   b. API returns array of presigned URLs (one per file, 60s expiry)
   c. Web app uploads files directly to S3 (bypassing API)
3. Web app calls POST /api/v1/activities with body containing:
   - S3 keys (not URLs) of uploaded files
   - Caption, student IDs, type=Photo
4. API:
   a. Creates activity_post (approval_status = PENDING)
   b. Creates media records (scan_status = PENDING)
   c. Publishes ActivityPostCreated event + MediaUploaded events
5. RabbitMQ consumer (Media Processing module):
   a. Receives MediaUploaded event
   b. Downloads file from S3
   c. Runs ClamAV scan
   d. Runs AWS Rekognition (or Azure Content Safety) for inappropriate content
   e. Updates media.scan_status (APPROVED or QUARANTINED)
   f. If QUARANTINED: moves file to quarantine bucket
   g. Publishes MediaScanCompleted event
6. Parent Communication module consumes MediaScanCompleted:
   a. If APPROVED: activity_post.approval_status → APPROVED → visible to parents
   b. If QUARANTINED: activity_post.approval_status → REJECTED → notify teacher + admin
   c. Publishes ActivityPostApproved or ActivityPostRejected event
7. Notification module consumes ActivityPostApproved:
   a. Sends push notification to linked parents
   b. Records delivery status
```

### 6.3 Invoice Generation (Monthly)

```
1. Hangfire recurring job triggers at 1 AM on 1st of each month (tenant timezone)
2. Job dispatches GenerateInvoicesForMonth command for each active tenant
3. Billing module:
   a. For each Active student with a FeePlan:
      - Generate invoice with line items from FeePlan.FeeComponents
      - Apply discounts
      - Apply tax (VAT 5%)
      - Set due_date = issue_date + 14 days (configurable per tenant)
      - Status = Issued
      - Save invoice
      - Publish InvoiceCreated event
4. Notification module consumes InvoiceCreated:
   a. Sends email to parent (using email template) with PDF attachment
   b. Sends push notification (Informational tier)
5. Hangfire job at 8 AM: SendReminder for invoices due in 3 days
   - Sends reminder email using configured template
6. Hangfire job daily at 9 AM: Mark overdue invoices
   - Invoices past due_date + grace period → status = Overdue
   - Publishes InvoiceOverdue event
7. Notification module consumes InvoiceOverdue:
   a. Sends overdue reminder (email + push) per configured schedule
```

---

## 7. Multi-Deployment Architecture (On-Prem vs SaaS)

### 7.1 Same Codebase, Different Configuration

NMS is built as a single codebase that supports both on-prem (single-tenant) and SaaS (multi-tenant) deployments via configuration:

| Aspect | On-Prem | SaaS |
|---|---|---|
| Tenants | 1 (the customer's nursery) | Many (one per centre) |
| `tenant_id` column | Present (always same value) | Present (varies per row) |
| RLS policies | Active (defense-in-depth) | Active (critical for isolation) |
| Keycloak realm | 1 realm (customer-managed) | 1 realm per centre (Phase 1), multi-realm per MCG (Phase 3) |
| Database | Customer-managed Postgres | Our managed RDS Postgres |
| Object storage | Customer's MinIO or S3 | Our AWS S3 |
| Domain | Customer's subdomain (e.g., `nursery.example.com`) | `app.nms.ae` (per-tenant subdomain: `tenant.nms.ae`) |
| Email | Customer's SMTP or our SES (relay) | Our SES |
| Push | Our FCM project (shared) or customer's FCM | Our FCM project |
| Updates | Customer-controlled | Continuous |
| Branding | Customer's branding | Default NMS branding (custom branding as add-on) |

### 7.2 Configuration Switches

```jsonc
// appsettings.json (on-prem example)
{
  "Deployment": {
    "Mode": "OnPrem",
    "TenantId": "single-tenant-uuid-fixed",
    "SingleTenantMode": true
  },
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "..." // from secrets
  },
  "Storage": {
    "Provider": "MinIO", // or "S3"
    "Endpoint": "https://minio.customer.internal:9000"
  },
  "Identity": {
    "Authority": "https://keycloak.customer.internal:8443/realms/nursery",
    "ClientId": "nms",
    "MultiRealm": false
  }
}
```

```jsonc
// appsettings.json (SaaS example)
{
  "Deployment": {
    "Mode": "SaaS",
    "MultiTenantMode": true
  },
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "..." // from AWS Secrets Manager
  },
  "Storage": {
    "Provider": "S3",
    "Endpoint": "https://s3.me-central-1.amazonaws.com"
  },
  "Identity": {
    "Authority": "https://keycloak.nms.ae/realms/{tenant-realm}",
    "ClientId": "nms",
    "MultiRealm": true
  }
}
```

### 7.3 Tenant Resolution (SaaS)

On SaaS, the API resolves `tenant_id` from:
1. JWT `tenant_id` claim (set by Keycloak based on user's realm/role mappings).
2. Subdomain (e.g., `brightfutures.nms.ae` → tenant lookup by subdomain → tenant_id).
3. Header `X-Tenant-Id` (for API-only access, where subdomain isn't available).

Defense-in-depth:
- JWT claim is source of truth.
- Subdomain only used for login page routing + initial tenant identification.
- RLS at DB level rejects any query where `tenant_id` doesn't match the JWT claim's `tenant_id`.

Full multi-tenancy details: `10-MULTI-TENANCY.md`.

---

## 8. Performance Architecture

### 8.1 Caching Strategy

| Cache scope | TTL | Invalidation |
|---|---|---|
| User profile + permissions | 15 min | On permission change (event-driven) |
| Room list (per tenant) | 1 hour | On room CRUD |
| Student list (per room) | 5 min | On student CRUD |
| Dashboard KPIs | 30 sec (materialized view) | RabbitMQ-triggered refresh on attendance event |
| Live ratio | Real-time (computed) | N/A — always computed fresh on attendance event |
| Email templates | 1 hour | On template CRUD |
| Localization strings | 24 hour | On translation file deploy |

### 8.2 Query Optimization

- All list endpoints use cursor-based pagination (no OFFSET).
- Composite indexes on `(tenant_id, created_at DESC)` and `(tenant_id, status)` for high-traffic entities.
- Partial indexes: `WHERE is_deleted = false` for soft-deleted entities.
- Partitioning: `attendance_record` and `audit_log` partitioned by month.
- Materialized views for dashboard aggregations.
- Read replica for reporting queries (isolated from transactional writes).

### 8.3 Connection Pooling

- PgBouncer in transaction mode between .NET and PostgreSQL.
- Default pool: 100 connections, configurable per env.
- EF Core `DbContextPool` (20 default) — short-lived contexts per request.

### 8.4 Background Job Scaling

- Hangfire on RabbitMQ backend (not SQL Server — we're on PostgreSQL).
- Worker count: 4 per pod (Phase 1), configurable.
- Queue priorities: `critical` (notifications), `default` (reports), `low` (cleanup tasks).
- Job deduplication via Hangfire's built-in dedup + our own idempotency keys.

### 8.5 Asynchronous Processing

- All non-critical path work goes async via RabbitMQ:
  - Report generation
  - Email sending
  - Push notification sending
  - Content moderation
  - PDF generation (bulk)
- API returns 202 Accepted for async work, client polls status or receives SignalR update.

---

## 9. Availability & Disaster Recovery

### 9.1 Availability Tiers

| Phase | Uptime SLA | Failure budget |
|---|---|---|
| Phase 1 | 99.5% | 3.6 hours/year downtime |
| Phase 2 | 99.5% | 3.6 hours/year |
| Phase 3 | 99.9% | 8.76 hours/year |

### 9.2 High Availability Design

- Multi-AZ deployment (AWS me-central-1 has 3 AZs).
- K8s cluster: 3+ worker nodes, auto-scaling 3–10.
- API: 3+ replicas, HPA on CPU + custom metrics.
- PostgreSQL: Multi-AZ (primary + standby), automatic failover.
- Redis: Primary + replica, automatic failover.
- RabbitMQ: 3-node cluster, quorum queues.
- Keycloak: 3+ replicas, shared PostgreSQL backend.

### 9.3 Backup & Recovery

| Data | Backup frequency | RPO | RTO |
|---|---|---|---|
| PostgreSQL | Daily snapshot + WAL archiving (5 min) | 5 min | 1 hour |
| Object storage (S3) | Continuous (cross-region replication) | 0 | 15 min |
| Redis | Not backed up (cache only) | N/A | N/A |
| RabbitMQ | Not backed up (transient) | N/A | N/A |
| Keycloak DB | Daily snapshot + WAL | 5 min | 1 hour |
| Configuration (Terraform state) | Version-controlled in git + S3 backend | 0 | 15 min |

### 9.4 Disaster Recovery Plan

- **RTO**: 8 hours (SRS mandate).
- **RPO**: 4 hours (SRS mandate) — we achieve 5 min via WAL archiving.
- **DR region**: Phase 3 — secondary region (AWS eu-central-1 or similar) for multi-region DR.
- **DR test**: Quarterly DR drill — restore from backup to DR env, verify critical flows.
- **Runbook**: `infra/docs/disaster-recovery.md` with step-by-step restore procedures.

---

## 10. Architectural Quality Attributes

| Attribute | Target | How achieved |
|---|---|---|
| Performance | Dashboard p95 < 2s, API p99 < 500ms | Caching, materialized views, cursor pagination, read replica, CDN |
| Scalability | 500 students / 50 staff / centre, 20 centres Phase 3, 500 concurrent | Multi-tenant RLS, horizontal pod autoscaling, partitioning |
| Availability | 99.5% (P1) / 99.9% (P3) | Multi-AZ, auto-failover, health checks, synthetic monitoring |
| Security | Zero critical findings, PDPL-compliant | Keycloak + RBAC + RLS + encryption + audit log + pen test |
| Maintainability | 80% test coverage, < 5 cyclomatic complexity avg | Modular monolith, CQRS, unit + integration tests |
| Observability | p99 latency dashboards, alerting | OpenTelemetry + Prometheus + Grafana + Loki |
| Deployability | Zero-downtime deploys, < 15 min rollback | Blue/green, Helm, CI/CD gates |
| Localizability | Full EN + AR RTL parity | i18next + JSONB + logical CSS properties + RTL QA gate |
| Accessibility | WCAG 2.1 AA parent portal | shadcn/ui + Radix (accessible primitives) + axe-core in CI |

---

**Next file to read**: `07-DATABASE-SCHEMA.md` for entities, RLS policies, partitioning, and JSONB i18n patterns.
