import { createApp } from 'vue'
import App from './App.vue'
import ToastContainer from './components/ToastContainer.vue'
import { router } from './router'
import { showError, showSuccess, showToast, type ToastInput, type ToastType } from './composables/use-toast'
import './style.css'

declare global {
  interface Window {
    qalyToast?: {
      showToast: (input: ToastInput) => string
      showSuccess: (message: string) => string
      showError: (message: string) => string
    }
  }
}

const toastTarget = document.getElementById('qaly-toast-root')
const target = document.getElementById('qaly-dashboard-app')

if (toastTarget) {
  createApp(ToastContainer).mount(toastTarget)
}

window.qalyToast = {
  showToast,
  showSuccess,
  showError,
}

document.querySelectorAll<HTMLElement>('[data-toast-message]').forEach((element) => {
  const message = element.dataset.toastMessage?.trim()
  if (!message || element.dataset.toastConsumed === 'true') return

  const type = normalizeToastType(element.dataset.toastType)
  element.dataset.toastConsumed = 'true'
  showToast({ type, message })
})

if (target) {
  createApp(App).use(router).mount(target)
}

function normalizeToastType(type: string | undefined): ToastType {
  return type === 'success' || type === 'error' || type === 'warning' || type === 'info' ? type : 'info'
}
