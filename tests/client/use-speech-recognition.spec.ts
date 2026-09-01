import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { useSpeechRecognition } from '@/composables/use-speech-recognition'

class MockSpeechRecognition {
  continuous = false
  interimResults = false
  lang = ''
  onstart: (() => void) | null = null
  onerror: ((event: any) => void) | null = null
  onend: (() => void) | null = null
  onresult: ((event: any) => void) | null = null

  start = vi.fn(() => {
    if (this.onstart) {
      this.onstart()
    }
  })

  stop = vi.fn(() => {
    if (this.onend) {
      this.onend()
    }
  })
}

describe('useSpeechRecognition', () => {
  let originalSpeechRecognition: any
  let originalWebkitSpeechRecognition: any

  beforeEach(() => {
    vi.useFakeTimers()
    originalSpeechRecognition = (window as any).SpeechRecognition
    originalWebkitSpeechRecognition = (window as any).webkitSpeechRecognition
    ;(window as any).SpeechRecognition = MockSpeechRecognition
    delete (window as any).webkitSpeechRecognition
  })

  afterEach(() => {
    vi.useRealTimers()
    ;(window as any).SpeechRecognition = originalSpeechRecognition
    ;(window as any).webkitSpeechRecognition = originalWebkitSpeechRecognition
  })

  it('detects isSupported when window.SpeechRecognition is available', () => {
    const callback = vi.fn()
    const { isSupported, isListening, hasError } = useSpeechRecognition(callback)

    expect(isSupported.value).toBe(true)
    expect(isListening.value).toBe(false)
    expect(hasError.value).toBeNull()
  })

  it('detects isSupported when only window.webkitSpeechRecognition is available', () => {
    delete (window as any).SpeechRecognition
    ;(window as any).webkitSpeechRecognition = MockSpeechRecognition

    const callback = vi.fn()
    const { isSupported } = useSpeechRecognition(callback)
    expect(isSupported.value).toBe(true)
  })

  it('reports isSupported as false when no SpeechRecognition API is found', () => {
    delete (window as any).SpeechRecognition
    delete (window as any).webkitSpeechRecognition

    const callback = vi.fn()
    const { isSupported, start } = useSpeechRecognition(callback)

    expect(isSupported.value).toBe(false)
    start()
    expect(callback).not.toHaveBeenCalled()
  })

  it('starts recognition with default language vi-VN and activates isListening', () => {
    const callback = vi.fn()
    const { start, isListening, hasError } = useSpeechRecognition(callback)

    start()

    expect(isListening.value).toBe(true)
    expect(hasError.value).toBeNull()
  })

  it('allows starting recognition with custom language', () => {
    const callback = vi.fn()
    const { start } = useSpeechRecognition(callback)

    start('en-US')
    // No errors thrown and successfully started
    expect(callback).not.toHaveBeenCalled()
  })

  it('handles "not-allowed" error by providing clear Vietnamese permission message', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start, hasError } = useSpeechRecognition(callback)
    start()

    expect(instance).not.toBeNull()
    instance!.onerror!({ error: 'not-allowed' })

    expect(hasError.value).toBe('Chưa cấp quyền Micro cho trình duyệt.')
  })

  it('handles "network" error by setting appropriate network message', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start, hasError } = useSpeechRecognition(callback)
    start()

    instance!.onerror!({ error: 'network' })
    expect(hasError.value).toBe('Lỗi mạng khi nhận diện giọng nói.')
  })

  it('invokes callback with final text when isFinal is true', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start } = useSpeechRecognition(callback)
    start()

    const mockEvent = {
      resultIndex: 0,
      results: [
        {
          isFinal: true,
          0: { transcript: 'Tạo công việc mới' }
        }
      ]
    }

    instance!.onresult!(mockEvent)
    expect(callback).toHaveBeenCalledWith('Tạo công việc mới', true)
  })

  it('invokes callback with interim text when isFinal is false', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start } = useSpeechRecognition(callback)
    start()

    const mockEvent = {
      resultIndex: 0,
      results: [
        {
          isFinal: false,
          0: { transcript: 'Đang nói gì đó' }
        }
      ]
    }

    instance!.onresult!(mockEvent)
    expect(callback).toHaveBeenCalledWith('Đang nói gì đó', false)
  })

  it('auto-restarts when onend fires while speech session is still expected to be active', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start, isListening } = useSpeechRecognition(callback)
    start()
    expect(isListening.value).toBe(true)

    // Simulate browser ending recognition stream
    instance!.onend!()
    expect(isListening.value).toBe(false)

    // Advance 300ms debounce
    vi.advanceTimersByTime(300)
    expect(instance!.start).toHaveBeenCalledTimes(2)
  })

  it('stops listening cleanly and prevents auto-restart when stop() is invoked', () => {
    const callback = vi.fn()
    let instance: MockSpeechRecognition | null = null

    class CustomMock extends MockSpeechRecognition {
      constructor() {
        super()
        instance = this
      }
    }
    ;(window as any).SpeechRecognition = CustomMock

    const { start, stop, isListening } = useSpeechRecognition(callback)
    start()
    expect(isListening.value).toBe(true)

    stop()
    expect(isListening.value).toBe(false)

    // Advance timer to ensure no restart happens
    vi.advanceTimersByTime(500)
    expect(instance!.start).toHaveBeenCalledTimes(1)
  })
})
