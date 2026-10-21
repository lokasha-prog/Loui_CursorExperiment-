# User Acceptance Test (UAT) plan — Bright Futures NMS

Plain-language plan for the pilot nursery. Each script is a short list of things a real person does. Mark each step Pass / Fail and write what you saw. Anything that fails goes in the defect log at the end.

## Who tests what
| Role | Who | Scripts |
|---|---|---|
| Manager (centre admin) | Nursery manager | A, B, C, D, E, F |
| Receptionist | Front desk | A, C |
| Teacher | One teacher per room | A, G |
| Accounts | Finance person | D |
| Parent | 3 volunteer families | H |

Test in **English and Arabic**. Test on a **computer and a phone**. Use the phone app (staff and parent views) for scripts A, G, H.

## Rules for testers
- Use the test accounts the IT person gives you; never share passwords.
- Do not use real children's photos until the privacy sign-off is done.
- Write down anything that confuses you, even if it "worked".

## Scripts
### A. Attendance
1. Check a child in by scanning the card (phone camera) and by typing the number. Pass if the child shows as Present within 2 seconds.
2. Check the child out with a named guardian. Pass if the pick-up is recorded.
3. Mark one child absent. Pass if the parent gets an alert.
4. Switch the phone to aeroplane mode, check a child in, switch back, press Send now. Pass if it arrives once.
5. Open the live staff-cover screen. Pass if a room short of staff shows red.

### B. Centre set-up
1. Add a room, a student, a guardian, a staff member. Pass if they appear in lists and search finds them.
2. Print an ID card (A6 and card size). Pass if the QR scans.
3. Print the emergency sheet for one child and for a whole room. Pass if allergies and parent phones are readable.

### C. Enquiries and tours
1. Enter an enquiry through the public form and through the desk. Pass if both appear in the pipeline.
2. Book a tour; check the confirmation e-mail. Move the enquiry to Enrolled and convert it to a student.

### D. Billing
1. Create a fee plan with tax and a discount. Generate an invoice for a child; download the PDF in English and Arabic.
2. Record a part payment, then the balance. Pass if the status changes Partly paid then Paid.
3. Add an expense. Check the monthly report and the money briefing (AI).

### E. Communication and reports
1. Write a newsletter, send it to one room, check the parent sees it and the open count rises.
2. Upload a document to the resource centre; confirm a parent can download it.
3. Make three reports (Excel, PDF) from the report centre.

### F. AI Brain
1. Press **Write briefing** on the dashboard, billing, enquiries and staff screens. Pass if each is understandable, uses only the figures listed under "Figures used", and no names appear.
2. Do the same in Arabic. Pass if it reads naturally right-to-left.

### G. Teacher day
1. Post a diary entry and a meal from the phone. Pass if it waits for approval, then shows to parents once approved.
2. Reply to a parent message.

### H. Parent
1. Sign in on the phone; read the feed and the day report.
2. Reply to a message; change alert choices; turn on phone alerts.
3. Switch to Arabic; confirm everything reads right-to-left.

## Acceptance (go-live criteria)
- No open defect of severity High or Critical.
- At least 90% of steps Pass in each script.
- Manager, accounts and two parents each sign off in writing.

## Defect log
| # | Script/step | What happened | Severity (Critical/High/Medium/Low) | Reported by | Status |
|---|---|---|---|---|---|
| | | | | | |

## Sign-off
| Role | Name | Date | Signature |
|---|---|---|---|
| Nursery manager | | | |
| Accounts | | | |
| Parent representative | | | |
| Project owner | | | |
