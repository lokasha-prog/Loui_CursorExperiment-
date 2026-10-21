import { defineConfig } from '@playwright/test'

/**
 * End-to-end checks in a real browser against a LOCAL running system (web on 5173 with the API behind it).
 * They use the local test-user buttons on the login page, so they never touch real accounts or passwords.
 * Run:  pnpm e2e
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  timeout: 60_000,
  retries: 1,
  workers: 1,
  reporter: [['list']],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
})
