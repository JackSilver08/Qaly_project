<script setup lang="ts">
import { CheckCircle2, Undo2, X } from 'lucide-vue-next'
import { ref, computed, onMounted, onUnmounted } from 'vue'

const props = defineProps<{
  importSessionId: string
  importedCount: number
  failedCount?: number
  duplicateSkippedCount?: number
  createdAt: string
}>()

const emit = defineEmits<{
  undo: []
  dismiss: []
}>()

const now = ref(Date.now())
let timer: ReturnType<typeof setInterval> | null = null

const expiresAt = computed(() => new Date(props.createdAt).getTime() + 30 * 60 * 1000)
const remainingMs = computed(() => Math.max(0, expiresAt.value - now.value))
const canUndo = computed(() => remainingMs.value > 0)

const remainingText = computed(() => {
  const totalSec = Math.floor(remainingMs.value / 1000)
  const min = Math.floor(totalSec / 60)
  const sec = totalSec % 60
  return `${min}:${sec.toString().padStart(2, '0')}`
})

const progressPercent = computed(() => {
  const total = 30 * 60 * 1000
  return Math.max(0, (remainingMs.value / total) * 100)
})

onMounted(() => {
  timer = setInterval(() => { now.value = Date.now() }, 1000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <transition name="slide-up">
    <div v-if="canUndo" class="undo-banner">
      <div class="undo-banner__progress" :style="{ width: progressPercent + '%' }"></div>
      <div class="undo-banner__content">
        <div class="undo-banner__left">
          <span class="undo-badge"><CheckCircle2 :size="20" /></span>
          <div>
            <p class="undo-text">
              Đã import <strong>{{ importedCount }}</strong> task thành công
              <span v-if="(failedCount ?? 0) > 0">, <strong>{{ failedCount }}</strong> lỗi</span>
              <span v-if="(duplicateSkippedCount ?? 0) > 0">, <strong>{{ duplicateSkippedCount }}</strong> trùng bỏ qua</span>
            </p>
            <p class="undo-timer">Còn <strong>{{ remainingText }}</strong> để hoàn tác</p>
          </div>
        </div>
        <div class="undo-banner__right">
          <button class="btn btn--undo" @click="emit('undo')">
            <Undo2 :size="14" /> Hoàn tác
          </button>
          <button class="btn-dismiss" @click="emit('dismiss')">
            <X :size="14" />
          </button>
        </div>
      </div>
    </div>
  </transition>
</template>

<style scoped>
.undo-banner {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 10000;
  width: min(560px, 92vw);
  border-radius: var(--qaly-radius-lg);
  background: rgba(30, 30, 45, .95);
  border: 1px solid rgba(255,255,255,.08);
  box-shadow: var(--qaly-shadow-md);
  backdrop-filter: none;
  overflow: hidden;
}

.undo-banner__progress {
  position: absolute;
  top: 0;
  left: 0;
  height: 3px;
  background: var(--primary);
  transition: width 1s linear;
  border-radius: 3px 0 0 0;
}

.undo-banner__content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  position: relative;
}

.undo-banner__left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.undo-badge {
  display: inline-flex;
  color: #22c55e;
}

.undo-text {
  margin: 0;
  font-size: .85rem;
  color: rgba(255,255,255,.85);
}

.undo-timer {
  margin: 2px 0 0;
  font-size: .72rem;
  color: rgba(255,255,255,.4);
}

.undo-banner__right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn--undo {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 6px 16px;
  border-radius: var(--qaly-radius-lg);
  font-size: .8rem;
  font-weight: 600;
  background: rgba(239,68,68,.12);
  color: #ef4444;
  border: 1px solid rgba(239,68,68,.2);
  cursor: pointer;
  transition: all .2s;
}
.btn--undo:hover {
  background: rgba(239,68,68,.2);
}

.btn-dismiss {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  background: transparent;
  border: none;
  color: rgba(255,255,255,.3);
  cursor: pointer;
  transition: all .2s;
}
.btn-dismiss:hover {
  background: rgba(255,255,255,.06);
  color: rgba(255,255,255,.6);
}

/* Animation */
.slide-up-enter-active { animation: slideUp .4s cubic-bezier(.22,1,.36,1); }
.slide-up-leave-active { animation: slideUp .3s ease reverse; }

@keyframes slideUp {
  from { opacity: 0; transform: translateX(-50%) translateY(40px); }
  to { opacity: 1; transform: translateX(-50%) translateY(0); }
}
</style>
