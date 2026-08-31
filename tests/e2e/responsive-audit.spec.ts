import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'
import { resolveSeededProjectId } from './support/seeded-project'

/**
 * G-3 — responsive audit across the three breakpoints the team signed up to support.
 *
 * The rule this enforces is narrow on purpose: the page body must never scroll sideways. Wide
 * content (tables, kanban boards, timelines, code) is allowed to scroll, but inside its own
 * container. A body-level horizontal scrollbar means something escaped its column, which is the
 * failure people actually see on a phone.
 *
 * On failure the test names the offending elements so the fix does not need a bisect.
 */

const VIEWPORTS = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 390, height: 844 },
] as const

/** Routes owned by the four-person team. Project pages are audited separately — see below. */
const TEAM_ROUTES = [
  { path: '/dashboard', ready: '.shell-header' },
  { path: '/tasks', ready: '.shell-header' },
  { path: '/teams', ready: '.shell-header' },
  { path: '/analytics', ready: '.shell-header' },
  { path: '/settings', ready: '.shell-header' },
  { path: '/profile', ready: '.shell-header' },
  { path: '/organizations', ready: '.shell-header' },
  { path: '/organizations/users', ready: '.shell-header' },
  { path: '/admin/users', ready: '.shell-header' },
  { path: '/admin/moderators', ready: '.shell-header' },
  { path: '/projects/archived', ready: '.shell-header' },
] as const

type Overflow = {
  documentWidth: number
  viewportWidth: number
  offenders: { tag: string; cls: string; right: number; width: number; text: string }[]
}

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

/** Measures body-level horizontal overflow and names what is sticking out. */
async function measureOverflow(page: Page): Promise<Overflow> {
  return page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth
    const offenders: Overflow['offenders'] = []

    for (const node of Array.from(document.body.querySelectorAll<HTMLElement>('*'))) {
      const box = node.getBoundingClientRect()
      if (box.width === 0 || box.height === 0) continue
      // Only blame an element when it is not inside something that scrolls on purpose.
      let scrollableAncestor = false
      for (let parent = node.parentElement; parent && parent !== document.body; parent = parent.parentElement) {
        const overflowX = getComputedStyle(parent).overflowX
        if (overflowX === 'auto' || overflowX === 'scroll' || overflowX === 'hidden') {
          scrollableAncestor = true
          break
        }
      }
      if (scrollableAncestor) continue
      if (box.right <= viewportWidth + 1) continue

      offenders.push({
        tag: node.tagName.toLowerCase(),
        cls: typeof node.className === 'string' ? node.className.slice(0, 90) : '',
        right: Math.round(box.right),
        width: Math.round(box.width),
        text: (node.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 60),
      })
    }

    // Keep the widest few — a single escaping element usually drags its whole ancestor chain in.
    offenders.sort((left, right) => right.right - left.right)

    return {
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth,
      offenders: offenders.slice(0, 6),
    }
  })
}

function describe(route: string, viewport: string, overflow: Overflow) {
  const lines = overflow.offenders.map(
    item => `    <${item.tag} class="${item.cls}"> right=${item.right}px width=${item.width}px — "${item.text}"`,
  )
  return [
    `${route} @ ${viewport}: nội dung tràn ngang ${overflow.documentWidth - overflow.viewportWidth}px`,
    `  (scrollWidth ${overflow.documentWidth} > viewport ${overflow.viewportWidth})`,
    ...(lines.length ? ['  Phần tử tràn:', ...lines] : ['  Không xác định được phần tử cụ thể.']),
  ].join('\n')
}

for (const viewport of VIEWPORTS) {
  test(`G-3 không có tràn ngang ở ${viewport.name} (${viewport.width}px) — các trang nhóm phụ trách`, async ({ page }, testInfo) => {
    test.setTimeout(180_000)
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    await login(page)

    const failures: string[] = []

    for (const route of TEAM_ROUTES) {
      await page.goto(route.path, { waitUntil: 'domcontentloaded' })
      await page.locator(route.ready).first().waitFor({ state: 'visible', timeout: 20_000 }).catch(() => undefined)
      // Let lazy panels and charts settle before measuring.
      await page.waitForTimeout(700)

      const overflow = await measureOverflow(page)
      if (overflow.documentWidth > overflow.viewportWidth + 1) {
        failures.push(describe(route.path, viewport.name, overflow))
        await testInfo.attach(`overflow-${viewport.name}-${route.path.replace(/\//g, '_')}.png`, {
          body: await page.screenshot({ fullPage: false }),
          contentType: 'image/png',
        })
      }
    }

    expect(failures, `\n${failures.join('\n\n')}\n`).toEqual([])
  })
}

test('G-3 + G-6 trang Dự án và Chi tiết dự án — chỉ ghi nhận, không chặn build', async ({ page }, testInfo) => {
  // These two pages belong to Duy Hoàng. The team may report but must not fix their logic, so this
  // check reports findings as an attachment instead of failing the suite.
  test.setTimeout(180_000)
  await login(page)
  const projectId = await resolveSeededProjectId(page)

  const findings: string[] = []

  for (const viewport of VIEWPORTS) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })

    for (const path of ['/projects', `/projects/${projectId}`]) {
      await page.goto(path, { waitUntil: 'domcontentloaded' })
      await page.locator('.shell-header').first().waitFor({ state: 'visible', timeout: 20_000 }).catch(() => undefined)
      await page.waitForTimeout(700)

      const overflow = await measureOverflow(page)
      if (overflow.documentWidth > overflow.viewportWidth + 1) {
        findings.push(describe(path === '/projects' ? '/projects' : '/projects/{id}', viewport.name, overflow))
      }
    }
  }

  await testInfo.attach('duy-hoang-responsive-findings.txt', {
    body: Buffer.from(findings.length ? findings.join('\n\n') : 'Không phát hiện tràn ngang.', 'utf-8'),
    contentType: 'text/plain',
  })

  // Always green: this spec documents, it does not gate.
  expect(true).toBe(true)
})
