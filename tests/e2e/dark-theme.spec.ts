import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })

async function useDarkTheme(page: Page) {
  await page.addInitScript(() => {
    if (window.localStorage.getItem('qaly-theme') === null) {
      window.localStorage.setItem('qaly-theme', 'dark')
    }
  })
}

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function expectDarkSurface(page: Page, selector: string) {
  const locator = page.locator(selector).first()
  if (selector !== 'body' && selector !== 'html') {
    await expect(locator).toBeVisible()
  }

  const color = await locator.evaluate(element => getComputedStyle(element).backgroundColor)
  const channels = color.match(/[\d.]+/g)?.map(Number) ?? []
  const [red = 255, green = 255, blue = 255, alpha = 1] = channels
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255

  expect(alpha, `${selector} must have a visible dark surface; received ${color}`).toBeGreaterThan(0.5)
  expect(luminance, `${selector} must be dark; received ${color}`).toBeLessThan(0.28)
}

async function findLargeLightSurfaces(page: Page) {
  return page.locator('body').evaluate(() => {
    const offenders: string[] = []

    for (const element of document.body.querySelectorAll<HTMLElement>('*')) {
      if (element.matches('img, svg, video, canvas') || element.closest('.auth-side-visual')) continue

      const rect = element.getBoundingClientRect()
      if (rect.width * rect.height < 16_000 || rect.bottom <= 0 || rect.top >= window.innerHeight) continue

      const style = getComputedStyle(element)
      if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0) continue

      const channels = style.backgroundColor.match(/[\d.]+/g)?.map(Number) ?? []
      const [red = 0, green = 0, blue = 0, alpha = 0] = channels
      const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255
      if (alpha < 0.8 || luminance < 0.72) continue

      const classes = [...element.classList].slice(0, 3).join('.')
      offenders.push(
        `${element.tagName.toLowerCase()}${classes ? `.${classes}` : ''} (${style.backgroundColor}, ${Math.round(rect.width)}x${Math.round(rect.height)})`,
      )
    }

    return offenders.slice(0, 12)
  })
}

test('dark theme is applied to auth pages before the app mounts', async ({ page }) => {
  await useDarkTheme(page)
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expectDarkSurface(page, '.login-page')
  await expectDarkSurface(page, '.login-form-shell')
  await expectDarkSurface(page, '.login-page .auth-form input:not([type="hidden"])')
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', '#0b1120')

  await page.goto('/Account/Register', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expectDarkSurface(page, '.login-page')
  await expectDarkSurface(page, '.login-form-shell')
  await expectDarkSurface(page, '.login-page .auth-form input:not([type="hidden"])')
})

test('theme toggle persists across reloads and updates browser metadata', async ({ page }) => {
  await useDarkTheme(page)
  await login(page)

  const toggle = page.locator('.shell-theme-toggle')
  await expect(toggle).toHaveAttribute('aria-label', /giao diện sáng/i)
  await toggle.click()

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', '#f8fafc')
  await expect.poll(() => page.evaluate(() => localStorage.getItem('qaly-theme'))).toBe('light')

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')

  await page.locator('.shell-theme-toggle').click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
})

test('primary authenticated routes keep their main surfaces dark', async ({ page }) => {
  await useDarkTheme(page)
  await login(page)

  const routes = [
    '/dashboard',
    '/projects',
    '/projects/archived',
    '/tasks',
    '/teams',
    '/analytics',
    '/profile',
    '/settings',
    '/organizations',
    '/organizations/users',
    '/admin/users',
    '/admin/moderators',
  ]

  for (const route of routes) {
    await test.step(route, async () => {
      await page.goto(route, { waitUntil: 'domcontentloaded' })
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
      await expect(page.locator('.shell-header')).toBeVisible()
      await expectDarkSurface(page, 'body')
      await expectDarkSurface(page, '.app-shell')
      await expectDarkSurface(page, '.shell-header')
      await expectDarkSurface(page, '.shell-sidebar')
      await expectDarkSurface(page, '.shell-main')
      await expect.poll(() => findLargeLightSurfaces(page), {
        message: `${route} contains a large light-colored surface while dark theme is active`,
      }).toEqual([])
    })
  }
})

test('project detail and shared overlays use dark surfaces', async ({ page }) => {
  await useDarkTheme(page)
  await login(page)

  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.projects-overview__cta')).toBeVisible()
  await page.locator('.projects-overview__cta').click()
  await expectDarkSurface(page, '.project-modal')
  await expect.poll(() => findLargeLightSurfaces(page)).toEqual([])

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.header-search')).toBeVisible()
  await page.locator('.header-search').click()
  await expectDarkSurface(page, '.global-search-panel')
  await expect.poll(() => findLargeLightSurfaces(page)).toEqual([])

  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  const projectCard = page.locator('.project-grid-card').first()
  if (await projectCard.count()) {
    await projectCard.click()
    await page.waitForURL(url => /^\/projects\/[0-9a-f-]{36}$/i.test(url.pathname))
    await expect(page.locator('.shell-header')).toBeVisible()
    await expect.poll(() => findLargeLightSurfaces(page), {
      message: 'Project detail contains a large light-colored surface while dark theme is active',
    }).toEqual([])
  }
})

test('dark theme remains intact on a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await useDarkTheme(page)
  await login(page)

  for (const route of ['/dashboard', '/projects', '/tasks', '/teams', '/settings']) {
    await test.step(route, async () => {
      await page.goto(route, { waitUntil: 'domcontentloaded' })
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
      await expect(page.locator('.shell-header')).toBeVisible()
      await expectDarkSurface(page, '.app-shell')
      await expectDarkSurface(page, '.shell-main')
      await expect.poll(() => findLargeLightSurfaces(page), {
        message: `${route} contains a large light-colored mobile surface while dark theme is active`,
      }).toEqual([])
    })
  }
})

test('group meeting and poll routes inherit the dark theme', async ({ page }) => {
  await useDarkTheme(page)
  await login(page)
  await page.goto('/teams', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.groups-workspace')).toBeVisible()

  await expect.poll(() => new URL(page.url()).pathname).toMatch(/^\/groups\/[0-9a-f-]{36}$/i)
  const groupId = new URL(page.url()).pathname.split('/')[2]

  await page.goto(`/groups/${groupId}/meeting`, { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.gm')).toBeVisible()
  await expect(page.locator('.gm')).not.toHaveClass(/gm--light/)
  await expectDarkSurface(page, '.gm-sidebar')
  await expect.poll(() => findLargeLightSurfaces(page), {
    message: 'Meeting route contains a large light-colored surface while dark theme is active',
  }).toEqual([])

  await page.goto(`/groups/${groupId}/polls`, { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.shell-header')).toBeVisible()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expect.poll(() => findLargeLightSurfaces(page), {
    message: 'Poll route contains a large light-colored surface while dark theme is active',
  }).toEqual([])
})
