import { beforeEach, describe, expect, it, vi } from 'vitest'

type FetchArgs = [input: RequestInfo | URL, init?: RequestInit]

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    text: async () => (typeof body === 'string' ? body : JSON.stringify(body)),
  } as unknown as Response
}

function textResponse(body: string, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => JSON.parse(body),
    text: async () => body,
  } as unknown as Response
}

/**
 * `api-client` memoises the CSRF token in a module-level promise, so every test loads a fresh
 * copy of the module to keep the token cache from leaking between cases.
 */
async function loadClient() {
  vi.resetModules()
  return import('@/utils/api-client')
}

let fetchMock: ReturnType<typeof vi.fn>

beforeEach(() => {
  fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
})

function headersOf(call: FetchArgs) {
  return new Headers(call[1]?.headers)
}

describe('apiJson', () => {
  it('sends GET without a CSRF token and disables the HTTP cache', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiJson('/api/projects')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as FetchArgs
    expect(url).toBe('/api/projects')
    expect(init?.credentials).toBe('same-origin')
    expect(init?.cache).toBe('no-store')
    expect(headersOf(fetchMock.mock.calls[0] as FetchArgs).has('X-CSRF-TOKEN')).toBe(false)
  })

  it('fetches a CSRF token before a mutating request and reuses it afterwards', async () => {
    const { apiJson } = await loadClient()
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-123' }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiJson('/api/projects', { method: 'POST', body: '{}' })
    await apiJson('/api/projects', { method: 'POST', body: '{}' })

    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/security/csrf')
    expect(headersOf(fetchMock.mock.calls[1] as FetchArgs).get('X-CSRF-TOKEN')).toBe('csrf-123')
    expect(headersOf(fetchMock.mock.calls[2] as FetchArgs).get('X-CSRF-TOKEN')).toBe('csrf-123')
  })

  it('does not overwrite a CSRF token the caller already supplied', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiJson('/api/projects', {
      method: 'POST',
      body: '{}',
      headers: { 'X-CSRF-TOKEN': 'caller-token' },
    })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(headersOf(fetchMock.mock.calls[0] as FetchArgs).get('X-CSRF-TOKEN')).toBe('caller-token')
  })

  it('retries the CSRF fetch after a failure instead of caching the rejection', async () => {
    const { apiJson } = await loadClient()
    fetchMock
      .mockResolvedValueOnce(jsonResponse('nope', 500))
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-after-retry' }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))

    await expect(apiJson('/api/projects', { method: 'POST', body: '{}' })).rejects.toThrow(/CSRF token request failed/)
    await apiJson('/api/projects', { method: 'POST', body: '{}' })

    expect(headersOf(fetchMock.mock.calls[2] as FetchArgs).get('X-CSRF-TOKEN')).toBe('csrf-after-retry')
  })

  it('sets a JSON content type for a plain body but leaves FormData to the browser', async () => {
    const { apiJson } = await loadClient()
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ token: 'csrf-123' }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))

    await apiJson('/api/projects', { method: 'POST', body: '{"a":1}' })
    await apiJson('/api/upload', { method: 'POST', body: new FormData() })

    expect(headersOf(fetchMock.mock.calls[1] as FetchArgs).get('Content-Type')).toBe('application/json')
    expect(headersOf(fetchMock.mock.calls[2] as FetchArgs).has('Content-Type')).toBe(false)
  })

  it('returns a parsed payload and tolerates an empty body', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 7 }))
    await expect(apiJson('/api/projects/7')).resolves.toEqual({ id: 7 })

    fetchMock.mockResolvedValueOnce(textResponse(''))
    await expect(apiJson('/api/projects/7')).resolves.toBeNull()
  })

  it('redirects to the login page on 401 and preserves the return URL', async () => {
    const { apiJson } = await loadClient()
    const location = { pathname: '/projects/42', href: '' }
    vi.stubGlobal('location', location)
    Object.defineProperty(window, 'location', { configurable: true, writable: true, value: location })
    fetchMock.mockResolvedValueOnce(jsonResponse(null, 401))

    await expect(apiJson('/api/projects')).rejects.toThrow('Bạn cần đăng nhập để tiếp tục.')
    expect(location.href).toBe('/Account/Login?returnUrl=%2Fprojects%2F42')
  })
})

describe('apiJson error mapping', () => {
  it.each([
    [403, 'Bạn không có quyền thực hiện thao tác này.'],
    [409, 'Dữ liệu đã thay đổi hoặc bị trùng. Vui lòng tải lại rồi thử lại.'],
    [404, 'Không tìm thấy dữ liệu yêu cầu.'],
    [422, 'Dữ liệu nhập vào chưa hợp lệ.'],
    [500, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'],
    [503, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'],
  ])('turns a bare %i into a Vietnamese message', async (status, message) => {
    const { apiJson, ApiError } = await loadClient()
    fetchMock.mockResolvedValueOnce(textResponse('', status))

    const error = await apiJson('/api/projects').catch((thrown) => thrown)
    expect(error).toBeInstanceOf(ApiError)
    expect(error.message).toBe(message)
    expect(error.status).toBe(status)
  })

  it('falls back to a generic message for an unmapped status', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(textResponse('', 418))
    await expect(apiJson('/api/projects')).rejects.toThrow('Không thể hoàn tất yêu cầu (mã 418).')
  })

  it('prefers the server error, then message, then title', async () => {
    const { apiJson } = await loadClient()

    fetchMock.mockResolvedValueOnce(jsonResponse({ error: 'Tên dự án đã tồn tại' }, 409))
    await expect(apiJson('/api/projects')).rejects.toThrow('Tên dự án đã tồn tại')

    fetchMock.mockResolvedValueOnce(jsonResponse({ message: 'Sprint đã đóng' }, 409))
    await expect(apiJson('/api/projects')).rejects.toThrow('Sprint đã đóng')

    fetchMock.mockResolvedValueOnce(jsonResponse({ title: 'One or more validation errors' }, 400))
    await expect(apiJson('/api/projects')).rejects.toThrow('One or more validation errors')
  })

  it('surfaces the first ProblemDetails validation message ahead of the generic title', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          title: 'One or more validation errors occurred.',
          errors: { Name: ['Tên dự án không được để trống'] },
        },
        400,
      ),
    )

    await expect(apiJson('/api/projects')).rejects.toThrow('Tên dự án không được để trống')
  })

  it('uses a non-JSON error body verbatim', async () => {
    const { apiJson } = await loadClient()
    fetchMock.mockResolvedValueOnce(textResponse('  Upstream timeout  ', 502))
    await expect(apiJson('/api/projects')).rejects.toThrow('Upstream timeout')
  })
})

describe('apiResult', () => {
  it('unwraps the data field of an envelope', async () => {
    const { apiResult } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ data: { id: 1 }, error: null, isSuccess: true }))
    await expect(apiResult('/api/projects/1')).resolves.toEqual({ id: 1 })
  })

  it('throws the envelope error when the call did not succeed', async () => {
    const { apiResult } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ data: null, error: 'Không đủ quyền', isSuccess: false }))
    await expect(apiResult('/api/projects/1')).rejects.toThrow('Không đủ quyền')
  })

  it('throws when a successful envelope carries no data', async () => {
    const { apiResult } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse({ data: null, error: null, isSuccess: true }))
    await expect(apiResult('/api/projects/1')).rejects.toThrow('Không thể hoàn tất yêu cầu.')
  })

  it('passes through a bare payload that is not an envelope', async () => {
    const { apiResult } = await loadClient()
    fetchMock.mockResolvedValueOnce(jsonResponse([{ id: 1 }]))
    await expect(apiResult('/api/projects')).resolves.toEqual([{ id: 1 }])
  })
})

describe('apiCommand', () => {
  it('resolves for a successful envelope, a bare ok and an empty body', async () => {
    const { apiCommand } = await loadClient()

    fetchMock.mockResolvedValueOnce(jsonResponse({ data: null, error: null, isSuccess: true }))
    await expect(apiCommand('/api/projects/1')).resolves.toBeUndefined()

    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: true }))
    await expect(apiCommand('/api/projects/1')).resolves.toBeUndefined()

    fetchMock.mockResolvedValueOnce(textResponse(''))
    await expect(apiCommand('/api/projects/1')).resolves.toBeUndefined()
  })

  it('rejects on a failed envelope or an explicit ok:false', async () => {
    const { apiCommand } = await loadClient()

    fetchMock.mockResolvedValueOnce(jsonResponse({ data: null, error: 'Không xoá được', isSuccess: false }))
    await expect(apiCommand('/api/projects/1')).rejects.toThrow('Không xoá được')

    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: false }))
    await expect(apiCommand('/api/projects/1')).rejects.toThrow('Không thể hoàn tất yêu cầu.')
  })
})

describe('errorMessage', () => {
  it('keeps a meaningful message', async () => {
    const { errorMessage } = await loadClient()
    expect(errorMessage(new Error('Sprint đã đóng'))).toBe('Sprint đã đóng')
  })

  it('replaces transport noise with the caller fallback', async () => {
    const { errorMessage } = await loadClient()
    expect(errorMessage(new Error('Request failed.'), 'Lưu thất bại')).toBe('Lưu thất bại')
    expect(errorMessage(new Error('Request failed with status 500'), 'Lưu thất bại')).toBe('Lưu thất bại')
    expect(errorMessage(new Error('Không thể hoàn tất yêu cầu (mã 418).'), 'Lưu thất bại')).toBe('Lưu thất bại')
    expect(errorMessage(new Error('   '), 'Lưu thất bại')).toBe('Lưu thất bại')
    expect(errorMessage('a string', 'Lưu thất bại')).toBe('Lưu thất bại')
  })

  it('keeps a server-mapped ApiError message even when it looks generic', async () => {
    const { ApiError, errorMessage } = await loadClient()
    const error = new ApiError('Bạn không có quyền thực hiện thao tác này.', 403, null)
    expect(errorMessage(error, 'Lưu thất bại')).toBe('Bạn không có quyền thực hiện thao tác này.')
  })

  it('uses the default fallback when none is supplied', async () => {
    const { errorMessage } = await loadClient()
    expect(errorMessage(null)).toBe('Đã xảy ra lỗi khi lưu dữ liệu')
  })
})
