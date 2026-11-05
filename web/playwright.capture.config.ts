import { defineConfig, devices } from '@playwright/test'

// `npm run capture:screenshots` visits every page of every role and stores the pictures in docs/assets/screenshots/.
// It needs the whole stack to run. Point it at another address with CAPTURE_URL (the demo URL after a deploy); when the
// variable is not set, the local API and web app are started (or reused) like in the end-to-end tests.
const target = process.env.CAPTURE_URL
const local = !target

export default defineConfig({
  testDir: './capture',
  timeout: 600_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  outputDir: './test-results/capture',
  use: {
    baseURL: target ?? 'http://localhost:5173',
    ...devices['Desktop Chrome'],
    viewport: { width: 1440, height: 900 },
    actionTimeout: 30_000,
    // Self-signed certificates of a local stack behind a proxy are accepted; a real demo URL has a real one.
    ignoreHTTPSErrors: true,
  },
  webServer: local
    ? [
        {
          command: 'dotnet run --project ../src/SchoMans.Api --no-launch-profile --urls http://localhost:5080',
          url: 'http://localhost:5080/health/live',
          // A database of its own (created and seeded on start), so the pictures never show what the tests of the phases left behind.
          // Same server and development password as appsettings.Development.json.
          env: {
            ASPNETCORE_ENVIRONMENT: 'Development',
            ConnectionStrings__Default:
              process.env.CAPTURE_DB ??
              'Server=127.0.0.1,1433;Database=SchoMansCapture;User Id=sa;Password=SchoMans_Dev#2026;TrustServerCertificate=True;Encrypt=False',
          },
          reuseExistingServer: true,
          timeout: 180_000,
        },
        { command: 'npm run dev', url: 'http://localhost:5173', reuseExistingServer: true, timeout: 120_000 },
      ]
    : undefined,
})
