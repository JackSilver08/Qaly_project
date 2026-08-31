import { chromium } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5005';
const outDir = resolve('artifacts/screenshots/ui-audit');
mkdirSync(outDir, { recursive: true });

const viewports = [
  { name: '1366x768', width: 1366, height: 768 },
  { name: '1536x864', width: 1536, height: 864 },
  { name: '1920x1080', width: 1920, height: 1080 },
  { name: '1280x720', width: 1280, height: 720 },
  { name: 'mobile-390x844', width: 390, height: 844 },
];

const tabs = [
  ['stats', 'Thống kê'],
  ['roadmap', 'Lộ Trình Dự Án'],
  ['tasks', 'Nhiệm vụ'],
  ['capacity', 'Phân công & Capacity'],
  ['activity', 'Hoạt động'],
  ['members', 'Thành viên'],
  ['wiki', 'Wiki'],
  ['github', 'GitHub'],
  ['webhooks', 'Webhook'],
];

const issues = [];
const events = [];

function pushIssue(scope, severity, issue, details = '') {
  issues.push({ scope, severity, issue, details });
}

async function login(page) {
  await page.goto(`${baseURL}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.locator('#loginForm').waitFor({ state: 'visible', timeout: 20000 });
  await page.locator('input[name="Email"]').fill('admin@qaly.dev');
  await page.locator('input[name="Password"]').fill('Admin@123456');
  await Promise.all([
    page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), {
      timeout: 30000,
      waitUntil: 'domcontentloaded',
    }),
    page.locator('#loginForm button[type="submit"]').click(),
  ]);
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => undefined);
}

async function measure(page, scope) {
  const result = await page.evaluate(() => {
    const doc = document.documentElement;
    const body = document.body;
    const viewportWidth = window.innerWidth;
    const offenders = [...document.querySelectorAll('body *')]
      .map((el) => {
        const rect = el.getBoundingClientRect();
        const style = window.getComputedStyle(el);
        return {
          tag: el.tagName.toLowerCase(),
          cls: typeof el.className === 'string' ? el.className.slice(0, 120) : '',
          id: el.id,
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          width: Math.round(rect.width),
          position: style.position,
        };
      })
      .filter((item) => item.width > 0 && (item.right > viewportWidth + 2 || item.left < -2))
      .slice(0, 8);

    const emojiTexts = [...document.querySelectorAll('body *')]
      .filter((el) => el.children.length === 0)
      .map((el) => el.textContent?.trim() ?? '')
      .filter((text) => /[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(text))
      .slice(0, 20);

    return {
      viewportWidth,
      scrollWidth: Math.max(doc.scrollWidth, body.scrollWidth),
      offenders,
      emojiTexts,
      dialogs: [...document.querySelectorAll('[role="dialog"], [role="alertdialog"], .project-modal, .task-modal, .import-modal, .ai-planner-modal, .roadmap-modal-shell, .capacity-modal')]
        .map((el) => {
          const rect = el.getBoundingClientRect();
          return {
            cls: typeof el.className === 'string' ? el.className.slice(0, 120) : '',
            top: Math.round(rect.top),
            left: Math.round(rect.left),
            right: Math.round(rect.right),
            bottom: Math.round(rect.bottom),
            width: Math.round(rect.width),
            height: Math.round(rect.height),
            viewportWidth,
            viewportHeight: window.innerHeight,
          };
        }),
    };
  });

  events.push({ type: 'measure', scope, ...result });
  if (result.scrollWidth > result.viewportWidth + 2) {
    pushIssue(scope, 'P1', 'Horizontal overflow', `scrollWidth=${result.scrollWidth}, viewport=${result.viewportWidth}, offenders=${JSON.stringify(result.offenders)}`);
  }
  if (result.emojiTexts.length) {
    pushIssue(scope, 'P2', 'Emoji rendered in audited UI surface', result.emojiTexts.join(' | '));
  }
  for (const dialog of result.dialogs) {
    if (dialog.left < -2 || dialog.right > dialog.viewportWidth + 2 || dialog.top < -2 || dialog.bottom > dialog.viewportHeight + 2) {
      pushIssue(scope, 'P1', 'Dialog exceeds viewport', JSON.stringify(dialog));
    }
  }
  return result;
}

async function screenshot(page, name) {
  await page.screenshot({ path: resolve(outDir, name), fullPage: true });
}

async function main() {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1366, height: 768 }, ignoreHTTPSErrors: true });
  const page = await context.newPage();

  page.on('console', (msg) => {
    if (['error'].includes(msg.type())) {
      events.push({ type: 'console', level: msg.type(), text: msg.text() });
    }
  });
  page.on('pageerror', (error) => {
    pushIssue('runtime', 'P1', 'Page error', error.message);
  });

  await login(page);
  await page.goto(`${baseURL}/projects`, { waitUntil: 'domcontentloaded' });
  await page.locator('.shell-header').waitFor({ state: 'visible', timeout: 20000 });
  await page.locator('.project-workspace').waitFor({ state: 'visible', timeout: 20000 });
  await measure(page, 'projects-list-1366x768');
  await screenshot(page, '01-project-list.png');

  await page.getByPlaceholder(/Tìm/i).first().fill('qaly');
  await measure(page, 'projects-search-filter');
  await page.getByPlaceholder(/Tìm/i).first().fill('');

  await page.getByRole('button', { name: /Tạo dự án/i }).first().click();
  await page.locator('.project-modal').waitFor({ state: 'visible', timeout: 10000 });
  await measure(page, 'project-create-modal');
  await screenshot(page, '05-project-modal.png');
  await page.locator('.project-modal button[type="submit"]').click({ force: true });
  await measure(page, 'project-create-validation');
  await screenshot(page, '06-project-form-validation.png');
  await page.keyboard.press('Escape').catch(() => undefined);
  if (await page.locator('.project-modal').isVisible().catch(() => false)) {
    await page.locator('.project-modal .icon-button').click();
  }

  const dashboardResponse = await page.request.get(`${baseURL}/api/dashboard/overview`);
  const dashboardPayload = await dashboardResponse.json();
  const firstProjectId =
    dashboardPayload?.data?.projects?.[0]?.id ??
    dashboardPayload?.projects?.[0]?.id;
  if (!firstProjectId) throw new Error('No seeded project id found in dashboard payload.');
  await page.goto(`${baseURL}/projects/${firstProjectId}`, { waitUntil: 'domcontentloaded' });
  await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
  const projectUrl = page.url().split('?')[0];
  await measure(page, 'project-detail-stats-1366x768');
  await screenshot(page, '02-project-detail-overview.png');

  for (const [id, label] of tabs) {
    await page.goto(id === 'stats' ? projectUrl : `${projectUrl}?tab=${id}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
    await page.getByRole('button', { name: label }).waitFor({ state: 'visible', timeout: 10000 }).catch(() => undefined);
    await page.waitForTimeout(750);
    await measure(page, `project-detail-${id}-1366x768`);
    if (id === 'tasks') await screenshot(page, '03-project-detail-tasks.png');
    if (id === 'roadmap') await screenshot(page, '04-project-detail-roadmap.png');
    if (id === 'github') await screenshot(page, '07-project-icons.png');
  }

  await page.goto(`${projectUrl}?tab=tasks`, { waitUntil: 'domcontentloaded' });
  await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
  await page.getByRole('button', { name: /Nhiệm vụ$/i }).click();
  await page.locator('.task-modal').waitFor({ state: 'visible', timeout: 10000 });
  await measure(page, 'task-create-modal');
  await page.locator('.task-modal button[type="submit"]').click({ force: true });
  await measure(page, 'task-create-validation');
  await page.keyboard.press('Escape');

  await page.goto(`${projectUrl}?tab=roadmap`, { waitUntil: 'domcontentloaded' });
  await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
  const roadmapButtons = page.locator('button');
  const roadmapButtonCount = await roadmapButtons.count();
  for (let i = 0; i < roadmapButtonCount; i++) {
    const text = (await roadmapButtons.nth(i).innerText().catch(() => '')).trim();
    if (/mốc|milestone|tạo|thêm/i.test(text)) {
      await roadmapButtons.nth(i).click().catch(() => undefined);
      if (await page.locator('.roadmap-modal-shell, .preset-modal, .confirmation-modal').first().isVisible().catch(() => false)) {
        await measure(page, 'roadmap-modal');
        await page.keyboard.press('Escape').catch(() => undefined);
        break;
      }
    }
  }

  for (const vp of viewports) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.goto(`${projectUrl}?tab=tasks`, { waitUntil: 'domcontentloaded' });
    await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
    await page.waitForTimeout(500);
    await measure(page, `responsive-tasks-${vp.name}`);
    await page.goto(`${projectUrl}?tab=roadmap`, { waitUntil: 'domcontentloaded' });
    await page.locator('.project-tabs').waitFor({ state: 'visible', timeout: 20000 });
    await page.waitForTimeout(500);
    await measure(page, `responsive-roadmap-${vp.name}`);
  }

  await browser.close();
  writeFileSync(resolve('artifacts/ui-audit/project-ui-audit-results.json'), JSON.stringify({ baseURL, tabs, issues, events }, null, 2));
  console.log(JSON.stringify({ issueCount: issues.length, issues }, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
