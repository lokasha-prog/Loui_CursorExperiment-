# 02 — PRD: MVP Pilot Release

> The smallest releasable NMS that delivers real value to 1–2 pilot nurseries and generates structured feedback to decide what to build next.
>
> **Build this first.** Do not start Phase 1 full scope until MVP pilot is live and pilot exit criteria are met.

---

## 1. MVP Purpose

Deploy a **safe, usable first release** to 1–2 pilot nurseries, observe actual workflows, and use structured feedback to decide what to change before expanding to full Phase 1.

The MVP is a **release boundary for piloting**, not a feature subset of Phase 1. It deliberately excludes modules that the source SRS lists as Phase 1 (billing, enquiry pipeline, forms, calendar breadth, reports catalogue, mobile) to keep the pilot focused on the riskiest unknowns: real-time operations, parent engagement, safety alerting, and Arabic RTL in production.

## 2. MVP Scope

### 2.1 In Scope (Build)

| Area | What's included |
|---|---|
| **Tenancy** | Single-tenant deployment (one pilot nursery per deployment). Architecture is multi-tenant-ready, but pilot runs one tenant. |
| **Languages** | English + Arabic (web). Full RTL parity. |
| **Identity** | Keycloak with one realm. Roles: Super Admin, Centre Admin, Teacher, Parent. MFA optional for admins. |
| **Centre setup** | Students (full lifecycle), Staff, Rooms, Programs, Groups, Labels, Guardians (primary + secondary). |
| **Attendance** | Student + staff check-in/check-out. Barcode scanner support (HID keyboard emulation). Daily summary counters. |
| **Ratio monitoring** | Real-time staff-to-student ratio per room. WebSocket-driven dashboard widget. Critical alert when ratio breaches. |
| **Parent communication** | Activity feed (8 types): announcement, photo, video, diary, food, toilet, medical, mood. Staff approval workflow before parent visibility. Daily consolidated report. |
| **Messaging** | Staff-to-parent shared inbox. Parent-to-staff. Threaded. Read receipts. Staff-to-staff internal. Broadcast announcements. |
| **Dashboard** | KPI cards (rooms, staff, students, programs). Live attendance summary. Live ratio widget. Pending actions widget (leave approvals, unapproved activities, unread messages). |
| **Audit trail** | Append-only audit log on all mutations. Super Admin viewer with filter by user, date, entity, action. |
| **Media pipeline** | Object storage with presigned URLs. Content moderation (basic — file type + size validation + ClamAV scan). CDN with signed URLs. Quarantine blocks CDN delivery. |
| **Notifications** | In-app banner (Critical tier). Push notifications via FCM (Operational + Critical). Email via SMTP for Informational. NO SMS in MVP (deferred to Phase 1 Sprint 13). |
| **Security baseline** | TLS 1.2+. Encryption at rest. Column-level encryption for medical/allergy/custody fields. Rate limiting. RBAC + parent-child ownership checks. |
| **Reports (minimal)** | Daily attendance print/export. Emergency child report (one-click PDF per child + bulk per room). |
| **Web frontend** | Responsive React app. Desktop-first for staff. Mobile-responsive for parents. RTL parity. |
| **Deployment** | K8s (dev/staging/prod). CI/CD pipeline. Monitoring (Prometheus + Grafana + Loki). Daily backups. |

### 2.2 Out of Scope for MVP (Deferred to Later Phase 1 Sprints)

These are **Phase 1 features** per the source SRS, but explicitly **excluded from MVP** to keep pilot focused. They are built in later sprints after MVP validation.

- ❌ Billing & accounting automation (Sprint 7–8)
- ❌ Enquiry & tour pipeline (Sprint 9–10)
- ❌ Configurable forms builder & admissions (Sprint 11–12)
- ❌ Calendar module full breadth (Sprint 11–12) — basic school events only in MVP
- ❌ ID card / certificate generators (Sprint 11–12)
- ❌ Full reports catalogue (Sprint 13–14) — only daily attendance + emergency report in MVP
- ❌ Newsletter builder & resource centre (Sprint 5–6 minimal — full features later)
- ❌ Mobile app (Sprint 15–16) — MVP is web-only
- ❌ SMS notifications (Sprint 13)
- ❌ AI-assisted newsletter content (Phase 2)
- ❌ Learning & Assessment module (Phase 2)
- ❌ Payment gateway (Phase 3)
- ❌ Multi-centre SaaS (Phase 3)

### 2.3 Deliberate MVP Constraints

To force focus and ship in ~10 weeks:

1. **One nursery per deployment** — no multi-tenant UI in MVP.
2. **Web only** — no native mobile app. Parents use responsive web on phone.
3. **No SMS** — Critical alerts use push + in-app banner only. SMS added in Sprint 13.
4. **No payment processing** — invoices not generated in MVP. Parents pay via existing channels.
5. **No learning/assessment** — observation is parent-communication-only in MVP.
6. **No custom forms** — enquiry form is a fixed web form, not a builder.
7. **No tours module** — pilot nurseries manage tours via existing calendar.
8. **One UAE region** — pilot deploys to single AWS UAE region (me-central-1) or pilot nursery's chosen cloud.

## 3. User Stories

Stories use the format: `As a <role>, I want <goal>, so that <value>`. Each story has acceptance criteria (AC) in Given/When/Then format. Story IDs use `MVP-<module>-<n>`.

### 3.1 Authentication & Onboarding

#### MVP-AUTH-01: First Super Admin bootstrap
**As a** Centre Director  
**I want** to set up the first Super Admin account via secure bootstrap link  
**So that** I can begin configuring my nursery without IT help.

**AC:**
- Given a fresh deployment with no users, when the bootstrap URL is opened with a one-time token (valid 24h, emailed to the licensed contact), then a Super Admin account creation form is shown.
- Given a valid token, when the user sets email + password (min 12 chars, complexity rules) + TOTP MFA, then the account is created in Keycloak with role `super_admin` and redirected to onboarding wizard.
- Given an expired or used token, when the URL is opened, then a 410 Gone page is shown with a "contact support" link.

#### MVP-AUTH-02: Role-based login
**As a** staff member or parent  
**I want** to log in with my email + password (or magic link)  
**So that** I access my role-appropriate dashboard.

**AC:**
- Given valid credentials, when the user logs in, then they receive a 15-minute JWT access token + 7-day refresh token (HttpOnly cookie) and are redirected to their role's landing page.
- Given a Teacher role, when logged in, then landing page is Dashboard with their assigned rooms' ratios prominent.
- Given a Parent role, when logged in, then landing page is their child's activity feed.
- Given 5 failed attempts, when the 6th attempt fails, then the account is locked for 15 minutes and the user is emailed.

#### MVP-AUTH-03: Parent-child ownership enforcement
**As a** Parent  
**I want** the system to strictly limit my access to my own child's data  
**So that** I cannot see other children's photos, reports, or messages.

**AC:**
- Given a Parent authenticated, when they call any `/api/v1/students/*` endpoint, then only students where they are linked as guardian are returned.
- Given a Parent attempts `/api/v1/students/<other-child-id>`, when the request hits the API, then a 404 Not Found is returned (not 403, to prevent ID enumeration).
- Given a Parent attempts `/api/v1/activities?student_id=<other-child-id>`, when the request is processed, then an empty result set is returned (no error leak).

### 3.2 Centre Management

#### MVP-CENTRE-01: Create a room
**As a** Centre Admin  
**I want** to create a room with capacity and ratio config  
**So that** the system can track children and staff per room and enforce ratios.

**AC:**
- Given the Centre Admin is on Settings → Rooms, when they click "Add Room", then a form opens with: name (i18n: en + ar), capacity, min_age_months, max_age_months, ratio_staff_to_student (e.g., 1:4), active checkbox.
- Given a valid form, when submitted, then the room is created with `tenant_id` of the current tenant, audit log records CREATE, and the room appears in the rooms list.
- Given a name conflict in the same tenant, when submitted, then a 409 Conflict is returned with a clear message in the user's locale.

#### MVP-CENTRE-02: Register a student with guardians
**As a** Centre Admin  
**I want** to register a student with their guardians, room, program, and key person  
**So that** the student is ready for attendance and parent communication.

**AC:**
- Given a new student form, when filled (name, DOB, photo upload via presigned URL, gender, room, program, group, primary guardian details, optional secondary guardian, key person staff assignment, medical notes encrypted, allergy info encrypted), when submitted, then the student is created with status `Signed Up`.
- Given the student is created, when the Centre Admin clicks "Activate", then status transitions to `Active`, an invitation email is sent to the primary guardian with a magic link to set up their parent account, and the audit log records the status transition.
- Given a guardian email that already exists as a parent in the same tenant, when the student is saved, then the guardian is linked to the additional student (not a new account created).

#### MVP-CENTRE-03: Student status lifecycle
**As a** Centre Admin  
**I want** to transition student status: Signed Up → Active → On Hold → Withdrawn / Transferred  
**So that** the system reflects the real lifecycle and inactive students don't appear in attendance.

**AC:**
- Given an Active student, when Centre Admin sets status to On Hold, then the student is excluded from attendance lists, but remains in reports; the audit log records the transition + reason.
- Given an Active student, when status is set to Withdrawn, then the student's data is retained (soft delete `is_deleted=true`, `deleted_at=now()`), guardians lose access immediately, but the record remains for compliance retention.

### 3.3 Attendance

#### MVP-ATT-01: Staff check-in a student via barcode
**As a** Teacher at the door  
**I want** to scan a parent's barcode (on their phone or paper card) at drop-off  
**So that** the student is checked in quickly and accurately.

**AC:**
- Given the Attendance screen is open on a USB/Bluetooth barcode scanner-connected device, when a barcode is scanned (HID keyboard emulation sends the code + Enter), then the system looks up the student by barcode, checks them in with timestamp, plays a success sound, and shows a green confirmation overlay for 2s.
- Given a scanned barcode that doesn't match any active student in the tenant, when the lookup fails, then a red error overlay shows "Student not found" + audio cue, and the event is logged to `attendance_event` table with `outcome=FAILED_LOOKUP`.
- Given a student already checked in, when their barcode is scanned again, then a yellow warning shows "Already checked in at <time>" + audio cue, no second check-in recorded.

#### MVP-ATT-02: Manual check-in/out
**As a** Teacher  
**I want** to manually check in/out a student from the attendance screen  
**So that** I can handle exceptions (forgotten card, scanner broken).

**AC:**
- Given the attendance list filtered by room and date, when the Teacher clicks "Check In" next to a Pending student, then the student is checked in with the current timestamp, summary counters update in real-time (via SignalR), and the audit log records the action.
- Given a student checked in > 24h ago, when a Teacher tries to edit the check-in time, then the system requires a Supervisor override (Centre Admin role), the override is logged as a separate audit event, and the original timestamp is preserved in `attendance_event`.

#### MVP-ATT-03: Live ratio monitor
**As a** Centre Admin  
**I want** to see a live room-by-room ratio on my dashboard  
**So that** I can spot breaches before they become safety incidents.

**AC:**
- Given the Dashboard is open, when the page loads, then a "Live Ratio Monitor" widget shows all active rooms with: Room name, Student count, Staff count, Ratio, Compliance badge (green OK / red BREACH / yellow WARNING).
- Given a room's ratio falls below the configured threshold, when the breach occurs, then within 30 seconds: (a) the dashboard widget turns red, (b) a SignalR event pushes a Critical in-app banner to all admins/teachers, (c) an FCM push notification is sent to all admins, (d) a `notification` record is created with `tier=Critical`.
- Given a breach is resolved (more staff checked in or students leave), when the ratio returns to compliant, then the dashboard updates within 30s and a "resolved" notification is sent.

### 3.4 Parent Communication

#### MVP-COMM-01: Teacher posts a photo activity
**As a** Teacher  
**I want** to take a photo of an activity in class, write a caption, and post it for parents  
**So that** parents see what their child did today.

**AC:**
- Given the Teacher is on the Activity Feed → New Post, when they select type=Photo, upload an image (via presigned URL — JPG/PNG/HEIC, max 10MB, max 5 images per post), select the student(s) tagged, write a caption (i18n optional — if Arabic provided, both shown), then the post is saved with `approval_status=PENDING` and is NOT visible to parents.
- Given the post is saved, when an auto content-moderation scan (file type, size, ClamAV malware scan, optional AWS Rekognition for inappropriate content) returns APPROVED, then `approval_status` auto-transitions to APPROVED and the post becomes visible to linked parents.
- Given a scan returns QUARANTINED, when the post is saved, then `media.scan_status=QUARANTINED`, the media is moved to a quarantine bucket, the CDN returns 403 on any signed URL for it, and the Teacher + Centre Admin are notified in-app.
- Given a Teacher wants to override auto-approval (e.g., add a sensitive caption review), when they mark the post as "Needs Manual Approval", then a Centre Admin must approve before parent visibility.

#### MVP-COMM-02: Parent views activity feed
**As a** Parent  
**I want** to see a chronological feed of my child's activities  
**So that** I feel connected during the workday.

**AC:**
- Given the Parent logs in (web responsive on mobile), when they open the app, then the default landing is their child's activity feed (most recent first), with each card showing: timestamp, type icon, caption, photo/video thumbnail (tappable to full-screen), staff member name.
- Given the Parent has multiple children, when they open the feed, then a child selector at top lets them switch; default is the most recently active child.
- Given a new activity is posted, when the parent has the app open, then a SignalR event appends the new card to the top with a subtle animation + soft chime (if not muted in settings).
- Given the parent scrolls, when they reach the bottom, then cursor-based pagination loads more (page size 20).

#### MVP-COMM-03: Daily consolidated report
**As a** Parent  
**I want** to receive a daily summary of my child's day at pickup time  
**So that** I have a structured view even if I missed real-time updates.

**AC:**
- Given 3:30 PM local time, when the scheduled job runs, then for each Active student present today, a daily report is generated (PDF + in-app card) containing: check-in/out times, food log, toilet log, nap log, mood, any incidents, photos posted today, staff member names.
- Given the report is generated, when it's ready, then: (a) it appears as a special "Daily Report" card in the parent's feed, (b) an email is sent to the primary guardian with the PDF attached, (c) a push notification is sent.
- Given a student was absent today, when the job runs, then no report is generated for them, but a "Marked Absent" notification is sent to the parent.

### 3.5 Messaging

#### MVP-MSG-01: Parent sends a message to staff
**As a** Parent  
**I want** to message the nursery staff about my child  
**So that** I can ask questions or share concerns.

**AC:**
- Given the Parent opens Messaging, when they tap "New Message", then a thread is created between them and the room staff (all staff assigned to their child's room are recipients).
- Given a parent sends a message, when the message is sent, then all room staff receive an in-app notification + FCM push (Operational tier), and the message appears in the staff shared inbox.
- Given any staff member responds, when the response is sent, then the parent receives a push notification and the thread updates in real-time on their app.
- Given a parent sends a message outside staff hours (configurable per centre — default 7 AM – 6 PM), when the message is sent, then an auto-reply confirms receipt and states staff will respond next working day.

#### MVP-MSG-02: Staff shared inbox
**As a** Teacher  
**I want** to see all parent messages in a shared inbox  
**So that** any available staff can respond, not just the assigned teacher.

**AC:**
- Given the Teacher opens Messaging, when the inbox loads, then all parent threads for their assigned rooms are shown, sorted by most recent, with unread indicator.
- Given a Teacher opens a thread, when they read it, then the thread is marked read for them but NOT for other staff (per-recipient read receipts).
- Given two staff respond in the same thread, when the responses are sent, then both are visible in the thread with timestamps + staff names, and the parent sees a single conversation.

### 3.6 Dashboard

#### MVP-DASH-01: Centre Admin dashboard
**As a** Centre Admin  
**I want** a real-time dashboard on login  
**So that** I immediately see the centre's status and any issues needing attention.

**AC:**
- Given the Centre Admin logs in, when the Dashboard loads (< 2s p95), then they see: KPI cards (Total Rooms, Total Staff, Total Students, Total Programs), Today's Attendance Summary (Checked In / Checked Out / Absent / Pending — for staff and students separately), Live Ratio Monitor widget, Pending Actions widget (unapproved activities count, pending leave requests count, unread parent messages count).
- Given any KPI changes (a student checks in, an activity is posted, a message arrives), when the change occurs, then the dashboard updates within 5 seconds via SignalR without page reload.
- Given the admin clicks a KPI card, when clicked, then they navigate to the relevant module filtered to today.

### 3.7 Notifications

#### MVP-NOTIF-01: Critical notification escalation
**As a** Centre Admin  
**I want** critical safety alerts (ratio breach, unexpected absence) to reach me reliably  
**So that** I never miss a safety event.

**AC:**
- Given a ratio breach occurs, when the system detects it, then within 30 seconds: in-app banner (red, persistent until dismissed) + FCM push to all admins. SMS is NOT sent in MVP.
- Given a Critical notification fails to deliver (FCM returns failure), when the failure is detected, then a retry is attempted up to 3 times (exponential backoff), all attempts are logged to `notification_delivery`, and on final failure a fallback email is sent to the admin's email.
- Given the admin opens the in-app banner, when they tap "Acknowledge", then the banner dismisses for them, but other admins still see it until they acknowledge. (Note: per source SRS §7 of planning doc, individual notification delivery does NOT require recipient acknowledgement — this "acknowledge" is a UX affordance only, not a delivery confirmation.)

### 3.8 Audit & Settings

#### MVP-AUDIT-01: Super Admin views audit log
**As a** Super Admin  
**I want** to view a filterable audit log of all system changes  
**So that** I can investigate incidents and demonstrate compliance.

**AC:**
- Given the Super Admin opens Settings → Audit Log, when the page loads, then the most recent 100 audit entries are shown (newest first), with columns: timestamp, user, entity type, entity ID, action, summary.
- Given the Super Admin applies filters (user, date range, entity type, action), when filters are applied, then the list updates with cursor-based pagination (page size 100).
- Given the Super Admin clicks an entry, when expanded, then the full JSONB diff (before/after) is shown.

## 4. MVP Acceptance Criteria (Pilot Exit Gates)

The MVP is considered **piloted and ready for Phase 1 expansion** when ALL the following are true. These must be measured during the pilot and signed off by the Product Owner.

### 4.1 Functional Acceptance

| Area | Acceptance evidence | Status |
|---|---|---|
| Core setup | A nursery admin can create and maintain the pilot's children, guardians, staff, rooms, programs, and groups, with role permissions applied and validated. | Proposed |
| Attendance & safety | Staff can check children and staff in/out via barcode and manual override; room attendance and staff-to-child ratio are visible in real time; configured critical events create a traceable alert and delivery outcome within 30s. | Proposed |
| Parent experience | A parent can access only linked child records, view approved activity updates and messages in their selected language (EN or AR), and receives a daily report by 4 PM local. | Proposed |
| Privacy & audit | Sensitive data and media follow approved access, storage, retention, and audit rules; quarantined media is inaccessible to parents via any path. | Legal approval required |
| Usability & quality | Critical pilot journeys pass UAT with the pilot nursery; parent portal accessibility (WCAG 2.1 AA) target recorded; Arabic RTL checks pass; no unresolved release-blocking defects. | Define severity + sign-off owners |
| Performance & recovery | Dashboard/attendance p95 < 2s on standard broadband; backup restoration demonstrated; incident response runbook walked through. | Confirm load profile |

### 4.2 Pilot Engagement Metrics

| Metric | Target | Measurement |
|---|---|---|
| Pilot nurseries live | ≥1 (target 2) | Count of deployments with ≥1 active user in past 7 days |
| Staff DAU | ≥10 | Daily active staff users |
| Parent WAU | ≥30 | Weekly active parent users |
| Daily check-ins logged in system | ≥80% of expected attendance | Compare system check-ins vs. expected enrolment |
| Activities posted per day | ≥5 per active room | Average across pilot period |
| Parent messages per week | ≥10 | Total threads + responses |
| Critical bug count | 0 | Open P0/P1 bugs in tracker |
| High bug count | ≤3 | Open P2 bugs in tracker |
| Parent portal p95 load | < 2.5s | RUM data |
| Ratio breach alert delivery time | < 30s from breach to push | Audit log + delivery log |
| Pilot user NPS | ≥30 | Survey at end of pilot |
| Pilot user "would recommend" | ≥60% | Survey at end of pilot |

### 4.3 Pilot Process Requirements

Before pilot launch, these must be agreed:
- Named pilot nurseries + signed pilot agreement (free or discounted, time-limited)
- Named user representatives at each pilot
- Feedback channels (Slack/WhatsApp group with pilot leads, in-app feedback button)
- Issue triage owner (Tech Lead) + SLA (P0 < 4h, P1 < 24h, P2 < 1 week)
- Review cadence: weekly during pilot (Sprint 4–5), bi-weekly after
- MVP success measures baseline (captured at pilot start, compared at pilot end)
- Expansion decision criteria: defined before pilot, signed off by Product Owner

## 5. MVP Dependencies & Assumptions

### 5.1 Hard Dependencies (Must Resolve Before Sprint 0 Ends)

1. **Pilot nursery signed** — at least one nursery organization committed to pilot with named director + 2-3 staff + 5-10 parents willing to test.
2. **UAE cloud provider selected** — AWS ME Central (me-central-1) recommended for SaaS pilot; on-prem pilot uses nursery's chosen infra.
3. **PDPL legal review complete** — legal counsel signs off on data handling, retention, parent consent terms.
4. **KHDA/ADEK alignment confirmed** — at minimum, confirm we're not violating any inspection requirements by digitizing records.
5. **Pilot agreement template signed off** — covers data ownership, pilot duration, support SLA, exit terms.

### 5.2 Working Assumptions (For Planning Only — Confirm Before Sprint 1)

1. **One tenant per pilot deployment** — even though architecture is multi-tenant-ready, MVP runs single-tenant.
2. **English + Arabic** — no other languages in MVP. Auto-translation considered in Phase 2.
3. **Parents use responsive web on their phones** — no native parent app in MVP.
4. **Pilot nurseries have stable WiFi** — offline mode is for emergency report only, not full app.
5. **Pilot nurseries have USB or Bluetooth HID barcode scanners** — we don't supply hardware.
6. **Ratios are configured per room per age band** — default to UAE regulatory standards, configurable per centre.
7. **Identity provider is Keycloak** — pilot nurseries don't have existing AD to federate.
8. **Email delivery via AWS SES or Azure Communication Services** — not self-hosted SMTP.
9. **Push notifications via FCM** — APNs handled via FCM's APNs bridge, no separate APNs key needed in MVP.

## 6. MVP Sprint Plan Summary

Detailed sprint breakdown in `19-ROADMAP-SPRINTS.md`. Summary:

| Sprint | Weeks | Deliverable |
|---|---|---|
| Sprint 0 | 1–2 | Env setup, tech stack lock, pilot nursery signed, Sprint 1–5 plan agreed |
| Sprint 1 | 3–4 | Centre Management module (Students, Staff, Rooms, Programs, Groups, Guardians, Labels) + auth + RBAC |
| Sprint 2 | 5–6 | Attendance module (check-in/out, barcode, leaves) + Dashboard (KPIs, live ratio) |
| Sprint 3 | 7–8 | Parent Communication module (activity feed 8 types, approval workflow, media pipeline) + Messaging (shared inbox, threads, push) |
| Sprint 4 | 9–10 | Notifications (3-tier in MVP scope — push + email + in-app, no SMS) + Daily report generation + Audit log viewer + Emergency child report |
| Sprint 5 | 11–12 | Pilot deployment + UAT + bug fixes + go-live + pilot feedback loop established |

**Total MVP duration: 12 weeks** (Sprints 0–5). Buffer of 2 weeks included for pilot-driven fixes.

After Sprint 5 pilot is live and stable, transition to Phase 1 Sprint 6 onwards (billing, enquiry, calendar, etc. per `19-ROADMAP-SPRINTS.md`).

## 7. Risks Specific to MVP

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Pilot nursery pulls out | Medium | High — delays MVP launch by 4+ weeks | Have 2 pilot candidates lined up by Sprint 0 end; offer pilot incentive (free 6-month SaaS after pilot) |
| Arabic RTL breaks in production | High | High — parent adoption fails | Mandatory RTL QA pass every sprint; Arabic-speaking QA tester on team from Sprint 1 |
| Content moderation false positives block legitimate photos | Medium | Medium — frustrates teachers | Configurable moderation sensitivity; manual override by Centre Admin; clear appeal flow |
| Ratio alert delivery unreliable | Medium | High — safety incident | Fallback email on FCM failure; alert retry queue with DLQ; monitor delivery rate in Grafana |
| Parents don't adopt the web app | High | High — pilot fails | Make parent onboarding < 2 minutes; magic-link login (no password to remember); daily report email drives first login; in-app tutorial on first visit |
| Pilot scope creep (nursery asks for billing mid-pilot) | High | Medium — delays Phase 1 | Clear MVP scope doc shared with pilot; "Phase 1 roadmap" shared; change control process for any addition |
| Performance under load (even small) | Low | High — pilot reputation | Load test with k6 from Sprint 2; p95 dashboard < 2s is a sprint acceptance gate from Sprint 4 |

---

**Next file to read**: `03-PRD-PHASE1-FULL.md` for the full Phase 1 module specs (read when MVP is piloted and you're expanding).
