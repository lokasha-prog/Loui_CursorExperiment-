# ADR-0005: Sign-in is built into NMS (replaces Keycloak)
- Status: Accepted (owner decision, 2026-10-20). Supersedes the Keycloak choice in specs 05, 09, 10.
- Context: the owner's SQL Server accepts Windows logins only; Keycloak runs in a container and needs a SQL login, and any other Keycloak store would break ADR-0004.
- Decision: NMS authenticates users itself. Users, password hashes, lockout state and refresh tokens live in SQL Server. The API issues short-lived JWT access tokens (15 min) and rotating refresh tokens (7 days, HttpOnly cookie, reuse detection). Roles and the permission matrix from spec 09 are unchanged. The first administrator is created through a one-time bootstrap action, not a seed.
- Consequences: no single sign-on across centres and no directory (LDAP/AD) federation until built later; MFA (TOTP) moves to a later sprint. Revisit for Phase 3 multi-centre.
