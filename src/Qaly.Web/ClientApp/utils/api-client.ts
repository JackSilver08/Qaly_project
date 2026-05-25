export interface ApiResult<T> {
  data: T | null
  error: string | null
  isSuccess: boolean
}

export async function apiJson<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)

  if (options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  headers.set('Accept', 'application/json')

  if (!options.method || options.method.toUpperCase() === 'GET') {
    options.cache = 'no-store'
  }

  const response = await fetch(url, {
    credentials: 'same-origin',
    ...options,
    headers,
  })

  if (response.status === 401) {
    window.location.href = `/Account/Login?returnUrl=${encodeURIComponent(window.location.pathname)}`
    throw new Error('Authentication required.')
  }

  const text = await response.text()
  const payload = parseApiPayload(text)

  if (!response.ok) {
    throw new Error(apiPayloadError(payload, response.status))
  }

  return payload as T
}

export async function apiResult<T>(url: string, options: RequestInit = {}): Promise<T> {
  const result = await apiJson<ApiResult<T> | T>(url, options)

  if (isApiResult<T>(result)) {
    if (!result.isSuccess || result.data == null) {
      throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.')
    }

    return result.data
  }

  return result as T
}

export async function apiCommand(url: string, options: RequestInit = {}) {
  const result = await apiJson<ApiResult<unknown> | { ok: boolean } | null>(url, options)

  if (isApiResult(result) && !result.isSuccess) {
    throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.')
  }

  if (result && typeof result === 'object' && 'ok' in result && (result as any).ok === false) {
    throw new Error('Không thể hoàn tất yêu cầu.')
  }
}

function parseApiPayload(text: string) {
  if (!text) return null

  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

function apiPayloadError(payload: unknown, status: number) {
  if (typeof payload === 'string' && payload.trim()) return payload.trim()

  if (payload && typeof payload === 'object') {
    if ('error' in payload && typeof (payload as any).error === 'string' && (payload as any).error.trim()) return (payload as any).error
    if ('message' in payload && typeof (payload as any).message === 'string' && (payload as any).message.trim()) return (payload as any).message
    if ('title' in payload && typeof (payload as any).title === 'string' && (payload as any).title.trim()) {
      const validationMessage = validationErrorMessage(payload)
      return validationMessage ?? (payload as any).title
    }
  }

  return `Không thể hoàn tất yêu cầu (mã ${status}).`
}

function validationErrorMessage(payload: object) {
  if (!('errors' in payload) || !(payload as any).errors || typeof (payload as any).errors !== 'object') return null

  for (const value of Object.values((payload as any).errors)) {
    if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
    if (typeof value === 'string') return value
  }

  return null
}

function isApiResult<T>(payload: unknown): payload is ApiResult<T> {
  return Boolean(payload && typeof payload === 'object' && 'isSuccess' in payload)
}

export function errorMessage(error: unknown, fallback = 'Đã xảy ra lỗi khi lưu dữ liệu') {
  const message = error instanceof Error ? error.message.trim() : ''

  if (
    !message ||
    message === 'Request failed.' ||
    message.startsWith('Request failed with status') ||
    message.startsWith('Không thể hoàn tất yêu cầu')
  ) {
    return fallback
  }

  return message
}
