# 03 — PRD: Phase 1 Full Scope (All 13 Modules)

> Full Phase 1 functional specification. Read this AFTER the MVP is piloted and you're expanding to the complete Phase 1 product.
>
> MVP modules (Centre Management, Attendance, Parent Communication, Messaging, Dashboard, Audit) are detailed in `02-PRD-MVP.md`. This file extends them with full Phase 1 features and adds the remaining 7 modules.

---

## 1. Phase 1 Module Map

| # | Module | Status | Primary Spec |
|---|---|---|---|
| 1 | Dashboard | MVP baseline + Phase 1 extensions | §3 below |
| 2 | Centre Management | MVP baseline + Phase 1 extensions | §4 below |
| 3 | Attendance & Leaves | MVP baseline + Phase 1 extensions | §5 below |
| 4 | Parent Communication | MVP baseline + Phase 1 extensions | §6 below |
| 5 | Messaging | MVP baseline + Phase 1 extensions | §7 below |
| 6 | Billing & Accounting | **Phase 1 only** | §8 |
| 7 | Tour Management | **Phase 1 only** | §9 |
| 8 | Enquiry Management | **Phase 1 only** | §10 |
| 9 | Calendar | **Phase 1 only** | §11 |
| 10 | Forms & Admissions | **Phase 1 only** | §12 |
| 11 | Tools (ID/Certificates) | **Phase 1 only** | §13 |
| 12 | Reports & Analytics | **Phase 1 only** | §14 |
| 13 | Settings & Configuration | MVP baseline + Phase 1 extensions | §15 |
| 14 | Learning & Assessment | **Phase 2** | See `04-PRD-PHASE2-3.md` |

## 2. Cross-Module Conventions

These apply to every module:

1. **i18n**: All user-facing strings via `react-i18next` (web) and `i18next` (mobile). All translatable DB fields stored as JSONB `{"en": "...", "ar": "..."}`. Default to English if locale missing.
2. **RTL**: Arabic locale sets `dir="rtl"` on `<html>`. CSS uses logical properties (`margin-inline-start`, never `margin-left`). Every screen QA'd in both LTR and RTL.
3. **Audit**: Every CREATE/UPDATE/DELETE writes to append-only `audit_log` with before/after JSONB diff.
4. **Multi-tenancy**: Every entity has `tenant_id`. EF Core global query filter ensures tenant isolation. PostgreSQL RLS is defense-in-depth.
5. **Soft delete**: `is_deleted` + `deleted_at` on Student, Staff, Invoice, Activity, Message. Hard delete only on transient data (sessions, temp uploads).
6. **Pagination**: Cursor-based (`?cursor=<base64>&limit=50`). Max page size 100. Never return unbounded lists.
7. **Permissions**: Every endpoint annotated with `[Authorize(Policy = "...")]`. Every UI element conditionally rendered based on permissions.
8. **Real-time**: SignalR pushes updates for: dashboard KPIs, ratio monitor, messaging, activity feed (for parents), attendance summary.
9. **File uploads**: All binary uploads via presigned S3 URLs. App server never receives multipart form data for binaries.
10. **Error responses**: Consistent envelope `{ success: false, errors: [{code, message, field?}] }`. HTTP status code matches error class.

## 3. Module 1: Dashboard (Phase 1 Extensions over MVP)

### 3.1 Additional KPI Cards (over MVP)
- Total Programs (in addition to Rooms, Staff, Students)
- Staff on leave today
- Students absent for 2+ consecutive days (alert card with student list)
- Today's revenue (Phase 1 Sprint 7+ — when billing is live)
- Overdue invoice count (Phase 1 Sprint 7+)
- New enquiries this week (Phase 1 Sprint 9+)
- Pending tour appointments (Phase 1 Sprint 9+)
- App installation adoption rate (Phase 1 Sprint 13+ — staff and parent)

### 3.2 Widgets Extension

| Widget | Phase 1 additions |
|---|---|
| Attendance Table | Filter by Room/Group/Program/Date; student status filter (Active, Inactive, On Hold, Withdrawn, Signed Up, Transferred); summary counters Total/Pending/Checked In/Checked Out/Absent for both staff and students |
| Live Ratio Monitor | Room-level ratio with compliance status (warning if breach); historical breach log accessible |
| Pending Actions | Add: pending invoice approvals (Sprint 7+), pending tour confirmations (Sprint 9+), pending admissions checklist items (Sprint 11+) |
| Billing snapshot | New widget (Sprint 7+): Total billed / Collected / Overdue / Outstanding this month, with revenue trend sparkline |
| Enquiry pipeline | New widget (Sprint 9+): Mini pipeline view showing count per stage |

### 3.3 Dashboard Personalization

- Each role sees a default layout (Centre Admin: full; Teacher: room-scoped; Parent: child-scoped; Accounts: billing-focused).
- Centre Admins can pin/reorder widgets within their own dashboard.
- Saved layouts stored per user.

### 3.4 WebSocket-Driven Updates (Phase 1 Full)

Every dashboard widget subscribes to specific SignalR channels:
- `tenant-{id}-attendance` — check-in/out events → attendance table + KPIs
- `tenant-{id}-ratio` — ratio recalculation → live ratio monitor
- `tenant-{id}-notifications` — new notifications → pending actions
- `tenant-{id}-billing` — invoice/payment events → billing snapshot (Sprint 7+)
- `tenant-{id}-enquiries` — enquiry stage changes → enquiry pipeline (Sprint 9+)

Client reconnects with exponential backoff. On reconnect, fetches full state for changed widgets.

## 4. Module 2: Centre Management (Phase 1 Extensions)

### 4.1 Student Lifecycle (Full 6 Statuses)

```
Signed Up → Active → On Hold → Withdrawn
                ↓                  ↑
            Inactive → Transferred
```

Each transition is logged with: previous status, new status, reason (free text or configurable enum), acting user, timestamp.

| Status | Meaning | Visible in attendance? | Parent access? |
|---|---|---|---|
| Signed Up | Registered but not yet started | No | No |
| Active | Currently enrolled and attending | Yes | Yes |
| On Hold | Temporarily not attending (travel, illness) | No (but retained in roster) | Read-only |
| Inactive | Long-term pause (e.g., summer break for part-year programs) | No | Read-only |
| Withdrawn | Permanently left the nursery | No (soft-deleted from active views) | No |
| Transferred | Moved to another room or centre within same org | No (in old room) / Yes (in new room) | Yes (linked to new room) |

### 4.2 Email Template Management

- CRUD for templates used by system communications (billing reminders, daily report, newsletter, enquiry auto-reply, etc.).
- Template fields: name, type (enum), subject (i18n), body (rich text, i18n), reply-to, attachments.
- Variables: `{{student.first_name}}`, `{{parent.first_name}}`, `{{invoice.amount}}`, `{{invoice.due_date}}`, etc. — full variable list in `08-API-SPEC.md` §11.
- Preview mode with sample data.
- Versioned — changes tracked, previous version restorable.

### 4.3 Labels & Tagging

- Custom tags applied to students or staff for cross-module filtering.
- Examples: "Allergy: Peanuts", "Sibling Discount", "VIP Family", "New Starter".
- Color-coded in UI.
- Used in: attendance filter, billing report filter, broadcast messaging target, calendar filter.

## 5. Module 3: Attendance & Leaves (Phase 1 Extensions)

### 5.1 Monthly Attendance Report

- Monthly view per student: Days Present, Days Absent, Days on Leave, Attendance Percentage.
- Export to Excel (Apache POI equivalent — EPPlus or ClosedXML in .NET).
- Filter by Room, Group, Program, Date Range.
- Includes staff attendance variant.

### 5.2 Leave Management

**Student Leave:**
- Create: Student, Leave Type (configurable — Sick, Travel, Family, Other), Date range, Notes.
- Filter: Month, Room, Student, Status.
- Workflow: Submitted → Approved / Rejected (Centre Admin approves).
- Approved leave auto-marks student absent for the date range (no manual check-in needed).

**Staff Leave:**
- Same structure as student leave.
- Approved leave affects ratio calculation — staff on leave are excluded from ratio numerator.
- Approved leave visible in Calendar module (Staff Leaves view).
- Leave history per staff member.

### 5.3 Attendance Forecast

- Forward-looking view by date.
- Splits expected attendance by Program.
- Columns: Program | Expected Students | Checked In | Checked Out.
- "Expected" = Enrolled − Approved Leave.
- Requires verification of pickup individual's identity (configurable — Centre Admin can mark specific students as requiring ID check at pickup).

### 5.4 Barcode Scanner Support (Full)

- USB HID scanners (keyboard emulation) — works in web app via input field auto-focus.
- Bluetooth HID scanners — same as USB.
- Mobile camera scanning (web app, in mobile browser) via `BarcodeDetector` API or `quagga2` library fallback.
- Each student has a unique barcode (generated on registration, printable on ID card).
- Each staff has a unique barcode.
- `attendance_event` table logs every scan (success or failure) for audit.

### 5.5 Sign-in Sheet Export

- PDF / Excel export of daily attendance with signature lines for parents.
- Configurable: include photo column, include pickup-time column, include signature column.

## 6. Module 4: Parent Communication (Phase 1 Extensions)

### 6.1 Newsletter Builder (Phase 1 Sprint 5–6)

- Rich-text editor (TipTap) with i18n content (parallel EN/AR editing).
- AI-assisted content generation (OpenAI API or local LLM) — "Generate newsletter draft from this week's activities" button.
- Recipient targeting: all parents, by room, by program, by group, by label.
- Scheduling: send now or schedule for future date/time.
- Status: Draft → Scheduled → Published → Archived.
- Analytics: delivery count, open rate (tracking pixel), click rate (per link).
- Filter by Title, Created By, Status.

### 6.2 Resource Centre (Phase 1 Sprint 5–6)

- Document library for sharing files with parents or staff.
- Folder organization with permissions (parent-facing vs. staff-only).
- File types: PDF, DOCX, XLSX, images, videos.
- Document versioning (SRS Open Question #13 — decided: full version history).
  - `document_version` table: version number, upload timestamp, uploader identity, per-parent download log.
  - Object storage versioning enabled.
- Access control: per-folder, per-role.
- Search by name, tag, content (full-text search via PostgreSQL `tsvector` in Phase 1; Elasticsearch in Phase 3 if needed).

### 6.3 Notes & Concerns (Phase 1)

- Separate filtered views for informal notes and escalated concerns.
- Notes: day-to-day observations, not parent-facing by default.
- Concerns: flagged for Centre Admin attention (safeguarding, behavioral, medical).
- Filter by month, room, student.
- Concerns trigger an in-app notification to Centre Admin (Operational tier).

## 7. Module 5: Messaging (Phase 1 Extensions)

### 7.1 Group Messaging & Broadcast

- Staff can broadcast announcements to: all parents, parents by room, parents by program, parents by group, parents by label.
- Broadcast messages are read-only for parents (no reply) — distinct from 1:1 threads.
- Staff can send group messages to selected staff (e.g., "All staff in Toddler Room").
- Staff Announcements: broadcast to all staff, scoped by date (today or calendar-based scheduling).

### 7.2 Messaging UX Enhancements

- Search across messages (full-text search via PostgreSQL FTS in Phase 1).
- Star/pin important threads.
- Attachment support (photos, documents) via presigned URL upload.
- Typing indicators (SignalR).
- Read receipts per recipient — "Seen by Mariam at 10:32 AM, Ahmed at 10:35 AM".
- Both parents of a child shown as linked contacts in staff's view (so staff know who they're talking to).

### 7.3 Auto-Translation Consideration

- Phase 1: NOT in scope (parent.app has 198-language auto-translation as a feature; we deliver EN + AR natively, which covers UAE market).
- Phase 2: Evaluate adding auto-translation for staff/parent messages when language mismatch detected.

## 8. Module 6: Billing & Accounting (Phase 1 Sprint 7–8)

### 8.1 Billing Dashboard

| Metric | Description |
|---|---|
| Total Billed Amount | Sum of all issued invoices in selected period |
| Total Collected Amount | Payments received in selected period |
| Overdue Amount | Outstanding on past-due invoices |
| Outstanding Amount | Unpaid on current invoices |
| Revenue Stats | Graphical revenue trend over configurable date range (monthly/quarterly/yearly) |
| New Invoices Today | Count and amount of invoices created today |
| Recent Payments | Last 10 payments received |
| Top Overdue Parents | Top 5 by outstanding amount |

### 8.2 Fee Plans

- Create and manage fee plans linked to programs.
- Plan attributes: Name (i18n), Status (Active/Inactive), Period (monthly/term/annual), Linked Program, Labels.
- Fee Components (sub-entity): Description, Amount (integer AED fils), Type (Tuition, Meal, Transport, Activity, Discount, Tax), Tax Rate (optional).
- Discounts: Percentage or fixed amount, applicable to specific components or total.
- Tax Rates: Configurable per component (UAE VAT 5% standard, but flexibility for exemptions).
- Associate multiple students with a single plan (e.g., siblings).
- Proration logic: mid-month enrolment auto-prorates based on configurable rule (daily / weekly / monthly-charge-full).

### 8.3 Invoice Management

- Auto-generate invoices from fee plans on configurable schedule (e.g., 1st of each month for monthly plans; start of term for term plans).
- Manual invoice creation (Centre Admin / Accounts role).
- Invoice fields: Number (auto-generated, sequential per tenant with prefix), Student, Fee Plan, Issue Date, Due Date, Line Items (fee components), Subtotal, Discounts, Tax, Total, Status, Notes.
- Status: Draft → Issued → Partially Paid → Paid → Overdue → Cancelled.
- Partial payment tracking via `invoice_payment` child table.
- Overpayments create `credit_balance` records (usable against future invoices).
- All amounts in integer AED fils.
- PDF generation via QuestPDF — branded template, bilingual (EN/AR side-by-side or sequential based on parent preference).

### 8.4 Automated Fee Reminders

- Configurable email reminder templates (using Email Template Management from §4.2).
- Trigger options: X days before invoice due date OR X days after invoice issue date OR X days after overdue.
- Selectable lead times: 1, 2, 3, 5, 7 days.
- Push notification option in addition to email.
- Template editor: Title, Reply-to, Subject, Rich-text body with variable substitution.
- Reminder log: every reminder sent is recorded with delivery status.
- Unsubscribe: parents can opt out of informational reminders (not overdue notices).

### 8.5 Expenses

- Record and categorize centre expenses.
- Fields: Date, Amount, Category (Rent, Utilities, Salaries, Supplies, Food, Other), Description, Receipt (optional file upload), Recorded By.
- Integrated into billing dashboard for net financial view (Revenue − Expenses = Net).
- Monthly expense report export.

### 8.6 Payment Link (Phase 1 minimal)

> Added per Loui Okasha comment on source SRS: "Add: Accounts and Payment link."

- Phase 1: Generate a payment link (Stripe Payment Link-style) on each invoice — parents click → land on a hosted payment page → enter card details → payment recorded.
- Phase 1 payment gateway: deferred to Phase 3 full integration, but Phase 1 supports manual payment recording (Accounts role records: amount, method (cash/cheque/transfer), reference, date).
- Phase 3: Full payment gateway integration (Telr / PayTabs / Network International) with online card payment.

## 9. Module 7: Tour Management (Phase 1 Sprint 9–10)

### 9.1 Tour Appointment Features

- Create and manage tour appointments linked to enquiry records.
- Calendar view: Month and List display modes.
- Filter by: Calendar (room/tour type), Staff, Month.
- Block-off management: staff can mark time slots as unavailable (leave, lunch, other tours).
- Print appointment schedule (PDF per day/week).
- Staff assignment per appointment.
- Tour confirmation: automated email/SMS (Phase 1: email only) to family on booking + reminder 24h before.
- Post-tour follow-up task creation: auto-create a task for assigned staff to follow up within 48h.

### 9.2 Tour Appointment Fields

- Family name, contact details (linked to enquiry record).
- Preferred date/time (with staff availability check).
- Assigned staff.
- Tour type (General, Specific Program Focus, Birthday Party Booking).
- Number of adults, number of children visiting.
- Notes (special requests, questions to address).
- Status: Scheduled → Completed → Cancelled → No-show.
- Outcome: linked to enquiry stage update (Completed → Enquiry moves to "Toured").

## 10. Module 8: Enquiry Management (Phase 1 Sprint 9–10)

### 10.1 Pipeline Stages (6 stages)

| Stage | Description |
|---|---|
| No Stage | Initial enquiry received, not yet categorised |
| Interested | Family has expressed interest; initial contact made |
| Contacted | Staff have reached out to the family |
| Tour Appointment | Tour has been scheduled |
| Toured | Family has visited the centre |
| Enrolled | Admission confirmed; transition to active student record |

### 10.2 Enquiry Record Fields

- Family / child name, contact details (phone, email, preferred contact method).
- Child DOB, desired start date, desired program.
- Enquiry source (configurable enum: Walk-in, Website, Referral, Social Media, Phone, Email, Existing Parent).
- Assigned staff member.
- Enquiry date and last contact date.
- Stage and status.
- Notes and activity log (every contact attempt, conversation summary).
- Stage history log (`enquiry_stage_log` table — append-only, every stage change with timestamp, user, reason).

### 10.3 Filters & Actions

- Filter by: Stage, Date Range (Today / This Month / Last Month / This Year / Custom), Source, Staff.
- Actions: Add Enquiry, Archive Enquiry, Convert to Admission.
- Convert to Admission: creates a Student record (status=Signed Up), links back to enquiry for analytics, sends admission workflow kickoff.

### 10.4 Online Self-Service Enquiry Form

- Public-facing form (no auth required) embedded on nursery's website or hosted at `enquiry.<nursery-domain>.com`.
- Fields configurable via Forms & Admissions module.
- On submission: creates enquiry record at stage "No Stage", sends auto-acknowledgement email to family, notifies assigned staff.
- Anti-spam: reCAPTCHA v3, rate limiting (5 submissions per IP per hour).

### 10.5 Enquiry Conversion Analytics

- Conversion rate by source.
- Conversion rate by staff.
- Average time in each stage.
- Drop-off points (which stage has highest "no progression" rate).
- Report: Enquiry Conversion Rate Report (in Reports & Analytics module).

## 11. Module 9: Calendar (Phase 1 Sprint 11–12)

> Per Loui Okasha comment: "3 Calendars: School, Admin and teachers"

### 11.1 Calendar Views (3 separate calendars + 4 specialized views)

| View | Scope | Key Features |
|---|---|---|
| **School Calendar** | Centre-wide events and holidays | Event types: Regular Event, Holiday, Staff-only Event. Month/List views. Filter by category, room, time range. Room-scoping for events. |
| **Admin Calendar** | Management view of all schedules | Aggregated view: staff schedules, student programs, leaves, tours, events, birthdays. Configurable layers. |
| **Staff Calendar (per teacher)** | Personal schedule | My shifts, my tasks, my leave, my students' programs, my assigned tours, my birthdays to acknowledge. |
| Staff Schedule | All staff or by room | Shift times, assignments, leave blocks |
| Student Schedule | All students or by room/program | Program days, special events |
| Staff Leaves | Leave calendar for all staff | Approved and pending leaves colour-coded |
| Birthday Calendars | Students and Staff (separate) | Upcoming birthdays, filter by room/status |

### 11.2 School Calendar Events

- Event types: Regular Event, Holiday, Staff-only Event.
- Fields: Title (i18n), Description (i18n), Start/End (date or datetime), Type, Room scoping (optional — "All Rooms" or specific rooms), Created By, Visibility (Public to parents / Staff only).
- Month and list display modes.
- Recurrence: daily, weekly, monthly, custom (e.g., "Every Tuesday and Thursday").
- ICS export for parents to subscribe to nursery calendar.
- Holiday events affect attendance forecast (no expected attendance on holidays).

### 11.3 Calendar Integration

- Phase 1: ICS export only (parents subscribe via Google Calendar / Apple Calendar / Outlook).
- Phase 3: Two-way Google Calendar / Microsoft 365 sync for staff.

## 12. Module 10: Forms & Admissions (Phase 1 Sprint 11–12)

### 12.1 Form Builder

- Configurable form fields: text, textarea, number, date, dropdown, radio, checkbox, file upload, signature (canvas-based), section header, conditional logic.
- Field types support i18n labels and options.
- Form types: Enquiry Form, Admission Form, Check-in/Check-out Form, Incident Form, Custom.
- Status: Live / Draft.
- Form versioning — changes tracked, published version is what users see.
- Embeddable on external website (public forms like Enquiry) or in-app (internal forms like Incident).

### 12.2 Form Analytics

- Per form: Submission count, Completion rate (started vs. submitted), Average completion time, Drop-off point (which field has highest abandonment).
- Filter by date range.

### 12.3 Admissions Process

- Define a multi-step admission checklist/process per centre.
- Process steps configurable: e.g., "Submit admission form", "Pay registration fee", "Submit medical records", "Sign contract", "Schedule trial day", "Confirm enrolment".
- Track each enrolled student through process steps.
- Status dashboard: Process Name | Status | Due Date | Total Students.
- Automate form delivery: when student enters a step, the relevant form is automatically sent to parent.
- Notifications to parent + assigned staff when step is pending or overdue.

### 12.4 Form Submission Storage

- All submissions stored as JSONB in `form_submission` table.
- Submissions linked to student or enquiry.
- File uploads via presigned URLs to object storage.
- PII fields encrypted at column level.

## 13. Module 11: Tools — ID Card & Certificate Generators (Phase 1 Sprint 11–12)

### 13.1 ID Card Generator

- Minimum 10 pre-designed template options (5 student, 5 staff variants).
- Template customization: logo upload, color scheme (primary, secondary, accent), field selection (name, photo, DOB, room, program, barcode, expiry).
- Scope: Students or Staff.
- Bulk generation: select multiple students/staff → generate all cards as PDF (one per page, ready to print).
- Print-ready export: PDF, A6 size (105 × 148 mm) standard, configurable.
- Barcode generation (Code 128 or QR code) on each card for attendance scanning.

### 13.2 Certificate Generator

- Minimum 10 pre-designed certificate templates.
- Customizable: title, recipient name, date, signature fields (multiple signatures supported), logo, color scheme.
- Scope: Students.
- Bulk generation: select multiple students → generate all certificates as PDF.
- PDF export, A4 landscape standard, configurable.
- Certificate numbering (sequential per template) for authenticity tracking.

## 14. Module 12: Reports & Analytics (Phase 1 Sprint 13–14)

### 14.1 Available Reports

| Report | Scope | Format | Filters |
|---|---|---|---|
| Monthly Attendance Report (Students) | Per-student monthly attendance summary | Excel + PDF | Room, Group, Program, Month |
| Monthly Attendance Report (Staff) | Per-staff monthly attendance summary | Excel + PDF | Room, Role, Month |
| Billing & Revenue Report | Invoices, payments, overdue breakdown | Excel | Date range, Status, Room |
| Enquiry Conversion Rate Report | Pipeline conversion by source, staff, stage | Excel + PDF | Date range, Source, Staff |
| Parent App Installation Report | Adoption tracking — % parents who logged in this month | Excel | Date range, Room |
| Staff App Installation Report | Adoption tracking — % staff who logged in this month | Excel | Date range, Role |
| Observation & Assessment Summary (Phase 2) | Per-student observation count, assessment completion | Excel + PDF | Date range, Room, Framework |
| Emergency Child Report | Per-child emergency info + bulk per room/centre | PDF (one-click) | Room, Date |
| Audit Log Export (Super Admin) | Filtered audit log | Excel + JSON | User, Date range, Entity, Action |
| Daily Sign-in Sheet | Printable daily attendance with signature lines | PDF | Room, Date |

### 14.2 Report Requirements

- All reports support Excel download (and PDF where visual format matters).
- Filters: Date range, Room, Program, Group, Staff (depending on report).
- Recently generated reports list with timestamp and generated-by user (last 30 days).
- Reports generated asynchronously (RabbitMQ job) — user notified when ready.
- Long-running reports don't block UI — show "Generating..." status with progress.
- Reports stored in object storage for 30 days, then auto-deleted.
- Multi-tenant safe — strictly scoped to `tenant_id`.

### 14.3 Analytics Dashboard (Phase 1)

- Visual KPIs over time: daily attendance rate trend, revenue trend, enquiry conversion trend.
- Charts: line, bar, pie, donut.
- Date range selector.
- Export chart as PNG.

## 15. Module 13: Settings & Configuration (Phase 1 Extensions)

### 15.1 Settings Areas

| Setting Area | Description | Phase 1 scope |
|---|---|---|
| Programme Settings | Configure which activity types and features are active per programme | Full |
| Access Control | Define and manage user roles; assign permissions at module level | Full |
| User Role Management | Create custom roles; set read/write/admin access per section | Full |
| Notification Settings | Configure push and email notification preferences per notification tier | Full |
| Language | Multi-language support (UI language selection per user) | EN + AR only |
| Integrations | External system API configurations | Email only in Phase 1; payment + ERP in Phase 3 |
| Audit Log Viewer | Filterable audit log for Super Admins | Full |
| Centre Profile | Centre name (i18n), logo, branding colors, address, contact, regulatory info | Full |
| Ratio Rules | Configure staff-to-student ratio per age band per room | Full |
| Attendance Settings | Barcode enabled, grace period, auto-absent time, supervisor override threshold | Full |
| Billing Settings | Default invoice due days, reminder schedule, currency, tax rate | Full |
| Calendar Settings | Weekend days (Fri-Sat UAE default), working hours, holiday calendar import | Full |
| Parent Portal Settings | Daily report time, allowed activity types, photo watermark, branding | Full |
| Data Retention | Per-entity retention periods (attendance, medical, assessment) — configurable within legal minimums | Full |

### 15.2 Custom Roles

- Super Admins can create custom roles beyond the standard 8 (Super Admin, Centre Admin, Teacher, Parent, Receptionist, Accounts, IT, Marketing, Procurement).
- Custom role: name, description, permissions per module (read / write / admin / none).
- Permissions enforced at API layer via `[Authorize(Policy = "...")]` + at UI layer via conditional rendering.

### 15.3 Notification Preferences Per User

- Each user can configure per-notification-type preferences (push on/off, email on/off).
- Critical tier notifications cannot be suppressed by users (safety alerts are mandatory).
- Operational tier: user can suppress push but not in-app.
- Informational tier: user can suppress both push and email (unsubscribe).

## 16. Cross-Cutting Requirements (Full Phase 1)

### 16.1 Audit Trail — UI Accessibility

All data mutations (CREATE, UPDATE, DELETE) logged with: acting user, timestamp, entity type, entity ID, action, before/after JSONB diff.

**Two access levels:**
- Backend/DB level: complete log always persisted, accessible to platform ops team for compliance/forensics. Baseline requirement.
- Admin UI level: filterable Audit Log viewer in Settings module, accessible to Super Admins. Filter by: user, date range, entity type, action.

**Implementation:**
- `audit_log` table — append-only, enforced via PostgreSQL trigger rejecting UPDATE/DELETE.
- Partitioned by month for query performance.
- Indexed on `(tenant_id, created_at DESC)`, `(tenant_id, user_id)`, `(tenant_id, entity_type, entity_id)`.

### 16.2 Media Storage & Security (Full)

- All media (photos, videos, documents) stored in S3-compatible object storage (NEVER in DB).
- CDN serves all media to parents — UAE edge PoPs required.
- Content moderation: every upload scanned (file type, size, ClamAV malware, optional AWS Rekognition for inappropriate content) BEFORE parent visibility.
- Pending or rejected media is quarantined at CDN layer — even if a signed URL is generated, CDN returns 403.
- Signed/expiring URLs only (5-minute default expiry for media, 60-second for upload presigned URLs).
- Direct public links to storage buckets NOT permitted — bucket policy blocks all public access.
- Media retention policy aligned with data retention (Section 5 of SRS).

### 16.3 Emergency Child Report (One-Click Evacuation Export)

Per child, instantly generatable single-page PDF containing:
- Child's full name, DOB, profile photo.
- Room and assigned key person.
- Medical conditions, allergies, dietary requirements (encrypted at rest, decrypted for this report only with audit log).
- Medication currently in use and administration notes.
- Emergency contact names, relationships, phone numbers (minimum 2 contacts).
- Any active safeguarding or custody notes flagged as relevant to emergency response.

**Access:**
- From Student profile: "Export Emergency Report" button.
- From Room view: "Export All for Room" button → bulk PDF (zipped or merged).
- From Dashboard: "Export All for Centre" button → bulk PDF (most critical for evacuation).
- Cached on device (mobile app, Phase 1 Sprint 15+) for offline access during network outage.
- Access requires Teacher+ role; access is logged at higher audit level (security-sensitive).

### 16.4 Notification Hierarchy (Full Phase 1)

| Tier | Examples | Required Channels | Suppressible? | Acknowledgement |
|---|---|---|---|---|
| **Critical / Safety** | Ratio breach, unexpected absence, safeguarding flag | In-app banner (red, persistent) + Push + SMS (Phase 1 Sprint 13+) | No — mandatory | Not required (per planning doc §7), but UX "Acknowledge" button records user-side dismissal |
| **Operational** | Leave approved/rejected, invoice generated, message received, activity pending approval | In-app notification + Push | Per user preference (push on/off per type) | Not required |
| **Informational / Reminders** | Fee reminder emails, daily report sent, upcoming birthday, newsletter published | Email + optional in-app | Yes — configurable, unsubscribe supported | Not required |

**Technical Requirements:**
- No recipient acknowledgement required for any individual delivery.
- Delivery attempts and available delivery status recorded in `notification_delivery` table.
- Retry and fallback behavior: defined around delivery errors, provider status, agreed timeout rules.
- Every attempt traceable.
- Critical tier: FCM failure → retry 3x (exponential backoff) → fallback to SMS (Sprint 13+) → fallback to email. All attempts logged.
- Operational tier: FCM delivery, retry 3x on failure, no SMS fallback.
- Informational tier: email batch delivery, no retry beyond provider's own.

Full spec: `15-NOTIFICATIONS.md`.

## 17. Non-Functional Requirements (Full Phase 1)

| Category | Requirement |
|---|---|
| **Performance** | Dashboard and attendance screens < 2s p95 on standard broadband. API p99 < 500ms. |
| **Scalability** | 500 students + 50 staff per centre. Multi-centre Phase 3: up to 20 centres, 500 concurrent users. |
| **Availability** | 99.5% uptime SLA Phase 1; planned maintenance windows outside 6 AM–8 PM local. |
| **Security** | RBAC, TLS 1.2+, encryption at rest + in transit, column-level encryption for sensitive fields, GDPR/PDPL-aligned. |
| **Data Residency** | UAE-hosted infrastructure (AWS me-central-1 recommended). |
| **Audit Logging** | All mutations logged, append-only, Super Admin UI viewer. |
| **Backup & Recovery** | Daily automated backups, RPO ≤ 4h, RTO ≤ 8h. |
| **Localization** | Arabic + English, full RTL. All third-party libraries must natively support RTL. |
| **Mobile Responsiveness** | All screens fully functional on tablet + mobile browsers. |
| **Accessibility** | WCAG 2.1 AA for parent portal. |
| **Barcode Support** | USB + Bluetooth HID scanners; mobile camera scanning. |
| **Integrations** | Phase 1: Email (SMTP/SES); Phase 2: payment gateway, external ERP, open API. |

---

**Next file to read**: `04-PRD-PHASE2-3.md` for Learning & Assessment, payment gateway, ERP integration, and multi-centre scale.
