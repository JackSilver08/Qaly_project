import { computed, readonly, ref } from 'vue'

export type QalyTheme = 'light' | 'dark'

export const themeStorageKey = 'qaly-theme'

const lightThemeColor = '#f8fafc'
const darkThemeColor = '#0b1120'

function isTheme(value: string | null | undefined): value is QalyTheme {
  return value === 'light' || value === 'dark'
}

function readStoredTheme(): QalyTheme | null {
  if (typeof window === 'undefined') return null

  try {
    const storedTheme = window.localStorage.getItem(themeStorageKey)
    return isTheme(storedTheme) ? storedTheme : null
  } catch {
    return null
  }
}

function readSystemTheme(): QalyTheme {
  if (typeof window === 'undefined') return 'light'
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function readDocumentTheme(): QalyTheme {
  if (typeof document === 'undefined') return 'light'
  const documentTheme = document.documentElement.dataset.theme
  return isTheme(documentTheme) ? documentTheme : readStoredTheme() ?? readSystemTheme()
}

const currentTheme = ref<QalyTheme>(readDocumentTheme())
let initialized = false

function updateThemeMetadata(theme: QalyTheme) {
  if (typeof document === 'undefined') return

  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme

  const themeColor = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
  if (themeColor) {
    themeColor.content = theme === 'dark' ? darkThemeColor : lightThemeColor
  }
}

export function applyTheme(theme: QalyTheme, persist = true) {
  currentTheme.value = theme
  updateThemeMetadata(theme)

  if (!persist || typeof window === 'undefined') return

  try {
    window.localStorage.setItem(themeStorageKey, theme)
  } catch {
    // Theme switching should still work when storage is unavailable.
  }
}

export function initializeTheme() {
  const initialTheme = readStoredTheme() ?? readDocumentTheme()
  applyTheme(initialTheme, false)

  if (initialized || typeof window === 'undefined') return
  initialized = true

  const systemTheme = window.matchMedia?.('(prefers-color-scheme: dark)')
  systemTheme?.addEventListener('change', (event) => {
    if (readStoredTheme() === null) {
      applyTheme(event.matches ? 'dark' : 'light', false)
    }
  })

  window.addEventListener('storage', (event) => {
    if (event.key !== themeStorageKey) return
    applyTheme(isTheme(event.newValue) ? event.newValue : readSystemTheme(), false)
  })
}

export function setTheme(theme: QalyTheme) {
  applyTheme(theme)
}

export function toggleTheme() {
  setTheme(currentTheme.value === 'dark' ? 'light' : 'dark')
}

export function useTheme() {
  const isDarkTheme = computed(() => currentTheme.value === 'dark')
  const themeButtonLabel = computed(() =>
    isDarkTheme.value ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối',
  )

  return {
    currentTheme: readonly(currentTheme),
    isDarkTheme,
    themeButtonLabel,
    setTheme,
    toggleTheme,
  }
}
