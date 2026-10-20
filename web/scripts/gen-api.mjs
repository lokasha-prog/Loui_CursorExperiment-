// Regenerates src/api/schema.d.ts from the running API's OpenAPI document (spec 12: the client is generated, never hand-written).
// The API address comes from .env.development (VITE_API_PROXY_TARGET) or from the environment.
import { spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'

if (existsSync('.env.development')) {
  process.loadEnvFile('.env.development')
}

const target = process.env.VITE_API_PROXY_TARGET
if (!target) {
  throw new Error('Set VITE_API_PROXY_TARGET (see .env.development) to the API address.')
}

const result = spawnSync('pnpm', ['exec', 'openapi-typescript', `${target}/openapi/v1.json`, '-o', 'src/api/schema.d.ts'], {
  stdio: 'inherit',
  shell: true,
})
process.exit(result.status ?? 1)
