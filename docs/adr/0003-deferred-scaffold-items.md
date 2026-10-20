# ADR-0003: Scaffold deviations from spec 05 section 11
- shadcn/ui init is deferred to Sprint 1 (interactive, built together with the first screens).
- Spec command had typo "zustad"; correct package is "zustand". `@radix-ui/react-*` wildcard is installed per-component by shadcn.
- Web/mobile packages pinned exactly (no ^). Node 24 / pnpm 10 are used locally; CI to pin Node 20 LTS.
- Mobile uses `node-linker=hoisted` (required by React Native with pnpm).

## Sprint 1 deviations and deferrals
- Response types in the web app are hand-written (`src/api/types.ts`) because the API returns pre-wrapped responses; request types are generated from the OpenAPI document (`pnpm gen:api`). Adding `.Produces<>` metadata later would let responses be generated too.
- Not yet built: `Idempotency-Key` support, TOTP multi-factor sign-in, forgot-password and email-verification flows, parent invitation e-mail, audit-log viewer UI (Sprint 4), audit entry for reading decrypted fields (Sprint 4 emergency report), monthly partitioning of the audit log (needs the Sprint 2 job runner).
- Mapster is not used; handlers project with LINQ directly (faster with EF Core).
- Table and column names use EF Core defaults (see ADR-0004).
- Rate limits: 100/min per signed-in user, 10/min per anonymous address, 5/min for sign-in, 60/min for page-load session checks (all in configuration).
- Session refresh tolerates a duplicate use within a short grace window (configurable) so a reload or second tab is not mistaken for theft.
