import { beforeEach, describe, expect, it, vi } from 'vitest'

type MediaListener = (event: { matches: boolean }) => void

let systemPrefersDark = false
let mediaListeners: MediaListener[] = []

function stubMatchMedia() {
  mediaListeners = []
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: systemPrefersDark,
    media: query,
    onchange: null,
    addEventListener: (_type: string, listener: MediaListener) => mediaListeners.push(listener),
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  }))
}

/**
 * `use-theme` snapshots the document/storage state at import time, so each test seeds the
 * environment first and then loads a fresh module instance.
 */
async function loadTheme() {
  vi.resetModules()
  stubMatchMedia()
  return import('@/composables/use-theme')
}

beforeEach(() => {
  systemPrefersDark = false
  window.localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
  document.head.innerHTML = ''
})

function addThemeColorMeta() {
  const meta = document.createElement('meta')
  meta.name = 'theme-color'
  meta.content = '#ffffff'
  document.head.appendChild(meta)
  return meta
}

describe('initial theme resolution', () => {
  it('defaults to light when nothing is stored and the system prefers light', async () => {
    const { useTheme } = await loadTheme()
    expect(useTheme().currentTheme.value).toBe('light')
  })

  it('follows the system preference when nothing is stored', async () => {
    systemPrefersDark = true
    const { useTheme } = await loadTheme()
    expect(useTheme().currentTheme.value).toBe('dark')
  })

  it('prefers the theme already stamped on the document over the system preference', async () => {
    systemPrefersDark = true
    document.documentElement.dataset.theme = 'light'
    const { useTheme } = await loadTheme()
    expect(useTheme().currentTheme.value).toBe('light')
  })

  it('reads the stored theme when the document carries no marker', async () => {
    window.localStorage.setItem('qaly-theme', 'dark')
    const { useTheme } = await loadTheme()
    expect(useTheme().currentTheme.value).toBe('dark')
  })

  it('ignores a corrupted stored value', async () => {
    window.localStorage.setItem('qaly-theme', 'neon')
    const { useTheme } = await loadTheme()
    expect(useTheme().currentTheme.value).toBe('light')
  })
})

describe('applyTheme', () => {
  it('stamps the document, the color scheme and the theme-color meta tag', async () => {
    const meta = addThemeColorMeta()
    const { applyTheme } = await loadTheme()

    applyTheme('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
    expect(meta.content).toBe('#0b1120')

    applyTheme('light')
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(meta.content).toBe('#f8fafc')
  })

  it('persists by default and skips persistence when asked', async () => {
    const { applyTheme } = await loadTheme()

    applyTheme('dark')
    expect(window.localStorage.getItem('qaly-theme')).toBe('dark')

    window.localStorage.clear()
    applyTheme('light', false)
    expect(window.localStorage.getItem('qaly-theme')).toBeNull()
  })

  it('still switches the theme when localStorage throws', async () => {
    const { applyTheme, useTheme } = await loadTheme()
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })

    expect(() => applyTheme('dark')).not.toThrow()
    expect(useTheme().currentTheme.value).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')

    setItem.mockRestore()
  })
})

describe('toggleTheme', () => {
  it('flips between light and dark and persists the choice', async () => {
    const { toggleTheme, useTheme } = await loadTheme()
    const { currentTheme, isDarkTheme } = useTheme()

    expect(currentTheme.value).toBe('light')

    toggleTheme()
    expect(currentTheme.value).toBe('dark')
    expect(isDarkTheme.value).toBe(true)
    expect(window.localStorage.getItem('qaly-theme')).toBe('dark')

    toggleTheme()
    expect(currentTheme.value).toBe('light')
    expect(isDarkTheme.value).toBe(false)
    expect(window.localStorage.getItem('qaly-theme')).toBe('light')
  })
})

describe('themeButtonLabel', () => {
  it('names the theme the button switches to, not the current one', async () => {
    const { setTheme, useTheme } = await loadTheme()
    const { themeButtonLabel } = useTheme()

    setTheme('light')
    expect(themeButtonLabel.value).toBe('Chuyển sang giao diện tối')

    setTheme('dark')
    expect(themeButtonLabel.value).toBe('Chuyển sang giao diện sáng')
  })
})

describe('initializeTheme', () => {
  it('applies the stored theme without rewriting storage', async () => {
    window.localStorage.setItem('qaly-theme', 'dark')
    const { initializeTheme, useTheme } = await loadTheme()
    const setItem = vi.spyOn(Storage.prototype, 'setItem')

    initializeTheme()

    expect(useTheme().currentTheme.value).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(setItem).not.toHaveBeenCalled()
    setItem.mockRestore()
  })

  it('follows later system changes only while the user has made no explicit choice', async () => {
    const { initializeTheme, setTheme, useTheme } = await loadTheme()
    initializeTheme()

    mediaListeners.forEach((listener) => listener({ matches: true }))
    expect(useTheme().currentTheme.value).toBe('dark')

    setTheme('light')
    mediaListeners.forEach((listener) => listener({ matches: true }))
    expect(useTheme().currentTheme.value).toBe('light')
  })

  it('syncs the theme across tabs through the storage event', async () => {
    const { initializeTheme, useTheme } = await loadTheme()
    initializeTheme()

    window.dispatchEvent(new StorageEvent('storage', { key: 'qaly-theme', newValue: 'dark' }))
    expect(useTheme().currentTheme.value).toBe('dark')

    window.dispatchEvent(new StorageEvent('storage', { key: 'qaly-theme', newValue: null }))
    expect(useTheme().currentTheme.value).toBe('light')
  })

  it('ignores storage events for unrelated keys', async () => {
    const { initializeTheme, useTheme } = await loadTheme()
    initializeTheme()

    window.dispatchEvent(new StorageEvent('storage', { key: 'other-key', newValue: 'dark' }))
    expect(useTheme().currentTheme.value).toBe('light')
  })
})
