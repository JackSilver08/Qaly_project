import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useMeetingRecovery, type RecoveryTranscriptEntry } from '@/composables/use-meeting-recovery'

/**
 * Minimal fake IndexedDB: a Map-backed store plus request/transaction objects
 * that fire onsuccess/onerror/oncomplete the same way the real API does.
 */
function installFakeIndexedDB() {
  const stores = new Map<string, unknown>()
  let objectStoreCreated = false
  let failNextOpen = false
  let failNextOperation = false

  class FakeRequest<T> {
    result: T | undefined
    error: Error | null = null
    onsuccess: (() => void) | null = null
    onerror: (() => void) | null = null
  }

  class FakeObjectStore {
    get(key: string) {
      const request = new FakeRequest<unknown>()
      queueMicrotask(() => {
        if (failNextOperation) {
          request.error = new Error('get failed')
          request.onerror?.()
          return
        }
        request.result = stores.get(key)
        request.onsuccess?.()
      })
      return request
    }

    put(value: unknown, key: string) {
      stores.set(key, value)
      return new FakeRequest()
    }

    delete(key: string) {
      stores.delete(key)
      return new FakeRequest()
    }
  }

  class FakeTransaction {
    oncomplete: (() => void) | null = null
    onerror: (() => void) | null = null
    error: Error | null = null
    private store = new FakeObjectStore()

    objectStore() {
      return this.store
    }

    constructor() {
      queueMicrotask(() => {
        if (failNextOperation) {
          this.error = new Error('transaction failed')
          this.onerror?.()
          failNextOperation = false
          return
        }
        this.oncomplete?.()
      })
    }
  }

  class FakeDatabase {
    objectStoreNames = {
      contains: (name: string) => objectStoreCreated && name === 'transcripts',
    }

    createObjectStore() {
      objectStoreCreated = true
    }

    transaction() {
      return new FakeTransaction()
    }
  }

  const fakeIndexedDB = {
    open() {
      const request = new FakeRequest<FakeDatabase>() as FakeRequest<FakeDatabase> & {
        onupgradeneeded: (() => void) | null
      }
      request.onupgradeneeded = null
      queueMicrotask(() => {
        if (failNextOpen) {
          request.error = new Error('open failed')
          request.onerror?.()
          failNextOpen = false
          return
        }
        const isFirstOpen = !objectStoreCreated
        request.result = new FakeDatabase()
        if (isFirstOpen) request.onupgradeneeded?.()
        request.onsuccess?.()
      })
      return request
    },
  }

  vi.stubGlobal('indexedDB', fakeIndexedDB)

  return {
    stores,
    forceStoreCreated: () => {
      objectStoreCreated = true
    },
    triggerOpenFailure: () => {
      failNextOpen = true
    },
    triggerOperationFailure: () => {
      failNextOperation = true
    },
  }
}

function entry(text: string, timestamp: number): RecoveryTranscriptEntry {
  return { senderName: 'A', text, timestamp }
}

describe('useMeetingRecovery', () => {
  let fake: ReturnType<typeof installFakeIndexedDB>

  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-08-31T12:00:00.000Z'))
    fake = installFakeIndexedDB()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  it('creates the object store on first open', async () => {
    const { saveBuffer } = useMeetingRecovery()
    await saveBuffer('meeting-1', [entry('hi', Date.now())])
    await vi.runAllTimersAsync()

    expect(fake.stores.has('meeting-1')).toBe(true)
  })

  it('does not recreate the object store when it already exists', async () => {
    fake.forceStoreCreated()
    const { saveBuffer } = useMeetingRecovery()
    await saveBuffer('meeting-1', [entry('hi', Date.now())])
    await vi.runAllTimersAsync()

    expect(fake.stores.get('meeting-1')).toEqual([entry('hi', Date.now())])
  })

  it('saveBuffer filters out entries older than 3 minutes', async () => {
    const { saveBuffer } = useMeetingRecovery()
    const now = Date.now()
    const stale = entry('cũ', now - 4 * 60 * 1000)
    const fresh = entry('mới', now)

    await saveBuffer('meeting-1', [stale, fresh])
    await vi.runAllTimersAsync()

    expect(fake.stores.get('meeting-1')).toEqual([fresh])
  })

  it('saveBuffer keeps an entry exactly at the 3-minute boundary', async () => {
    const { saveBuffer } = useMeetingRecovery()
    const now = Date.now()
    const boundary = entry('biên', now - 3 * 60 * 1000)

    await saveBuffer('meeting-1', [boundary])
    await vi.runAllTimersAsync()

    expect(fake.stores.get('meeting-1')).toEqual([boundary])
  })

  it('saveBuffer resolves once the transaction completes', async () => {
    const { saveBuffer } = useMeetingRecovery()
    const promise = saveBuffer('meeting-1', [entry('hi', Date.now())])
    await vi.runAllTimersAsync()

    await expect(promise).resolves.toBeUndefined()
  })

  it('saveBuffer swallows errors instead of throwing', async () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
    fake.triggerOpenFailure()

    const { saveBuffer } = useMeetingRecovery()
    const promise = saveBuffer('meeting-1', [entry('hi', Date.now())])
    await vi.runAllTimersAsync()

    await expect(promise).resolves.toBeUndefined()
    expect(warnSpy).toHaveBeenCalled()
  })

  it('getBuffer returns an empty array when no record exists', async () => {
    const { getBuffer } = useMeetingRecovery()
    const resultPromise = getBuffer('unknown-meeting')
    await vi.runAllTimersAsync()

    expect(await resultPromise).toEqual([])
  })

  it('getBuffer filters out stale entries from a stored record', async () => {
    const now = Date.now()
    fake.stores.set('meeting-1', [entry('cũ', now - 5 * 60 * 1000), entry('mới', now)])

    const { getBuffer } = useMeetingRecovery()
    const resultPromise = getBuffer('meeting-1')
    await vi.runAllTimersAsync()

    expect(await resultPromise).toEqual([entry('mới', now)])
  })

  it('getBuffer returns an empty array on error instead of throwing', async () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
    fake.triggerOpenFailure()

    const { getBuffer } = useMeetingRecovery()
    const resultPromise = getBuffer('meeting-1')
    await vi.runAllTimersAsync()

    expect(await resultPromise).toEqual([])
    expect(warnSpy).toHaveBeenCalled()
  })

  it('clearBuffer deletes the record for the given meeting', async () => {
    fake.stores.set('meeting-1', [entry('hi', Date.now())])

    const { clearBuffer } = useMeetingRecovery()
    await clearBuffer('meeting-1')
    await vi.runAllTimersAsync()

    expect(fake.stores.has('meeting-1')).toBe(false)
  })

  it('clearBuffer swallows errors instead of throwing', async () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
    fake.triggerOpenFailure()

    const { clearBuffer } = useMeetingRecovery()
    const promise = clearBuffer('meeting-1')
    await vi.runAllTimersAsync()

    await expect(promise).resolves.toBeUndefined()
    expect(warnSpy).toHaveBeenCalled()
  })

  it('stores and retrieves buffers for different meetings independently', async () => {
    const { saveBuffer, getBuffer } = useMeetingRecovery()
    const now = Date.now()

    await saveBuffer('meeting-a', [entry('a', now)])
    await vi.runAllTimersAsync()
    await saveBuffer('meeting-b', [entry('b', now)])
    await vi.runAllTimersAsync()

    const resultA = getBuffer('meeting-a')
    await vi.runAllTimersAsync()
    const resultB = getBuffer('meeting-b')
    await vi.runAllTimersAsync()

    expect(await resultA).toEqual([entry('a', now)])
    expect(await resultB).toEqual([entry('b', now)])
  })
})
