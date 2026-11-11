import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // In development the API runs separately; same-origin /api calls avoid CORS.
    proxy: { '/api': 'http://localhost:5125' },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    // Pin the zone so date tests mean the same thing on every machine and in CI.
    env: { TZ: 'America/New_York', VITE_API_URL: 'http://localhost' },
  },
})
