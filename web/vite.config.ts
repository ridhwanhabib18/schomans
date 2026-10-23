import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: { port: 5173, strictPort: true },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['./src/test/setup.ts'],
    reporters: ['default', 'junit'],
    outputFile: { junit: './test-results/vitest-junit.xml' },
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      // Generated client, test helpers, bootstrap and type-only files are not application logic.
      exclude: ['src/api/generated/**', 'src/test/**', 'src/**/*.test.{ts,tsx}', 'src/main.tsx', 'src/**/*.d.ts'],
      reporter: ['text-summary', 'html', 'cobertura'],
      reportsDirectory: './coverage',
    },
  },
})
