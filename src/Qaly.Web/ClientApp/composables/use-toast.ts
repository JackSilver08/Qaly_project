import { readonly, ref } from 'vue'

export type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface ToastInput {
  type?: ToastType
  message: string
  title?: string
  duration?: number
}

export interface ToastItem {
  id: string
  type: ToastType
  message: string
  title?: string
  duration: number
}

const DEFAULT_DURATION = 4600
const MAX_TOASTS = 5

const toasts = ref<ToastItem[]>([])
const timers = new Map<string, number>()
let toastSequence = 0

function clearToastTimer(id: string) {
  const timer = timers.get(id)
  if (timer !== undefined) {
    if (typeof window !== 'undefined') window.clearTimeout(timer)
    timers.delete(id)
  }
}

export function dismissToast(id: string) {
  clearToastTimer(id)
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

export function showToast(input: ToastInput) {
  const message = input.message.trim()
  if (!message) return ''

  const id = `toast-${Date.now()}-${toastSequence++}`
  const toast: ToastItem = {
    id,
    type: input.type ?? 'info',
    message,
    title: input.title,
    duration: input.duration ?? DEFAULT_DURATION,
  }

  toasts.value = [toast, ...toasts.value]

  if (toasts.value.length > MAX_TOASTS) {
    const overflow = toasts.value.slice(MAX_TOASTS)
    overflow.forEach((item) => clearToastTimer(item.id))
    toasts.value = toasts.value.slice(0, MAX_TOASTS)
  }

  if (typeof window !== 'undefined') {
    timers.set(id, window.setTimeout(() => dismissToast(id), toast.duration))
  }

  return id
}

export function showSuccess(message: string, options: Omit<ToastInput, 'message' | 'type'> = {}) {
  return showToast({ ...options, type: 'success', message })
}

export function showError(message: string, options: Omit<ToastInput, 'message' | 'type'> = {}) {
  return showToast({ ...options, type: 'error', message })
}

export function showWarning(message: string, options: Omit<ToastInput, 'message' | 'type'> = {}) {
  return showToast({ ...options, type: 'warning', message })
}

export function showInfo(message: string, options: Omit<ToastInput, 'message' | 'type'> = {}) {
  return showToast({ ...options, type: 'info', message })
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    showToast,
    showSuccess,
    showError,
    showWarning,
    showInfo,
    dismissToast,
  }
}
