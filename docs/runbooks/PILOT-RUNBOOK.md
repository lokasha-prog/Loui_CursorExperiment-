# Pilot runbook: installing and running NMS at a nursery

Everything runs on **one Windows computer** that also holds the **Microsoft SQL Server** database. There is no cloud part, no Docker and no other database (golden rule). Screens are opened in a browser on any device on the nursery network.

## 1. What you need
- Windows 10/11 or Windows Server, SQL Server (Express 2019+ is enough), .NET 10 **ASP.NET Core runtime** (or use the packaged self-contained build), 2 GB free disk.
- A computer name or fixed address the tablets and phones can reach, for example `http://nursery-pc:8080`.
- Optional: an SMTP mail server for e-mails (daily reports, alerts). Without it, e-mails are marked "skipped" and everything else works.

## 2. Build the package (on the development machine)
```powershell
powershell -File tools\package-pilot.ps1      # creates dist\pilot (API + built web app + scripts)
```
Copy the `dist\pilot` folder to the nursery computer, for example `C:\NMS`.

## 3. Install (run PowerShell as Administrator, in `C:\NMS`)
```powershell
.\install-service.ps1 -Url http://0.0.0.0:8080 -SqlInstance '.\SQLEXPRESS' -Database NMS `
    -SmtpHost mail.example.com -SmtpPort 587 -SmtpFrom noreply@example.com
```
- Creates the database tables automatically on first start (empty; no data is ever pre-loaded).
- Creates `nms.keys.xml` with the secret keys. **Back this file up somewhere safe.** Without the encryption key, medical and identity data cannot be read again.
- Prints a **setup key**. Open `http://<computer>:8080`, choose "Set up your centre", enter the setup key, the centre name and the first administrator's details. This can only be done once.
- Open Windows Firewall for the chosen port if other devices cannot connect.
- The test-user buttons on the login screen do **not** exist in this install (they are development-only). Do not set `DevTools__Enabled`.

## 4. Optional settings (service environment variables, double underscore for levels)
| Setting | Meaning | Default |
|---|---|---|
| `Attendance__TimeZoneId` | Centre time zone | Asia/Dubai |
| `DailyReports__GenerateAt` | Time of day daily reports are written | 15:30 |
| `Audit__DefaultRetentionDays` | Change-history retention before a centre chooses | 2555 (about 7 years) |
| `Ai__Groq__ApiKey` | Turns on the AI summary for managers | not set |
| `Realtime__RedisConnectionString` | Only if several servers share live updates | not set |

Change a setting: edit the service's environment (`regedit`: `HKLM\SYSTEM\CurrentControlSet\Services\NMS\Environment`) or re-run `install-service.ps1`, then `Restart-Service NMS`.

## 5. Daily life
- **Health:** `http://<computer>:8080/health/ready` answers `{"status":"ready"}`.
- **Scheduled jobs** (every 15 minutes inside the service): marks children on excused leave absent, writes daily reports, sends waiting e-mails (with retries), applies the audit retention policy once a day if switched on.
- **Audit log:** Super administrators open **Audit log**. Retention can be changed (never below 365 days), expired history can be removed automatically or manually, and a spreadsheet copy can be downloaded first. Every purge is itself recorded.

## 6. Backups (do this on day one)
```powershell
schtasks /Create /TN "NMS backup" /SC DAILY /ST 02:00 /RU SYSTEM /TR "powershell -NoProfile -File C:\NMS\backup-db.ps1"
```
Backups go to `D:\NMS-backups` (change with `-Folder`), are verified, and kept 30 days. The SQL Server service account (`NT SERVICE\MSSQL$SQLEXPRESS`) must have Modify rights on that folder, or the backup fails. **Copy them off the computer regularly** (external disk or another machine).

### Restoring a backup
1. `Stop-Service NMS`
2. `sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE [NMS] FROM DISK = N'D:\NMS-backups\NMS_YYYYMMDD_HHMM.bak' WITH REPLACE"`
3. `Start-Service NMS`
4. Practise this once on a spare computer before go-live.

## 7. Upgrading
1. Run `backup-db.ps1`.
2. Build a new package, copy it over `C:\NMS` (keep `nms.keys.xml`), run `install-service.ps1` again with the same options. Database changes apply automatically on start.
3. Check `/health/ready` and sign in.

## 8. Go-live checklist
- [ ] Backup scheduled and a restore rehearsed; `nms.keys.xml` stored safely off the computer.
- [ ] Windows Update and antivirus fine with the service; computer set to never sleep.
- [ ] HTTPS in front of the service if devices connect over anything other than a private network.
- [ ] Setup done, administrator password stored in the nursery's password manager.
- [ ] Rooms, programmes, staff, children and guardians entered or imported; staff and parent accounts created; barcode labels printed.
- [ ] Barcode scanners tested (they type like a keyboard into the Attendance scan box).
- [ ] SMTP tested with a real daily report e-mail.
- [ ] Legal sign-off on data protection (PDPL) and on any AI use before enabling it; the AI feature sends totals only.
- [ ] Staff training done; UAT checklist passed (see `UAT-CHECKLIST.md`).
- [ ] In-app feedback button visible; someone named to read feedback daily in week 1.

## 9. When something is wrong
| Symptom | Check |
|---|---|
| Nobody can sign in | Is SQL Server running? (`Get-Service MSSQL*`). Is the NMS service running? `/health/ready` |
| Screens load but data is missing | SQL Server stopped or the disk is full |
| E-mails not arriving | `Notifications` page shows alerts in the app regardless; check SMTP settings; failed e-mails retry 3 times |
| Time shown wrongly | `Attendance__TimeZoneId` |
| Forgot administrator password | Another administrator resets it; if none, restore setup is not possible, contact support |

Logs: Windows Event Viewer plus the console output captured by the service manager.
