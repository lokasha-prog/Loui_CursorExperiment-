# ADR-0008: Do not split out the notification or media services yet

Status: accepted 2026-10-21 (Sprint 25 decision gate). Revisit at each quarter or when a trigger below fires.

## Question
Should the alert sender (notifications) and the media handling become separate services (spec 19, Sprint 25), or stay inside the one NMS application?

## Triggers set by the spec
- Alerts: split if the waiting queue is **consistently above 1,000** or alert delay is **above 5 seconds at the 95th percentile**.
- Media: split if scanning takes **above 30 seconds at the 95th percentile** or the false-positive rate is **above 5%**.

## What we measured (2026-10-21, one 8-core PC that also ran SQL Server and the test tool)
| Measure | Result | Trigger |
|---|---|---|
| Screen queries, 500 people with a realistic pause (0-20 s) | 0 errors; 95th percentile 0.03-0.05 s; 99th percentile 0.03-0.10 s | target 99th under 0.5 s: **met** |
| Same, 500 people with no pause at all (about 47,000 requests in a row) | 0 errors; 95th percentile 0.9-4 s | extreme stress, not a real day |
| Alert sending | one batch every interval (default 15 min) now takes up to 500 waiting alerts (was 50); in-app alerts are written at once and need no queue | queue never near 1,000 in tests |
| Media | files are checked by content when uploaded (type sniffing, size limit 30 MB) inside the request; no scanner service exists, so scan latency and false positives are not applicable | n/a |
| New measuring point | `GET /api/v1/system/queues` (managers) shows alerts waiting, oldest wait in minutes, failures and sends in 24 h, reports waiting, media files and megabytes | use it for the next review |

## Decision
Keep notifications and media inside the application. A split would add a second thing to install, monitor and back up at every nursery (and each on-site customer has one computer), with no measured problem to fix. Instead:
- Batch size raised to 500 and the run interval is a setting (`Jobs:IntervalMinutes`).
- The queue measure above is checked weekly in the pilot and monthly after.
- Phone alerts already go through an outside push service; e-mail through the customer's mail server: those are the slow parts and they are already separate.

## Triggers to revisit
Alerts waiting above 1,000 on three consecutive checks, oldest alert waiting above 30 minutes, any centre above 1,500 children, a virus scanner added for uploads (then media becomes a candidate), or a SaaS deployment with more than 50 centres on one server.
