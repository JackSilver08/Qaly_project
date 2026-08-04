<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { Check, ChevronDown, Settings2 } from 'lucide-vue-next'
import type { AiModelOption } from './types'
import { aiModelCompactLabel, aiModelStatusLabel } from './types'

const props = withDefaults(defineProps<{
  modelValue: string
  options: AiModelOption[]
  compact?: boolean
}>(), {
  compact: false
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'open-settings': []
}>()

const isOpen = ref(false)
const rootRef = ref<HTMLElement | null>(null)
const menuPlacement = ref<'up' | 'down'>('down')
const menuMaxHeight = ref('min(520px, calc(100vh - 160px))')

const selectedOption = computed(() => {
  return props.options.find(option => option.id === props.modelValue) ?? props.options[0]
})

const triggerLabel = computed(() => {
  const option = selectedOption.value
  return option ? `Model: ${aiModelCompactLabel(option)}` : 'Model'
})

const triggerText = computed(() => {
  const option = selectedOption.value
  return props.compact ? (option?.shortLabel || option?.label || 'Model') : (option?.label || 'Model')
})

function updateMenuPlacement() {
  const root = rootRef.value
  if (!root) return

  const rect = root.getBoundingClientRect()
  const gap = 8
  const viewportMargin = 16
  const availableBelow = window.innerHeight - rect.bottom - gap - viewportMargin
  const availableAbove = rect.top - gap - viewportMargin
  const shouldOpenUp = availableBelow < 260 && availableAbove > availableBelow
  const availableSpace = Math.max(180, shouldOpenUp ? availableAbove : availableBelow)

  menuPlacement.value = shouldOpenUp ? 'up' : 'down'
  menuMaxHeight.value = `${Math.min(520, Math.floor(availableSpace))}px`
}

function toggleOpen() {
  isOpen.value = !isOpen.value
  if (isOpen.value) {
    nextTick(updateMenuPlacement)
  }
}

function selectOption(option: AiModelOption) {
  if (option.disabled) return
  emit('update:modelValue', option.id)
  isOpen.value = false
}

function handlePointerDown(event: PointerEvent) {
  const target = event.target as Node | null
  if (target && rootRef.value?.contains(target)) return
  isOpen.value = false
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    isOpen.value = false
  }
}

function handleResize() {
  if (isOpen.value) updateMenuPlacement()
}

onMounted(() => {
  document.addEventListener('pointerdown', handlePointerDown)
  document.addEventListener('keydown', handleKeydown)
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', handlePointerDown)
  document.removeEventListener('keydown', handleKeydown)
  window.removeEventListener('resize', handleResize)
})
</script>

<template>
  <div ref="rootRef" class="ai-model-selector" :class="{ 'is-compact': compact, 'is-open': isOpen }">
    <button
      class="ai-model-trigger"
      type="button"
      aria-haspopup="listbox"
      :aria-expanded="isOpen"
      :title="selectedOption?.tooltip || selectedOption?.description || triggerLabel"
      @click="toggleOpen"
    >
      <span class="ai-model-dot" :class="`status-${selectedOption?.status || 'planned'}`" aria-hidden="true"></span>
      <span class="ai-model-trigger-text">{{ triggerText }}</span>
      <span class="ai-model-status">{{ selectedOption?.badge || aiModelStatusLabel(selectedOption?.status || 'planned') }}</span>
      <ChevronDown :size="14" aria-hidden="true" />
    </button>

    <Transition name="ai-model-menu">
      <div
        v-if="isOpen"
        class="ai-model-menu"
        :class="{ 'opens-up': menuPlacement === 'up' }"
        :style="{ maxHeight: menuMaxHeight }"
        role="listbox"
        aria-label="Chọn model phân tích"
      >
        <button
          v-for="option in options"
          :key="option.id"
          class="ai-model-option"
          :class="{ 'is-selected': option.id === modelValue, 'is-disabled': option.disabled }"
          type="button"
          role="option"
          :aria-selected="option.id === modelValue"
          :disabled="option.disabled"
          :title="option.tooltip || option.description"
          @click="selectOption(option)"
        >
          <span class="ai-model-option-dot" :class="`status-${option.status}`" aria-hidden="true"></span>
          <span class="ai-model-option-copy">
            <strong>{{ option.label }}</strong>
            <small>{{ option.description }}</small>
            <em v-if="option.privacyNote">{{ option.privacyNote }}</em>
          </span>
          <span class="ai-model-option-badge">{{ option.badge || aiModelStatusLabel(option.status) }}</span>
          <Check v-if="option.id === modelValue" :size="15" aria-hidden="true" />
        </button>

        <button class="ai-model-settings" type="button" @click="emit('open-settings')">
          <Settings2 :size="15" aria-hidden="true" />
          <span>Xem registry provider</span>
        </button>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.ai-model-selector {
  position: relative;
  min-width: 0;
  flex: 0 1 auto;
}

.ai-model-selector.is-open {
  z-index: 1500;
}

.ai-model-trigger {
  min-width: 0;
  max-width: 100%;
  height: 34px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #0f172a;
  padding: 0 10px;
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
}

.ai-model-trigger:hover,
.ai-model-trigger:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  outline: none;
}

.ai-model-selector.is-compact .ai-model-trigger {
  width: 34px;
  padding: 0;
  justify-content: center;
}

.ai-model-selector.is-compact .ai-model-trigger-text,
.ai-model-selector.is-compact .ai-model-status,
.ai-model-selector.is-compact .ai-model-trigger > svg {
  display: none;
}

.ai-model-trigger-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-model-status,
.ai-model-option-badge {
  flex: 0 0 auto;
  border-radius: 999px;
  background: #eef4ff;
  color: #1d4ed8;
  padding: 2px 7px;
  font-size: 10px;
  font-weight: 900;
}

.ai-model-dot,
.ai-model-option-dot {
  width: 8px;
  height: 8px;
  flex: 0 0 auto;
  border-radius: 50%;
  background: #94a3b8;
}

.status-live {
  background: #10b981;
}

.status-mock,
.status-fallback {
  background: #64748b;
}

.status-planned,
.status-budget,
.status-privacy {
  background: #f59e0b;
}

.ai-model-menu {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  width: min(360px, calc(100vw - 32px));
  max-height: min(520px, calc(100vh - 160px));
  overflow-y: auto;
  display: grid;
  gap: 6px;
  border: 1px solid rgba(15, 23, 42, 0.1);
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
  padding: 8px;
  z-index: 1501;
}

.ai-model-menu.opens-up {
  top: auto;
  bottom: calc(100% + 8px);
}

.ai-model-option,
.ai-model-settings {
  width: 100%;
  display: grid;
  align-items: center;
  gap: 10px;
  border: 1px solid transparent;
  border-radius: var(--qaly-radius-lg);
  background: transparent;
  color: #0f172a;
  padding: 10px;
  text-align: left;
  cursor: pointer;
}

.ai-model-option {
  grid-template-columns: auto minmax(0, 1fr) auto auto;
}

.ai-model-settings {
  grid-template-columns: auto minmax(0, 1fr);
  color: #1d4ed8;
  font-weight: 800;
}

.ai-model-option:hover:not(:disabled),
.ai-model-option:focus-visible:not(:disabled),
.ai-model-settings:hover,
.ai-model-settings:focus-visible {
  border-color: #bfdbfe;
  background: #f8fbff;
  outline: none;
}

.ai-model-option.is-selected {
  border-color: #bfdbfe;
  background: #eff6ff;
}

.ai-model-option.is-disabled {
  cursor: not-allowed;
  opacity: 0.68;
}

.ai-model-option-copy {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.ai-model-option-copy strong,
.ai-model-option-copy small,
.ai-model-option-copy em {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ai-model-option-copy strong {
  color: #0f172a;
  font-size: 13px;
}

.ai-model-option-copy small,
.ai-model-option-copy em {
  color: #64748b;
  font-size: 11px;
  font-style: normal;
  line-height: 1.35;
}

.ai-model-menu-enter-active,
.ai-model-menu-leave-active {
  transition: opacity 0.16s ease, transform 0.16s ease;
}

.ai-model-menu-enter-from,
.ai-model-menu-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

:global(:root[data-theme='dark']) .ai-model-trigger,
:global(:root[data-theme='dark']) .ai-model-menu,
:global(:root[data-theme='dark']) .ai-model-option,
:global(:root[data-theme='dark']) .ai-model-settings {
  border-color: var(--line) !important;
  background: var(--panel) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .ai-model-trigger:hover,
:global(:root[data-theme='dark']) .ai-model-trigger:focus-visible,
:global(:root[data-theme='dark']) .ai-model-option:hover:not(:disabled),
:global(:root[data-theme='dark']) .ai-model-option:focus-visible:not(:disabled),
:global(:root[data-theme='dark']) .ai-model-option.is-selected,
:global(:root[data-theme='dark']) .ai-model-settings:hover,
:global(:root[data-theme='dark']) .ai-model-settings:focus-visible {
  background: var(--panel-soft) !important;
}

:global(:root[data-theme='dark']) .ai-model-option-copy strong {
  color: var(--text-strong) !important;
}

:global(:root[data-theme='dark']) .ai-model-option-copy small,
:global(:root[data-theme='dark']) .ai-model-option-copy em {
  color: var(--muted) !important;
}

@media (max-width: 720px) {
  .ai-model-selector,
  .ai-model-trigger {
    width: 100%;
  }

  .ai-model-menu {
    left: 0;
    right: auto;
    width: min(360px, calc(100vw - 32px));
  }
}
</style>
