# ADR-0006: Temporary test accounts on the login screen (PROPOSED, awaiting owner decision)

- Status: **Proposed. Not implemented.** The first attempt was stopped by the build tool's security check (2026-10-20).
- Owner request: while in development and testing, the login screen should always list a test account for each role, with the super administrator `salam@brightfuturs.com` and the password `Salam12` (all test accounts to use that password).
- Why it needs a decision: it conflicts with three standing rules.
  1. Golden rule: never seed data or hard-code values (this would create accounts at start-up and show credentials).
  2. Our own password policy requires at least 12 characters; `Salam12` has 7.
  3. A known super-administrator password shown to anyone who opens the login page is dangerous if the database or server is ever reachable by anyone else (this machine hosts other projects' databases on the same SQL Server).
- Safer options for the owner to choose from:
  - **A. Local-only helper (recommended):** the account list and one-click sign-in exist only in the local development build, read from a settings file that is never committed and is never served by the API. The server refuses to start with the feature on in any non-development setting. Accounts are created by an explicit developer command, not at every start-up. The password rule is relaxed only through a development setting.
  - **B. Strong passwords:** same panel, but generated 12+ character passwords instead of `Salam12`.
  - **C. Exactly as requested:** committed settings, always-on panel, `Salam12`. Requires the owner to approve the exception explicitly and accept the risk; must be removed before the pilot (Sprint 5 go-live checklist).
- Removal plan (any option): delete the `DevTestAccounts` section and panel before pilot go-live; tracked on the PMO risk list.
