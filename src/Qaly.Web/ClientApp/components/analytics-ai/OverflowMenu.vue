<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { MoreHorizontal } from 'lucide-vue-next'
import type { Component } from 'vue'

type OverflowMenuItem = {
  key: string
  label: string
  icon?: Component
  danger?: boolean
}

const props = withDefaults(defineProps<{
  items: OverflowMenuItem[]
  ariaLabel?: string
  align?: 'start' | 'end'
  triggerTitle?: string
}>(), {
  ariaLabel: 'Tùy chọn',
  align: 'end',
  triggerTitle: 'Tùy chọn'
})

const emit = defineEmits<{
  select: [key: string]
}>()

const isOpen = ref(false)
const rootRef = ref<HTMLElement | null>(null)
const triggerRef = ref<HTMLButtonElement | null>(null)
const menuRef = ref<HTMLElement | null>(null)

const menuId = computed(() => `overflow-menu-${Math.random().toString(36).slice(2, 8)}`)

function focusItem(index: number) {
  const menu = menuRef.value
  if (!menu) return
  const buttons = Array.from(menu.querySelectorAll<HTMLButtonElement>('[role="menuitem"]'))
  if (!buttons.length) return
  const clamped = (index + buttons.length) % buttons.length
  buttons[clamped]?.focus()
}

function open() {
  if (isOpen.value) return
  isOpen.value = true
  nextTick(() => focusItem(0))
}

function close(returnFocus = true) {
  if (!isOpen.value) return
  isOpen.value = false
  if (returnFocus) nextTick(() => triggerRef.value?.focus())
}

function toggle() {
  if (isOpen.value) close()
  else open()
}

function handleSelect(key: string) {
  emit('select', key)
  close()
}

function currentIndex() {
  const menu = menuRef.value
  if (!menu) return -1
  const buttons = Array.from(menu.querySelectorAll<HTMLButtonElement>('[role="menuitem"]'))
  return buttons.indexOf(document.activeElement as HTMLButtonElement)
}

function handleMenuKeydown(event: KeyboardEvent) {
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      focusItem(currentIndex() + 1)
      break
    case 'ArrowUp':
      event.preventDefault()
      focusItem(currentIndex() - 1)
      break
    case 'Home':
      event.preventDefault()
      focusItem(0)
      break
    case 'End':
      event.preventDefault()
      focusItem(-1)
      break
    case 'Escape':
      event.preventDefault()
      close()
      break
    case 'Tab':
      close(false)
      break
  }
}

function handlePointerDown(event: PointerEvent) {
  const target = event.target as Node | null
  if (target && rootRef.value?.contains(target)) return
  close(false)
}

onMounted(() => {
  document.addEventListener('pointerdown', handlePointerDown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', handlePointerDown)
})
</script>

<template>
  <div ref="rootRef" class="overflow-menu" :class="{ 'is-open': isOpen }">
    <button
      ref="triggerRef"
      class="overflow-menu-trigger"
      type="button"
      :aria-label="ariaLabel"
      :title="triggerTitle"
      aria-haspopup="menu"
      :aria-expanded="isOpen"
      :aria-controls="menuId"
      @click="toggle"
    >
      <slot name="trigger">
        <MoreHorizontal :size="18" aria-hidden="true" />
      </slot>
    </button>

    <Transition name="overflow-menu-pop">
      <div
        v-if="isOpen"
        :id="menuId"
        ref="menuRef"
        class="overflow-menu-list"
        :class="{ 'align-start': align === 'start' }"
        role="menu"
        :aria-label="ariaLabel"
        @keydown="handleMenuKeydown"
      >
        <button
          v-for="item in items"
          :key="item.key"
          class="overflow-menu-item"
          :class="{ 'is-danger': item.danger }"
          type="button"
          role="menuitem"
          @click="handleSelect(item.key)"
        >
          <component :is="item.icon" v-if="item.icon" :size="15" aria-hidden="true" />
          <span>{{ item.label }}</span>
        </button>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.overflow-menu {
  position: relative;
  display: inline-flex;
}

.overflow-menu.is-open {
  z-index: 1500;
}

.overflow-menu-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  transition: background-color 0.14s ease, color 0.14s ease, border-color 0.14s ease;
}

.overflow-menu-trigger:hover,
.overflow-menu-trigger:focus-visible {
  background: var(--panel-soft);
  color: var(--text-strong);
  outline: none;
}

.overflow-menu-trigger:focus-visible {
  border-color: var(--primary);
  box-shadow: var(--qaly-focus-ring);
}

.overflow-menu-list {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  min-width: 200px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 6px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--panel);
  box-shadow: var(--qaly-shadow-md);
  z-index: 1501;
}

.overflow-menu-list.align-start {
  right: auto;
  left: 0;
}

.overflow-menu-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 9px 10px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: var(--text);
  font: inherit;
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.14s ease, color 0.14s ease;
}

.overflow-menu-item svg {
  flex: 0 0 auto;
  color: var(--muted);
}

.overflow-menu-item:hover,
.overflow-menu-item:focus-visible {
  background: var(--primary-soft);
  color: var(--primary-strong);
  outline: none;
}

.overflow-menu-item:hover svg,
.overflow-menu-item:focus-visible svg {
  color: var(--primary-strong);
}

.overflow-menu-item.is-danger {
  color: var(--danger);
}

.overflow-menu-item.is-danger svg {
  color: var(--danger);
}

.overflow-menu-item.is-danger:hover,
.overflow-menu-item.is-danger:focus-visible {
  background: var(--danger-soft);
  color: var(--danger);
}

.overflow-menu-pop-enter-active,
.overflow-menu-pop-leave-active {
  transition: opacity 0.14s ease, transform 0.14s ease;
}

.overflow-menu-pop-enter-from,
.overflow-menu-pop-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

@media (prefers-reduced-motion: reduce) {
  .overflow-menu-trigger,
  .overflow-menu-item {
    transition: none;
  }

  .overflow-menu-pop-enter-active,
  .overflow-menu-pop-leave-active {
    transition: none;
  }
}
</style>
