import { chromium } from '@playwright/test';
const baseURL='http://localhost:5005';
const browser=await chromium.launch(); const context=await browser.newContext({viewport:{width:1366,height:768}}); const page=await context.newPage();
page.on('console', msg => console.log('console', msg.type(), msg.text()));
page.on('pageerror', err => console.log('pageerror', err.message));
await page.goto(`${baseURL}/Account/Login`, {waitUntil:'domcontentloaded'}); await page.locator('input[name="Email"]').fill('admin@qaly.dev'); await page.locator('input[name="Password"]').fill('Admin@123456'); await Promise.all([page.waitForURL(u=>!u.pathname.startsWith('/Account/Login'),{timeout:30000}), page.locator('#loginForm button[type="submit"]').click()]);
const res=await page.request.get(`${baseURL}/api/dashboard/overview`); const data=await res.json(); const id=data.projects[0].id; console.log('id',id); await page.goto(`${baseURL}/projects/${id}`, {waitUntil:'domcontentloaded'}); await page.waitForTimeout(3000); console.log('url',page.url()); console.log(await page.locator('body').innerText({timeout:5000}).catch(e=>e.message)); await page.screenshot({path:'artifacts/screenshots/ui-audit/debug-detail.png', fullPage:true}); await browser.close();
