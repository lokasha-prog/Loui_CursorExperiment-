# Customer onboarding guide

How a new nursery goes from signed contract to a working centre. Typical time: 2 weeks.

## Week 0 - Before day one
- [ ] Contract signed; centre name (English and Arabic), time zone, opening days confirmed.
- [ ] Choose hosting: one on-site computer (see PILOT-RUNBOOK) or a managed server.
- [ ] Decide e-mail sending (SMTP details), optional text messages (provider account) and the AI Brain (provider key).
- [ ] Legal: privacy notice for parents and photo-consent form prepared (UAE data-protection law).

## Week 1 - Set up
1. Install, run the first-time set-up and create the first administrator (PILOT-RUNBOOK 3).
2. Settings: centre profile and logo, grace times, weekend days, parent-portal invoice switch.
3. Create rooms (with capacity and staff-to-child ratio), programmes, groups.
4. Add staff and give each a role (centre admin, teacher, receptionist, accounts); make custom roles in Settings if needed.
5. Import or enter children and guardians; print ID cards; link each family to a parent account (invite by e-mail).
6. Billing: fee plans with tax and discounts, billing settings (due days, reminders).
7. Website: set `centreSlug` in `website/config.js` and publish; test the enquiry form end to end.

## Week 2 - Train and go live
- [ ] Manager (2 h): dashboard, staff cover, AI briefings, reports, settings.
- [ ] Reception (1.5 h): enquiries, tours, check-in/out, ID cards.
- [ ] Teachers (1 h): phone app - scan, feed posts, messages, emergency information offline.
- [ ] Accounts (1.5 h): invoices, payments, expenses, reminders.
- [ ] Parents: 15-minute welcome session or video; install the phone app; choose alert preferences; Arabic or English.
- [ ] Run the UAT scripts (docs/uat/UAT-PLAN.md) and sign off.
- [ ] First backup done and a restore rehearsed. Emergency paper fallback printed.
- [ ] Go-live day: support person on site in the morning; check the staff-cover screen and first check-ins together.

## After go-live
- Day 1, 3 and 7 check-in calls; weekly review for the first month (open questions, defects, feedback form).
- Month 1: satisfaction survey (PILOT-SURVEY), review report usage and billing accuracy.
