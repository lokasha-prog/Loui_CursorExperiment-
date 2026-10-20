# User acceptance checklist (internal team, one week, then the pilot nursery)

Run each item in **English and Arabic**, on a computer and on a phone-sized screen. Tick, date and sign. Anything that fails goes to the in-app Feedback button and the bug list.

## A. Setup and access
- [ ] First-time setup creates the centre and the first Super administrator; a second attempt is refused.
- [ ] Sign-in finds the centre from the e-mail alone; wrong password gives the same message as an unknown e-mail; five wrong tries lock the account.
- [ ] Each role sees only its own menu: centre administrator, teacher, parent, receptionist, accounts, IT, marketing.
- [ ] A teacher sees only the rooms they are assigned to; a parent sees only their own children (another child's page says "not found").

## B. Centre set-up
- [ ] Add rooms (with staff ratio), programmes, groups, labels.
- [ ] Add a child with two guardians (one cannot collect), set the child active.
- [ ] Add staff, assign them to rooms, create their sign-ins; create a parent sign-in from a guardian.

## C. A normal day (target: all done by the front desk without help)
- [ ] Scan a child in; the same scan again is refused politely; a wrong barcode says "no match".
- [ ] A child can only leave with an authorised guardian after the identity box is ticked.
- [ ] Mark a child absent; they can still arrive later.
- [ ] A staff member scans in; the room cover on the dashboard and Attendance page updates by itself within seconds.
- [ ] Take a staff member out so a room is short: red banner on every page, bell notification and an e-mail to managers; fix it and the banner clears.
- [ ] Late arrival and early leave show with the right status.

## D. Communication
- [ ] Teacher posts a diary note and a photo; the parent sees them; a different parent does not.
- [ ] An announcement and a medical note wait for a manager; approve and reject both work; rejected posts are never visible to parents.
- [ ] Parent writes to the centre; the shared inbox shows it; staff reply; the parent sees the reply as unread, then read.
- [ ] A broadcast reaches all families and cannot be replied to.
- [ ] After 15:30 each present child has a daily report; parents get it by e-mail (quiet) and see it in the feed.

## E. Leave, reports, safety
- [ ] Leave request: send, approve, reject, cancel; overlapping dates are refused; an excused leave marks the child absent automatically.
- [ ] Monthly report matches the attendance list; the Excel download opens correctly with Arabic names.
- [ ] Emergency sheet for one room prints in English and Arabic; allergies and who may collect are right. The print is recorded in the audit log.
- [ ] Audit log shows who changed what; medical details never appear in clear; retention settings save and refuse fewer than 365 days; a manual purge asks for confirmation.
- [ ] Notification preferences: urgent alerts cannot be switched off in the app.

## F. Reliability
- [ ] Restart the computer: the service comes back by itself and nobody is lost.
- [ ] Take a backup and restore it on a spare computer; sign in and see the data.
- [ ] Three tablets in use at once feel quick (pages open in under 2.5 seconds). Measured on the dev machine with 10 people at once: slowest 95th percentile 0.39 s (`python tools/perf-check.py`).

## G. Exit gates for the pilot (from the product plan)
- [ ] 10 or more staff and 30 or more parents active.
- [ ] 80 percent or more of expected attendance logged in the system.
- [ ] No critical bug open; at most 3 high bugs open.
- [ ] Shortage alert reaches managers in under 30 seconds.
- [ ] Survey: NPS 30 or higher; at least 60 percent would recommend (see `PILOT-SURVEY.md`).
