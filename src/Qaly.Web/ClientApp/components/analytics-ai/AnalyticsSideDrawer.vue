<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue'
import { X } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  title: string
  subtitle?: string
}>()

const emit = defineEmits<{
  close: []
}>()

const titleId = computed(() => `analytics-drawer-title-${props.title.replace(/\W+/g, '-').toLowerCase()}`)

function close() {
  emit('close')
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.open) {
    close()
  }
}

watch(
  () => props.open,
  (open) => {
    if (open) {
      document.addEventListener('keydown', handleKeydown)
    } else {
      document.removeEventListener('keydown', handleKeydown)
    }
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <Transition name="analytics-drawer-shell">
    <div v-if="open" class="analytics-drawer-shell">
      <button class="analytics-drawer-overlay" type="button" aria-label="Đóng bảng phụ" @click="close"></button>
      <aside class="analytics-drawer-panel" role="dialog" aria-modal="true" :aria-labelledby="titleId">
        <header class="analytics-drawer-header">
          <div class="analytics-drawer-heading">
            <strong :id="titleId">{{ title }}</strong>
            <span v-if="subtitle">{{ subtitle }}</span>
          </div>
          <div class="analytics-drawer-header-actions">
            <slot name="header-actions"></slot>
            <button class="analytics-drawer-close" type="button" aria-label="Đóng bảng phụ" @click="close">
              <X :size="17" aria-hidden="true" />
            </button>
          </div>
        </header>

        <div class="analytics-drawer-body">
          <slot></slot>
        </div>

        <footer class="analytics-drawer-footer">
          <slot name="footer"></slot>
        </footer>
      </aside>
    </div>
  </Transition>
</template>

<style scoped>
.analytics-drawer-shell {
  --analytics-drawer-shell-left: 240px;
  --analytics-drawer-shell-top: 72px;
  position: fixed;
  inset: var(--analytics-drawer-shell-top) 0 0 var(--analytics-drawer-shell-left);
  /* Must stay above the global AI workspace (z-index 9999) as this drawer is
     shared by both Analytics and the floating native assistant. */
  z-index: 10020;
  pointer-events: auto;
}

.analytics-drawer-overlay {
  position: absolute;
  inset: 0;
  border: 0;
  background: rgba(15, 23, 42, 0.22);
  cursor: default;
}

.analytics-drawer-panel {
  position: absolute;
  top: 12px;
  right: 12px;
  bottom: 12px;
  width: min(420px, calc(100vw - var(--analytics-drawer-shell-left) - 24px));
  min-width: 0;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr) auto;
  overflow: hidden;
  border: 1px solid rgba(15, 23, 42, 0.1);
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
}

.analytics-drawer-header,
.analytics-drawer-footer {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
  border-color: #e2e8f0;
}

.analytics-drawer-header {
  justify-content: space-between;
  border-bottom: 1px solid #e2e8f0;
  padding: 14px 14px 12px;
}

.analytics-drawer-footer {
  border-top: 1px solid #e2e8f0;
  padding: 10px 14px;
}

.analytics-drawer-heading {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.analytics-drawer-heading strong,
.analytics-drawer-heading span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.analytics-drawer-heading strong {
  color: #0f172a;
  font-size: 15px;
  font-weight: 900;
}

.analytics-drawer-heading span {
  color: #64748b;
  font-size: 12px;
  font-weight: 650;
}

.analytics-drawer-header-actions {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
}

.analytics-drawer-close {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border: 1px solid #dbe4ef;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #475569;
  cursor: pointer;
}

.analytics-drawer-close:hover,
.analytics-drawer-close:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  color: #1d4ed8;
  outline: none;
}

.analytics-drawer-body {
  min-width: 0;
  min-height: 0;
  overflow-y: auto;
  padding: 14px;
}

.analytics-drawer-shell-enter-active,
.analytics-drawer-shell-leave-active {
  transition: opacity 0.18s ease;
}

.analytics-drawer-shell-enter-active .analytics-drawer-panel,
.analytics-drawer-shell-leave-active .analytics-drawer-panel {
  transition: transform 0.2s ease;
}

.analytics-drawer-shell-enter-from,
.analytics-drawer-shell-leave-to {
  opacity: 0;
}

.analytics-drawer-shell-enter-from .analytics-drawer-panel,
.analytics-drawer-shell-leave-to .analytics-drawer-panel {
  transform: translateX(8px);
}

:global(:root[data-theme='dark']) .analytics-drawer-panel,
:global(:root[data-theme='dark']) .analytics-drawer-header,
:global(:root[data-theme='dark']) .analytics-drawer-footer,
:global(:root[data-theme='dark']) .analytics-drawer-close {
  border-color: var(--line) !important;
  background: var(--panel) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .analytics-drawer-heading strong {
  color: var(--text-strong) !important;
}

:global(:root[data-theme='dark']) .analytics-drawer-heading span {
  color: var(--muted) !important;
}

@media (max-width: 1400px) and (min-width: 1241px) {
  .analytics-drawer-shell {
    --analytics-drawer-shell-left: 248px;
  }
}

@media (max-width: 1240px) and (min-width: 981px) {
  .analytics-drawer-shell {
    --analytics-drawer-shell-left: 228px;
  }
}

@media (max-width: 980px) {
  .analytics-drawer-shell {
    --analytics-drawer-shell-left: 0px;
    --analytics-drawer-shell-top: 82px;
  }

  .analytics-drawer-panel {
    width: min(420px, calc(100vw - 24px));
  }
}

@media (max-width: 820px) {
  .analytics-drawer-panel {
    top: auto;
    right: 8px;
    bottom: 8px;
    left: 8px;
    width: auto;
    max-height: min(74vh, 680px);
    border-radius: var(--qaly-radius-lg) var(--qaly-radius-lg) var(--qaly-radius-md) var(--qaly-radius-md);
  }

  .analytics-drawer-shell-enter-from .analytics-drawer-panel,
  .analytics-drawer-shell-leave-to .analytics-drawer-panel {
    transform: translateY(18px);
  }
}
</style>
