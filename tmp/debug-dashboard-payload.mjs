import { chromium } from '@playwright/test';

const baseURL = 'http://localhost:5005';
const browser = await chromium.launch();
const context = await browser.newContext();
const page = await context.newPage();
await page.goto(`${baseURL}/Account/Login`, { waitUntil: 'domcontentloaded' });
await page.locator('input[name="Email"]').fill('admin@qaly.dev');
await page.locator('input[name="Password"]').fill('Admin@123456');
await Promise.all([
  page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), { timeout: 30000 }),
  page.locator('#loginForm button[type="submit"]').click(),
]);
const res = await page.request.get(`${baseURL}/api/dashboard/overview`, { failOnStatusCode: false });
console.log('status', res.status());
const text = await res.text();
console.log(text.slice(0, 2000));
await browser.close();
