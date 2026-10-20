# ADR-0007: Choices made while delivering the MVP (Sprints 2 to 5)

Status: Accepted (owner approved continuing without further questions, 2026-10-20).

| Topic | Spec said | We did | Why |
|---|---|---|---|
| Background jobs | Hangfire | A small built-in scheduler inside the API (`ScheduledJobsService`), jobs are ordinary commands run per centre every 15 minutes | One less moving part on an on-site computer; fully testable; SQL Server only. Revisit if jobs need dashboards or distributed workers. |
| Media storage and scanning | S3 + ClamAV + Rekognition | Files stored in SQL Server (`MediaFiles`), real file type checked from the first bytes, size limits (10 MB photos, 30 MB video), access rules per child | Golden rule (SQL only). No cloud scanning available; add ClamAV later if the nursery wants it. |
| Push notifications | FCM | In-app bell (live) + e-mail with retries. Push waits for the mobile app | No mobile app in the MVP. |
| Attendance partitioning, materialised views | Monthly partitions, mv_* | Plain indexed tables | Volumes of one nursery are small; revisit at scale (Phase 3). |
| Read receipts | Separate table | `LastReadAt` per participant | Same behaviour, simpler. |
| Audit retention | Not specified | Per-centre policy (default 7 years, never below 365 days), automatic daily purge + manual purge + CSV copy; the database trigger only allows deletion by the retention routine and never of entries younger than 365 days; purges are themselves audited | Owner request; keeps "append-only" meaningful. |
| Emergency PDF | QuestPDF | QuestPDF Community licence | Free for organisations under the licence revenue limit; check the limit as the company grows. |
| Deployment | Terraform, EKS, RDS | One Windows service next to SQL Server (`tools/package-pilot.ps1`, `install-service.ps1`, `backup-db.ps1`), the API serves the built web app | Pilot is on-site and SQL-Server-only; cloud hosting is a later decision (production file storage and data residency are still open). |
| AI Brain | Not in the spec | `IAiBrain` port with Groq `openai/gpt-oss-120b`; first feature: attendance summary from daily totals only | Owner choice; no personal data leaves the centre. Residency of any future personal-data feature needs legal sign-off. |
| Test users | n/a | ADR-0006 option A (local helper, development only) | Owner choice. |
| Demo data | n/a | `tools/load-demo-data.ps1` calls the real API; nothing is stored in the application or migrations | Golden rule: no seeded or hard-coded data. |
