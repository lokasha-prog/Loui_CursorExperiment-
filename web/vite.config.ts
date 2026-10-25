import fs from 'node:fs'
import path from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [react()],
    // The product version lives only in nms/VERSION (ADR-0009).
    define: { __APP_VERSION__: JSON.stringify(fs.readFileSync(path.resolve(__dirname, '../VERSION'), 'utf8').trim()) },
    resolve: { alias: { '@': path.resolve(__dirname, 'src') } },
    server: {
      // The API address comes from configuration (.env files), never from code.
      proxy: env.VITE_API_PROXY_TARGET
        ? {
            '/api': { target: env.VITE_API_PROXY_TARGET, changeOrigin: false },
            '/hubs': { target: env.VITE_API_PROXY_TARGET, changeOrigin: false, ws: true },
          }
        : undefined,
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      coverage: {
        provider: 'v8',
        include: ['src/**/*.{ts,tsx}'],
        exclude: ['src/**/*.d.ts', 'src/**/*.test.{ts,tsx}', 'src/main.tsx', 'src/test/**', 'src/api/schema.d.ts', 'src/api/types.ts', 'src/components/ui/**'],
      },
    },
  }
})
