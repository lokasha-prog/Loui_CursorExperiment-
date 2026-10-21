# Security test pack — for the outside penetration testers

Prepared 2026-10-21. Give this file to the testing company together with a **test copy** of the system (never the live one) and test accounts for each role.

## 1. What is being tested
- Web app (React) and phone app (Expo) for staff and parents.
- API: about 228 operations (description at `/openapi/v1.json`, available on the test copy).
- Database: SQL Server with row-level security by centre ("tenant") and column encryption for form answers.
- Scheduled jobs and background workers (reports, newsletters, reminders).
- Out of scope: the hosting provider, the SMS/e-mail/AI providers, denial-of-service beyond the rate limits.

## 2. How sign-in and access work
- Sign-in returns a short-lived access token (JWT) plus a refresh token (web: HTTP-only cookie with a CSRF header; phone: secure store). Refresh tokens rotate; reuse of an old one ends the session family.
- Roles: centre admin, teacher, receptionist, accounts, marketing, IT, parent, and custom roles made in Settings. Permissions travel in the token; the API checks a permission on every endpoint.
- Every table with centre data has a tenant column, an EF Core query filter and a SQL Server row-level security policy, so a bug in one layer should not expose another centre's data.
- Parents see only their own children (scope filter in the API); teachers only their rooms.

## 3. Test accounts to request
One per role per test centre, plus a second test centre (to try crossing from one centre to the other). The development "test-user" helper (`/api/v1/dev/*`) must be **absent** on the test copy: confirm it answers 404.

## 4. Already checked by us (repeat with `python tools/security-smoke.py <api-url>`)
| Check | Result |
|---|---|
| Every operation without sign-in returns 401, except the public ones | 228 of 228 |
| Parent role reading every GET operation | 17 answered 200, all expected for parents (own children, feed, messages, notices, invoices, resources, forms) |
| Odd input (quotes, long text, bad dates, missing values) never gives a 500 | fixed 2026-10-21: bad or missing query values now give 400 |
| Security headers on the API | nosniff, DENY framing, strict referrer policy, CSP `default-src 'none'`, HSTS on https |
| Rate limits | per person, per address, on login and on public forms |
| Accessibility scan (axe, AA) | clean after colour fixes |
| Dependencies | no vulnerable NuGet packages; `pnpm audit --prod` clean |
| Secrets in the repository | none (keys live in user-secrets or server environment) |

## 5. Please look hard at
1. Crossing centres: any call that returns or changes another centre's data.
2. Parent-to-parent: a parent reading another family's child, invoice, message, photo or report.
3. Public pages: enquiry form, public forms, calendar feed link, newsletter click redirects (open-redirect, token guessing).
4. File uploads: media, resources, form file answers (type sniffing, size, stored paths, download headers).
5. Rich text: newsletter HTML (sanitised on server and in the browser).
6. Token handling: refresh rotation, logout, phone refresh token header (`X-NMS-Client: mobile`).
7. Reports and exports: audit log export, emergency data (`/emergency/data`), personal data in Excel/PDF.
8. AI Brain: confirm only totals are sent (no names, no free text); prompt-injection through any field that reaches a prompt (none should).
9. Business logic: paying an invoice twice, negative amounts, changing a paid invoice, check-out by an unlisted guardian.

## 6. Known and accepted
- The web site headers (CSP, HSTS) are set by the reverse proxy in production; the development server does not send them. The production proxy settings are in the go-live runbook.
- Push alerts go through Expo's service; only the notice title and text are sent.
- Signatures on certificates are drawn pictures, not legally certified e-signatures.

## 7. Reporting
Findings by severity (Critical / High / Medium / Low), with steps to repeat. We aim to fix Critical and High within 3 working days and re-test before go-live.
