import { defineConfig, devices } from '@playwright/test'

const configuredBaseURL = process.env.E2E_BASE_URL?.trim()
const shouldStartLocalDatabaseServer = process.env.E2E_START_LOCAL_DATABASE_SERVER === 'true'

if (!configuredBaseURL) {
  throw new Error(
    'Demo thực tế yêu cầu E2E_BASE_URL. Ví dụ PowerShell: '
    + '$env:E2E_BASE_URL="https://staging.qaly.example"',
  )
}

let baseURL: string
try {
  const target = new URL(configuredBaseURL)
  if (!['http:', 'https:'].includes(target.protocol)) throw new Error('unsupported protocol')
  if (target.hostname === 'dia-chi-moi-truong-demo') {
    throw new Error('example placeholder')
  }
  baseURL = target.toString().replace(/\/$/, '')
} catch {
  throw new Error(
    `E2E_BASE_URL chưa phải địa chỉ môi trường thật: ${configuredBaseURL}. `
    + 'Hãy thay bằng URL đang truy cập được, ví dụ https://staging.qaly.vn.',
  )
}

if (process.env.E2E_ADMIN_EMAIL?.trim() === 'tai-khoan-demo'
  || process.env.E2E_ADMIN_PASSWORD?.trim() === 'mat-khau-demo') {
  throw new Error(
    'E2E_ADMIN_EMAIL/E2E_ADMIN_PASSWORD vẫn là giá trị minh họa. '
    + 'Hãy nhập tài khoản thật của môi trường demo.',
  )
}

const slowMo = Number.parseInt(process.env.E2E_DEMO_SLOW_MO ?? '0', 10)
if (!Number.isInteger(slowMo) || slowMo < 0) {
  throw new Error('E2E_DEMO_SLOW_MO phải là số nguyên không âm (milliseconds).')
}

export default defineConfig({
  testDir: './tests/e2e',
  testMatch: 'demo-script-30-minutes.spec.ts',
  outputDir: 'test-results/demo-visual',
  timeout: 600_000,
  expect: {
    timeout: 15_000,
  },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report/demo', open: 'never' }],
    ['json', { outputFile: 'test-results/demo-visual-results.json' }],
  ],
  webServer: shouldStartLocalDatabaseServer
    ? {
        command: `dotnet run --project src/Qaly.Web --no-launch-profile --urls "${new URL(baseURL).origin}"`,
        url: new URL('/Account/Login', baseURL).toString(),
        reuseExistingServer: false,
        timeout: 180_000,
        env: {
          ASPNETCORE_ENVIRONMENT: 'Development',
          UseInMemoryDatabase: 'false',
        },
      }
    : undefined,
  use: {
    ...devices['Desktop Chrome'],
    baseURL,
    viewport: { width: 1440, height: 900 },
    colorScheme: 'light',
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'on',
    launchOptions: { slowMo },
  },
})
