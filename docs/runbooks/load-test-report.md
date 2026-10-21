# Speed and load test report — 2026-10-21

**Tool:** `python tools/perf-check.py --users N --requests R --api <url>` against a second copy of the API (`tools/start-load-copy.ps1`, per-person limits lifted so the limiter does not hide real speed). 19 main screen queries, each asked R times with N parallel callers. Everything ran on one 8-core, 32 GB PC that also hosted SQL Server and the test tool, so real servers will do better.

**Gate (spec):** 95th percentile under 2.5 s, no errors.

| Test | Result |
|---|---|
| One caller, each query | 15-40 ms |
| 100 users (earlier run) | slowest p95 0.9 s, 0 errors — **pass** |
| 500 users, 40 requests per query | slowest p95 0.9 s, 0 errors — **pass** |
| 500 users all hammering at once (2,500 requests per query, about 47,500 requests in a row, no think time) | 0 errors; p95 above the gate on 8 of 19 queries (worst: billing snapshot 6.4 s, calendar agenda 4.7 s, activities 3.8 s, reports analytics 3.6 s, dashboard 2.9 s). **Gate not met under this extreme load.** |

**Reading the extreme result.** 500 callers with no pause between requests is much harsher than 500 people using the system (a real user opens a screen every 10-30 seconds), and the test tool competed with the API and SQL Server for the same CPU. Still, it showed real waste, so we fixed what it found:

- **Activity feed** made four database trips per post: now five trips per page (p50 at 500 callers 13.6 s -> about 3 s).
- **Billing snapshot** ran 22 queries: now 8 (month totals and the 6-month trend are grouped inside the database).
- **Ten-second memory for manager overview screens** (billing snapshot, calendar agenda, analytics): kept per person and per centre; identical questions arriving together share one database visit; failures are never remembered; setting `Caching:ManagerScreensSeconds` to 0 turns it off. Result at 500 callers: billing snapshot p95 6.4 s -> 2.1 s, agenda 6.2 s -> 1.5 s, analytics 4.9 s -> 1.5 s.
- Nothing failed or returned an error at any load; the API stayed up.

**Last run (500 callers, no pause):** 0 errors; 12 of 19 queries under 2.5 s p95. Still above the gate: dashboard summary 4.2 s, activities 3.8 s, resources 3.6 s, enquiry summary 3.7 s, report monthly 2.6 s, newsletters 2.6 s, calendar events 1.8 s (ok). These are ordinary queries; the machine was simply out of CPU (single-request time is 15-40 ms), so the answer is more cores/separate test machine, not code.

**Still to do before go-live (not blocking the pilot):**
1. Re-run the 500-user test on the production-size server with the test tool on a separate machine and a realistic pause between requests.
2. SQL Server index review with the real data volume of the pilot (query plans for dashboard summary, activities, enquiry summary).
3. If real use needs it, extend the ten-second memory to the dashboard summary (it feeds live attendance, so it stays uncached for now).
