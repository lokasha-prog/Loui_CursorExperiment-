# Incident response runbook

For the nursery IT person and the support team. Keep calm, write down times, and tell the nursery manager early.

## Severity
| Level | Meaning | Examples | Respond within |
|---|---|---|---|
| 1 Critical | Children's safety or data at risk, or nobody can use the system | Check-in down at opening, data leak, ransomware | 15 minutes, all hands |
| 2 High | A main function is broken for many people | Invoices will not create, parent app cannot sign in | 1 hour |
| 3 Medium | One feature broken, workaround exists | A report fails | 1 working day |
| 4 Low | Cosmetic or wording | Typo, spacing | Next release |

## First 15 minutes
1. Check `http://<server>/health/ready`. If it does not answer, restart the service: `Restart-Service NMS`.
2. Check the disk is not full and SQL Server is running (`Get-Service MSSQL*`).
3. Read the latest lines of the service log (`C:\NMS\logs`). Note the request id of any error shown to users.
4. Tell the manager what you know and what you are doing. If check-in is down, switch to the paper fallback (printed sign-in sheet from Reports > Daily sign-in sheet, and the saved emergency information on staff phones).

## Paper fallback (system down)
- Daily sign-in sheet printed the evening before.
- Emergency information is saved on staff phones and works with no signal; print the emergency PDF for each room weekly.
- After recovery, enter missed check-ins in the attendance screen (the change history records who and when).

## Data incident (possible leak)
1. Stop the spread: sign the affected users out (Users > deactivate), rotate secrets (`nms.keys.xml`, AI key, SMTP password).
2. Preserve evidence: copy the logs and the audit log export; do not delete anything.
3. Tell the manager and the owner immediately. Under the UAE data-protection law, the regulator and affected families must be told without undue delay; legal counsel decides the wording.
4. Find the cause from the audit log (who, what, when), fix, and write the post-incident report.

## Restore from backup
See PILOT-RUNBOOK section 6. Rehearse a restore on a spare computer at least once before go-live and after every upgrade.

## After every incident (within 3 working days)
Write: what happened, timeline, impact, cause, what we did, what we will change. Add any new checklist item here.

## Contacts (fill in before go-live)
| Role | Name | Phone |
|---|---|---|
| Nursery manager | | |
| Nursery IT person | | |
| Support (Mobility Eye) | | |
| Legal counsel | | |
