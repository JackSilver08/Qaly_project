import { beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * The toast store is a module singleton, so every test re-imports it to start from an empty queue.
 */
async function loadToast() {
  vi.resetModules()
  return import('@/composables/use-toast')
}

beforeEach(() => {
  vi.useFakeTimers()
})

describe('showToast', () => {
  it('adds a toast with the default type and duration', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    const id = showToast({ message: 'Đã lưu' })

    expect(id).not.toBe('')
    expect(toasts.value).toHaveLength(1)
    expect(toasts.value[0]).toMatchObject({ id, type: 'info', message: 'Đã lưu', duration: 4600 })
  })

  it('trims the message and ignores a blank one', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    expect(showToast({ message: '   Đã lưu   ' })).not.toBe('')
    expect(toasts.value[0].message).toBe('Đã lưu')

    expect(showToast({ message: '   ' })).toBe('')
    expect(toasts.value).toHaveLength(1)
  })

  it('puts the newest toast first', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    showToast({ message: 'đầu tiên' })
    showToast({ message: 'thứ hai' })

    expect(toasts.value.map((toast) => toast.message)).toEqual(['thứ hai', 'đầu tiên'])
  })

  it('issues unique ids even within the same millisecond', async () => {
    const { showToast } = await loadToast()
    vi.setSystemTime(new Date('2026-03-14T12:00:00.000Z'))

    const ids = Array.from({ length: 5 }, (_, index) => showToast({ message: `toast ${index}` }))
    expect(new Set(ids).size).toBe(5)
  })

  it('caps the queue at five and drops the oldest', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    for (let index = 1; index <= 7; index += 1) {
      showToast({ message: `toast ${index}` })
    }

    expect(toasts.value).toHaveLength(5)
    expect(toasts.value.map((toast) => toast.message)).toEqual([
      'toast 7',
      'toast 6',
      'toast 5',
      'toast 4',
      'toast 3',
    ])
  })

  it('auto-dismisses after the configured duration', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    showToast({ message: 'ngắn', duration: 1_000 })
    showToast({ message: 'dài', duration: 9_000 })

    vi.advanceTimersByTime(1_000)
    expect(toasts.value.map((toast) => toast.message)).toEqual(['dài'])

    vi.advanceTimersByTime(8_000)
    expect(toasts.value).toHaveLength(0)
  })

  it('does not resurrect an overflowed toast when its timer would have fired', async () => {
    const { showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    for (let index = 1; index <= 6; index += 1) {
      showToast({ message: `toast ${index}`, duration: 1_000 })
    }

    vi.advanceTimersByTime(5_000)
    expect(toasts.value).toHaveLength(0)
  })
})

describe('dismissToast', () => {
  it('removes only the requested toast', async () => {
    const { dismissToast, showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    const first = showToast({ message: 'giữ lại' })
    const second = showToast({ message: 'bỏ đi' })

    dismissToast(second)

    expect(toasts.value.map((toast) => toast.id)).toEqual([first])
  })

  it('is a no-op for an unknown id', async () => {
    const { dismissToast, showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    showToast({ message: 'giữ lại' })
    dismissToast('toast-does-not-exist')

    expect(toasts.value).toHaveLength(1)
  })

  it('cancels the pending timer so a re-added id is not swept away', async () => {
    const { dismissToast, showToast, useToast } = await loadToast()
    const { toasts } = useToast()

    showToast({ message: 'đầu tiên', duration: 1_000 })
    dismissToast(toasts.value[0].id)

    showToast({ message: 'thứ hai', duration: 5_000 })
    vi.advanceTimersByTime(1_000)

    expect(toasts.value.map((toast) => toast.message)).toEqual(['thứ hai'])
  })
})

describe('typed helpers', () => {
  it('tag the toast with the matching type', async () => {
    const { showError, showInfo, showSuccess, showWarning, useToast } = await loadToast()
    const { toasts } = useToast()

    showSuccess('ok')
    showError('lỗi')
    showWarning('cảnh báo')
    showInfo('thông tin')

    expect(toasts.value.map((toast) => toast.type)).toEqual(['info', 'warning', 'error', 'success'])
  })

  it('forwards the title and duration options', async () => {
    const { showError, useToast } = await loadToast()
    const { toasts } = useToast()

    showError('Không lưu được', { title: 'Lỗi mạng', duration: 12_000 })

    expect(toasts.value[0]).toMatchObject({ title: 'Lỗi mạng', duration: 12_000, type: 'error' })
  })
})
