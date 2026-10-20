# NMS — Bright Futures

Nursery management for UAE early childhood centres. English and Arabic (left-to-right and right-to-left).
Specs: `docs/prerequisites` · Mock-ups and PMO dashboard: `docs/screens` · Decisions: `docs/adr`.

## Golden rules
Data lives only in the local Microsoft SQL Server. No seeded data, no hard-coded values (ADR-0004).

## Run it locally
1. Supporting services (cache, queue, file store, test mailbox): `cd infra/docker`, copy `.env.example` to `.env`, fill it, `docker compose up -d`.
2. API settings (kept in your user secrets, never in the repo), from `backend/src/NMS.Api`:
   `dotnet user-secrets set "ConnectionStrings:Nms" "<SQL Server connection string>"`, then `Jwt:SigningKey` (base64, 48 bytes), `Encryption:Key` (base64, 32 bytes), `Auth:BootstrapToken` (any long random text), `Database:MigrateOnStartup` = `true`.
3. API: `dotnet run --project backend/src/NMS.Api` (listens on the address in `ASPNETCORE_URLS`).
4. Web: `cd web && pnpm install && pnpm dev` (the API address is `VITE_API_PROXY_TARGET` in `web/.env.development`).
5. Open the web address, choose "Set up your centre", and enter your setup key (`Auth:BootstrapToken`) to create the first administrator.

## Tests
- Backend: `dotnet test backend/NMS.sln` (integration tests need `NMS_TEST_CONNECTION_STRING`, a SQL Server connection string without a database name; each run creates and deletes its own database).
- Web: `cd web && pnpm test` (`pnpm test:coverage`, `pnpm lint`, `pnpm typecheck`).
