import { defineConfig, devices } from '@playwright/test'

const isCi = !!process.env.CI

// End-to-end tests run against the real stack: SQL Server and Keycloak (docker compose), the API and the web app.
// Locally the servers below are reused when they already run; in CI they are started here.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: isCi ? 1 : 0,
  forbidOnly: isCi,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['junit', { outputFile: 'test-results/playwright-junit.xml' }],
  ],
  use: {
    baseURL: 'http://localhost:5173',
    // Visual evidence of every step, stored per phase in docs/assets/testing/phase-<n>/.
    screenshot: 'on',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } },
    {
      name: 'mobile',
      // A phone has a 390 px wide screen (PRD: screens from 360 px); this runs the layout checks only.
      use: { ...devices['Pixel 5'], viewport: { width: 390, height: 844 } },
      testMatch: /layout\.spec\.ts/,
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../src/SchoMans.Api --no-launch-profile --urls http://localhost:5080',
      url: 'http://localhost:5080/health/live',
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
      reuseExistingServer: !isCi,
      timeout: 180_000,
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: !isCi,
      timeout: 120_000,
    },
  ],
})
