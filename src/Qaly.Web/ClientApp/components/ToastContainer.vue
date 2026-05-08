<script setup lang="ts">
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-vue-next'
import { useToast, type ToastType } from '../composables/use-toast'

const { toasts, dismissToast } = useToast()

const iconByType = {
  success: CheckCircle2,
  error: XCircle,
  warning: AlertTriangle,
  info: Info,
}

const titleByType: Record<ToastType, string> = {
  success: 'Thành công',
  error: 'Thất bại',
  warning: 'Cần chú ý',
  info: 'Thông báo',
}
</script>

<template>
  <Teleport to="body">
    <TransitionGroup name="toast-stack" tag="div" class="toast-viewport" aria-live="polite">
      <article
        v-for="toast in toasts"
        :key="toast.id"
        class="toast-card"
        :class="`toast-card--${toast.type}`"
        :role="toast.type === 'error' ? 'alert' : 'status'"
      >
        <div class="toast-card__icon" aria-hidden="true">
          <component :is="iconByType[toast.type]" :size="19" />
        </div>

        <div class="toast-card__content">
          <strong>{{ toast.title ?? titleByType[toast.type] }}</strong>
          <p>{{ toast.message }}</p>
        </div>

        <button class="toast-card__close" type="button" aria-label="Đóng thông báo" @click="dismissToast(toast.id)">
          <X :size="16" />
        </button>

        <span class="toast-card__progress" :style="{ animationDuration: `${toast.duration}ms` }"></span>
      </article>
    </TransitionGroup>
  </Teleport>
</template>
