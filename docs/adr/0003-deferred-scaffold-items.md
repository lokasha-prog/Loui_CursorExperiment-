# ADR-0003: Scaffold deviations from spec 05 section 11
- shadcn/ui init is deferred to Sprint 1 (interactive, built together with the first screens).
- Spec command had typo "zustad"; correct package is "zustand". `@radix-ui/react-*` wildcard is installed per-component by shadcn.
- Web/mobile packages pinned exactly (no ^). Node 24 / pnpm 10 are used locally; CI to pin Node 20 LTS.
- Mobile uses `node-linker=hoisted` (required by React Native with pnpm).
