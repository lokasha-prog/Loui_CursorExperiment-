# Changelog

Every release of Bright Futures NMS, newest first. One version covers the whole product: the API, the web app,
the mobile app and the public website. Versions follow [Semantic Versioning](https://semver.org) as set out in
`docs/adr/0009-versioning.md`; add a release only with `tools/version.ps1`.
Versions before 1.0.0 are pre-releases rebuilt from the sprint plan (`docs/screens/project-pmo.html`); sprints 5 and 6
(pilot go-live and stabilisation) delivered no new features, so they have no version.

## [1.3.0] - 2026-10-25

- Chat with data now covers learning, rooms and staff: lessons, observations by type (for example concerns this month), assessments with milestone scores by area, the curriculum and its milestones, each room's occupancy, attendance and staff ratio, and staff by employment status and hire year. Still counts and totals only, never names.
- Chat with data is now a floating button on every screen for the super administrator, above the feedback button; the conversation stays while moving between pages, Escape closes it and focus returns to the button. English and Arabic.

## [1.2.0] - 2026-10-24

- Chat with data (super administrator): ask free questions such as "how many girls are present today in Little Stars?" or "how many teachers are on leave today?". The AI Brain looks up exact counts with fixed, checked data tools (children, staff, leave, money, enquiries, branches) and shows how it worked each answer out; only counts and totals go to the AI provider, never names. English and Arabic.

## [1.1.0] - 2026-10-24

- Release notes: a new page, open to everyone from the side menu and from the version number, listing every version and its feature changes, with the earlier history (0.0.1 to 0.27.0, one pre-release per development sprint) on request. English and Arabic page, screen-reader friendly.
- The changelog now carries the full feature history before 1.0.0, rebuilt from the sprint plan.

## [1.0.2] - 2026-10-24

- AI cards and the lesson helper now say plainly when the AI Brain is not set up on the server (no AI key added), instead of asking to try again later.
- New tools\set-ai-key.ps1: saves the AI key in local secrets and restarts the local API.

## [1.0.1] - 2026-10-24

- Every "are you sure?" question now uses the app's own confirmation box (English and Arabic) instead of the browser pop-up: removing guardians, expenses, events, lessons, observations, newsletters, documents, folders, roles and admission steps; cancelling invoices; sending newsletters; replacing the calendar link.
- Gallery photo viewer: Remove sits beside the title and the photo fits the window, so nothing needs scrolling.

## [1.0.0] - 2026-10-24

First numbered release: the system as it stands today, pilot-ready, becomes the baseline. It includes:

- Centres, rooms, programmes, groups and labels; students, guardians and staff; user accounts and roles.
- Attendance with QR check-in, live staff-to-child ratios, leave and the monthly and forecast reports.
- Daily feed, messages, notifications, newsletters, forms, resources and the calendar.
- Billing: invoices, payments (including online payments), expenses and reminders.
- Enquiries, tours and admissions; the bilingual public website with its enquiry form.
- Learning: curriculum, lessons, observations, assessments and the learning journey.
- Groups of centres: switching centres, group dashboard and reports, moving a child to another centre.
- Gallery: each child's photos and videos for staff and family, a "Gallery" / "My gallery" page in the menu,
  whole-nursery photos in every child's gallery, and sharing with a room, group, chosen children or branches.
- English and Arabic throughout; the mobile app for families and staff.

## [0.27.0] - Sprint 27 (pre-release)

- Groups of centres under a head centre, one sign-in for every centre with switching, group dashboard and three cross-centre reports.
- Add a centre from the screen; one person at several centres with a role in each; move a child to another centre.

## [0.26.0] - Sprint 26 (pre-release)

- Learning roll-out guide, the "teachers recording" measure on the dashboard and the Android test build of the phone app.

## [0.25.0] - Sprint 25 (pre-release)

- Speed test with 500 users (no errors, 99% of answers under 0.1 s), larger alert batches and a queue health check.
- Decision record ADR-0008: notifications and media stay inside the application.

## [0.24.0] - Sprint 24 (pre-release)

- Photos on feed posts, parents can start a message, family calendar by day and invoice summary on the phone.
- Alert channels per level on Android with tap-to-open, server address on the sign-in screen, app store build set-up.

## [0.23.0] - Sprint 23 (pre-release)

- Phone app works with no signal and sends what was done later; parent Learning screen; staff Observe screen with photos.
- Signing out wipes everything saved on the phone.

## [0.22.0] - Sprint 22 (pre-release)

- Five learning reports (progress, assessment, comparison, class and child milestones) and the learning journey as PDF in English and Arabic.
- Staff version with codes and scores; family version with plain levels from approved assessments only.

## [0.21.0] - Sprint 21 (pre-release)

- Montessori starter curriculum (5 areas, 4 age bands, 20 milestones in English and Arabic) and lesson plan templates per age group.
- AI lesson-idea helper and milestone suggestions (only curriculum names and the theme are sent to the AI provider).

## [0.20.0] - Sprint 20 (pre-release)

- Observations (formal, informal, concern) shared with families, with concern alerts.
- Assessment checklists, scoring, manager approval of a plain parent version, and the learning journey for staff and parents.

## [0.19.0] - Sprint 19 (pre-release)

- Curriculum frameworks kept as the centre's own data (areas, age bands, milestones, import by code, English and Arabic).
- Lessons: create, search and filter by curriculum, area, age band and room, with materials, objectives and milestones.

## [0.18.0] - Sprint 18 (pre-release)

- The nursery's public website in English and Arabic, with an enquiry form straight into the enquiry pipeline.
- Incident response runbook, customer onboarding guide and app security policy.

## [0.17.0] - Sprint 17 (pre-release)

- Accessibility check (WCAG 2.1 AA) clean in English and Arabic; security checks on every endpoint; end-to-end browser tests.

## [0.16.0] - Sprint 16 (pre-release)

- The phone app gains a parent view: feed, day reports, messages and alerts, with how-we-reach-you choices, in English and Arabic.
- Phones register for push alerts.

## [0.15.0] - Sprint 15 (pre-release)

- Staff phone app: fingerprint or face unlock, Today dashboard and staff cover, attendance by camera scan with an offline queue.
- Feed posting, messages, alerts and emergency information saved on the phone for use with no signal.

## [0.14.0] - Sprint 14 (pre-release)

- Newsletter builder with audiences, scheduling, open and click counts and an AI draft; resource centre with folders and versions.
- Right-to-left check of every screen in Arabic on desktop and phone; speed test with 100 users at once.

## [0.13.0] - Sprint 13 (pre-release)

- Report centre: 8 reports in Excel, PDF and JSON made in the background, with 30 days of history; analytics charts.
- SMS fallback for critical alerts, with a per-centre switch and a monthly limit.

## [0.12.0] - Sprint 12 (pre-release)

- Form builder (11 field types, versions, show-only-if rules, public forms) with answers and analytics.
- Admission checklist with automatic forms and overdue reminders; certificate printer with 10 designs.

## [0.11.0] - Sprint 11 (pre-release)

- Calendars (school, admin, personal) with events, holidays, birthdays, leave and tours, repeating events and a parent subscription link.
- ID card generator with 10 designs and QR codes.

## [0.10.0] - Sprint 10 (pre-release)

- Settings: centre profile and branding, attendance grace times, weekend days, the parent invoice switch and custom roles.

## [0.9.0] - Sprint 9 (pre-release)

- Six-stage enquiry pipeline with history and contact log, conversion to a student, and a sources report.
- Tour booking with availability, confirmation and reminder e-mails, follow-up tasks and the protected public enquiry form.

## [0.8.0] - Sprint 8 (pre-release)

- Expenses and the monthly expense report, billing settings, automatic fee reminders with a log, revenue and overdue reports.

## [0.7.0] - Sprint 7 (pre-release)

- Fee plans with items, tax and discounts; manual and automatic monthly invoices; part and full payments.
- Invoice PDF in English and Arabic, billing overview and family notifications.

## [0.4.0] - Sprint 4 (pre-release)

- Notifications at three levels, the audit log viewer and the one-click emergency PDF.

## [0.3.0] - Sprint 3 (pre-release)

- Activity feed with 8 kinds of post, the approval step for managers, and messaging with a shared inbox.

## [0.2.0] - Sprint 2 (pre-release)

- Check-in and check-out, safe pick-up, absences and leave, live staff cover, reports and the attendance forecast.

## [0.1.0] - Sprint 1 (pre-release)

- Students, staff, rooms, programmes, groups and guardians, with built-in sign-in and role-based access.

## [0.0.1] - Sprint 0 (pre-release)

- Project set-up: code layout, local services and the local SQL Server database.
