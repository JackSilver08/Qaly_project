import { defineConfig, devices } from '@playwright/test'

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5000'
const configuredWorkers = Number.parseInt(process.env.E2E_WORKERS ?? '4', 10)

if (!Number.isInteger(configuredWorkers) || configuredWorkers < 1) {
  throw new Error('E2E_WORKERS must be a positive integer.')
}

export default defineConfig({
  testDir: './tests/e2e',
  outputDir: 'test-results',
  timeout: 30_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: true,
  // One Qaly dev server is shared by the browser workers. Keep real parallelism,
  // but bound it so assistant polling, SignalR and in-memory persistence are not
  // starved by Playwright's CPU-derived default (12 workers on common dev hosts).
  workers: configuredWorkers,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI
    ? [['list'], ['github'], ['html', { open: 'never' }], ['json', { outputFile: 'test-results/e2e-results.json' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
