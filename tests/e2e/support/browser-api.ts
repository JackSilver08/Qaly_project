import type { Page } from '@playwright/test'

export type BrowserApiResponse = {
  ok(): boolean
  status(): number
  url(): string
  json(): Promise<unknown>
  text(): Promise<string>
}

type BrowserApiOptions = {
  data?: unknown
  headers?: Record<string, string>
}

export async function browserApiRequest(
  page: Page,
  method: string,
  url: string,
  options: BrowserApiOptions = {},
): Promise<BrowserApiResponse> {
  const result = await page.evaluate(
    async ({ requestMethod, requestUrl, requestData, requestHeaders }) => {
      const headers = new Headers(requestHeaders)
      const normalizedMethod = requestMethod.toUpperCase()

      if (!['GET', 'HEAD', 'OPTIONS'].includes(normalizedMethod) && !headers.has('X-CSRF-TOKEN')) {
        const csrfResponse = await fetch('/api/security/csrf', {
          credentials: 'same-origin',
        })
        if (!csrfResponse.ok) {
          return {
            body: await csrfResponse.text(),
            ok: false,
            status: csrfResponse.status,
            url: csrfResponse.url,
          }
        }

        const csrfPayload = await csrfResponse.json() as { token?: string }
        if (!csrfPayload.token) {
          throw new Error('CSRF endpoint did not return a request token.')
        }
        headers.set('X-CSRF-TOKEN', csrfPayload.token)
      }

      let body: string | undefined
      if (requestData !== undefined) {
        headers.set('Content-Type', 'application/json')
        body = JSON.stringify(requestData)
      }

      const response = await fetch(requestUrl, {
        method: normalizedMethod,
        headers,
        body,
        credentials: 'same-origin',
      })

      return {
        body: await response.text(),
        ok: response.ok,
        status: response.status,
        url: response.url,
      }
    },
    {
      requestMethod: method,
      requestUrl: url,
      requestData: options.data,
      requestHeaders: options.headers ?? {},
    },
  )

  return {
    ok: () => result.ok,
    status: () => result.status,
    url: () => result.url,
    json: async () => JSON.parse(result.body) as unknown,
    text: async () => result.body,
  }
}
