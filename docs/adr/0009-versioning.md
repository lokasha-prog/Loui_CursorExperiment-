# ADR-0009: One product version, semantic versioning

- Status: Accepted (2026-10-24)
- Context: The parts of the product carried unrelated numbers (web 0.0.0, mobile 1.0.0 and 1.2.0, API none), so nobody
  could tell which build a nursery was running or what changed between two installs.
- Decision:
  - The whole product (API, web app, mobile app, public website) has **one version**, kept only in `nms/VERSION`.
    Everything else reads it or is kept in step with it: the API assemblies at build time (`backend/Directory.Build.props`),
    the web app at build time (`__APP_VERSION__` in `web/vite.config.ts`), and the copies in `web/package.json`,
    `mobile/package.json`, `mobile/app.json` and `website/index.html`.
  - Versions are `MAJOR.MINOR.PATCH` ([Semantic Versioning](https://semver.org)), starting at **1.0.0** (the system as of 2026-10-24):
    - **MAJOR**: a change that breaks something already in use: an API call or field removed or changed in meaning,
      a database change that an older app cannot work with, or a step nurseries must take to upgrade.
    - **MINOR**: a new feature or screen that leaves existing use working (new endpoints, new optional fields, additive migrations).
    - **PATCH**: fixes and small improvements with no new feature.
  - Every delivered change raises the version, and only through `tools/version.ps1 -Bump major|minor|patch -Change "..."`,
    which also adds the release notes to `CHANGELOG.md` and raises the mobile build numbers. A version is never reused or lowered.
  - `tools/version.ps1 -Check` and the web test `src/version.test.ts` fail when any copy disagrees with `VERSION`
    or the changelog has no entry for it.
  - The running version is visible: `GET /api/v1/version` (no sign-in), and in the web app's side menu.
  - Once the code is in git, each release is committed as `Release x.y.z` and tagged `vx.y.z`.
- Consequences: A pilot install can be identified at a glance and compared with the changelog. The mobile app's store
  version follows the product version, so it moves in step even for releases that change only the web app.
