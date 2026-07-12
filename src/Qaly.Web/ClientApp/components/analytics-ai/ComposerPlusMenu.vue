<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  ArrowLeft,
  Check,
  Clock3,
  Database,
  FolderOpen,
  Lightbulb,
  Paperclip,
  Plus,
  Settings2,
  Wrench,
  X
} from 'lucide-vue-next'
import type { AiToolbarAction, AnalyticsMiniTab } from './types'

type PromptSuggestion = { label: string; prompt: string }
type ProjectOption = { id: string; name: string }

const props = defineProps<{
  projects: ProjectOption[]
  selectedTarget: string
  suggestions: PromptSuggestion[]
  tools: AiToolbarAction[]
  isCompact?: boolean
  hideSystem?: boolean
}>()

const emit = defineEmits<{
  attach: []
  'select-project': [id: string]
  'fill-prompt': [prompt: string]
  'open-drawer': [tab: AnalyticsMiniTab]
  'open-change': [open: boolean]
}>()

type MenuView = 'root' | 'suggestions' | 'tools' | 'projects'

const isOpen = ref(false)
const view = ref<MenuView>('root')
const rootRef = ref<HTMLElement | null>(null)
const triggerRef = ref<HTMLButtonElement | null>(null)
const panelRef = ref<HTMLElement | null>(null)

const panelTitle = computed(() => {
  switch (view.value) {
    case 'suggestions':
      return 'Câu hỏi gợi ý'
    case 'tools':
      return 'Công cụ phân tích'
    case 'projects':
      return 'Chọn dự án'
    default:
      return 'Thêm'
  }
})

watch(isOpen, open => {
  emit('open-change', open)
  if (open) {
    view.value = 'root'
    nextTick(() => focusFirst())
  }
})

function focusFirst() {
  const panel = panelRef.value
  if (!panel) return
  const focusable = panel.querySelector<HTMLElement>('[data-menu-focus]')
  focusable?.focus()
}

function openMenu() {
  isOpen.value = true
}

function closeMenu(returnFocus = true) {
  if (!isOpen.value) return
  isOpen.value = false
  view.value = 'root'
  if (returnFocus) nextTick(() => triggerRef.value?.focus())
}

function toggleMenu() {
  if (isOpen.value) closeMenu()
  else openMenu()
}

function goto(next: MenuView) {
  view.value = next
  nextTick(() => focusFirst())
}

function back() {
  view.value = 'root'
  nextTick(() => focusFirst())
}

function onAttach() {
  emit('attach')
  closeMenu()
}

function onSelectProject(id: string) {
  emit('select-project', id)
  closeMenu()
}

function onSuggestion(prompt: string) {
  emit('fill-prompt', prompt)
  closeMenu()
}

function onTool(tool: AiToolbarAction) {
  if (tool.behavior === 'open-drawer') {
    emit('open-drawer', tool.tab)
  } else if (tool.prompt) {
    emit('fill-prompt', tool.prompt)
  }
  closeMenu()
}

function onDrawer(tab: AnalyticsMiniTab) {
  emit('open-drawer', tab)
  closeMenu()
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && isOpen.value) {
    event.preventDefault()
    if (view.value !== 'root') back()
    else closeMenu()
  }
}

function handlePointerDown(event: PointerEvent) {
  const target = event.target as Node | null
  if (target && rootRef.value?.contains(target)) return
  closeMenu(false)
}

onMounted(() => {
  document.addEventListener('pointerdown', handlePointerDown)
  document.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  emit('open-change', false)
  document.removeEventListener('pointerdown', handlePointerDown)
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div ref="rootRef" class="composer-plus" :class="{ 'is-open': isOpen, 'is-compact': isCompact }">
    <button
      ref="triggerRef"
      class="composer-plus-trigger"
      type="button"
      aria-label="Mở chức năng"
      title="Thêm chức năng"
      aria-haspopup="menu"
      :aria-expanded="isOpen"
      @click="toggleMenu"
    >
      <Plus :size="20" aria-hidden="true" />
    </button>

    <Transition :name="isCompact ? 'plus-sheet' : 'plus-pop'">
      <div
        v-if="isOpen"
        ref="panelRef"
        class="composer-plus-panel"
        role="menu"
        :aria-label="panelTitle"
      >
        <header v-if="view !== 'root'" class="composer-plus-head">
          <button
            class="composer-plus-back"
            type="button"
            aria-label="Quay lại"
            data-menu-focus
            @click="back"
          >
            <ArrowLeft :size="16" aria-hidden="true" />
          </button>
          <strong>{{ panelTitle }}</strong>
          <button
            v-if="isCompact"
            class="composer-plus-close"
            type="button"
            aria-label="Đóng"
            @click="closeMenu()"
          >
            <X :size="16" aria-hidden="true" />
          </button>
        </header>

        <!-- ROOT -->
        <div v-if="view === 'root'" class="composer-plus-groups">
          <div class="composer-plus-group">
            <p class="composer-plus-label">Nhập liệu</p>
            <button class="composer-plus-item" type="button" role="menuitem" data-menu-focus @click="onAttach">
              <Paperclip :size="16" aria-hidden="true" />
              <span>Đính kèm tệp</span>
            </button>
            <button class="composer-plus-item" type="button" role="menuitem" @click="goto('projects')">
              <FolderOpen :size="16" aria-hidden="true" />
              <span>Chọn dự án</span>
              <small class="composer-plus-chevron" aria-hidden="true">›</small>
            </button>
          </div>

          <div class="composer-plus-group">
            <p class="composer-plus-label">Phân tích</p>
            <button class="composer-plus-item" type="button" role="menuitem" @click="goto('suggestions')">
              <Lightbulb :size="16" aria-hidden="true" />
              <span>Câu hỏi gợi ý</span>
              <small class="composer-plus-chevron" aria-hidden="true">›</small>
            </button>
            <button class="composer-plus-item" type="button" role="menuitem" @click="goto('tools')">
              <Wrench :size="16" aria-hidden="true" />
              <span>Công cụ phân tích</span>
              <small class="composer-plus-chevron" aria-hidden="true">›</small>
            </button>
          </div>

          <div v-if="!hideSystem" class="composer-plus-group">
            <p class="composer-plus-label">Hệ thống</p>
            <button class="composer-plus-item" type="button" role="menuitem" @click="onDrawer('history')">
              <Clock3 :size="16" aria-hidden="true" />
              <span>Lịch sử</span>
            </button>
            <button class="composer-plus-item" type="button" role="menuitem" @click="onDrawer('sources')">
              <Database :size="16" aria-hidden="true" />
              <span>Nguồn dữ liệu</span>
            </button>
            <button class="composer-plus-item" type="button" role="menuitem" @click="onDrawer('model')">
              <Settings2 :size="16" aria-hidden="true" />
              <span>Thiết lập model</span>
            </button>
          </div>
        </div>

        <!-- SUGGESTIONS -->
        <div v-else-if="view === 'suggestions'" class="composer-plus-group">
          <button
            v-for="(s, idx) in suggestions"
            :key="idx"
            class="composer-plus-item is-stacked"
            type="button"
            role="menuitem"
            @click="onSuggestion(s.prompt)"
          >
            <span>{{ s.label }}</span>
          </button>
        </div>

        <!-- TOOLS -->
        <div v-else-if="view === 'tools'" class="composer-plus-group">
          <button
            v-for="tool in tools"
            :key="tool.key"
            class="composer-plus-item is-stacked"
            type="button"
            role="menuitem"
            :title="tool.description"
            @click="onTool(tool)"
          >
            <span>{{ tool.label }}</span>
            <small v-if="tool.description">{{ tool.description }}</small>
          </button>
        </div>

        <!-- PROJECTS -->
        <div v-else class="composer-plus-group">
          <button
            class="composer-plus-item"
            type="button"
            role="menuitem"
            @click="onSelectProject('workspace')"
          >
            <span>Tất cả dự án</span>
            <Check v-if="selectedTarget === 'workspace'" :size="15" class="composer-plus-check" aria-hidden="true" />
          </button>
          <button
            v-for="p in projects"
            :key="p.id"
            class="composer-plus-item"
            type="button"
            role="menuitem"
            @click="onSelectProject(p.id)"
          >
            <span>{{ p.name }}</span>
            <Check v-if="selectedTarget === p.id" :size="15" class="composer-plus-check" aria-hidden="true" />
          </button>
        </div>
      </div>
    </Transition>

    <button
      v-if="isOpen && isCompact"
      class="composer-plus-backdrop"
      type="button"
      tabindex="-1"
      aria-label="Đóng"
      @click="closeMenu(false)"
    ></button>
  </div>
</template>

<style scoped>
.composer-plus {
  position: relative;
  display: inline-flex;
}

.composer-plus.is-open {
  z-index: 1450;
}

.composer-plus-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel);
  color: var(--text);
  cursor: pointer;
  transition: background-color 0.14s ease, border-color 0.14s ease, color 0.14s ease;
}

.composer-plus-trigger:hover,
.composer-plus.is-open .composer-plus-trigger {
  background: var(--panel-soft);
  border-color: var(--primary);
  color: var(--primary-strong);
}

.composer-plus-trigger:focus-visible {
  outline: none;
  border-color: var(--primary);
  box-shadow: var(--qaly-focus-ring);
}

.composer-plus-panel {
  position: absolute;
  left: 0;
  bottom: calc(100% + 8px);
  width: 288px;
  max-height: min(60vh, 460px);
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 10px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--panel);
  box-shadow: var(--qaly-shadow-md);
  z-index: 1451;
}

.composer-plus-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--line);
}

.composer-plus-head strong {
  flex: 1;
  color: var(--text-strong);
  font-size: 13px;
  font-weight: 700;
}

.composer-plus-back,
.composer-plus-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  transition: background-color 0.14s ease, color 0.14s ease;
}

.composer-plus-back:hover,
.composer-plus-back:focus-visible,
.composer-plus-close:hover,
.composer-plus-close:focus-visible {
  background: var(--panel-soft);
  color: var(--text-strong);
  outline: none;
}

.composer-plus-back:focus-visible,
.composer-plus-close:focus-visible {
  border-color: var(--primary);
  box-shadow: var(--qaly-focus-ring);
}

.composer-plus-groups {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.composer-plus-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.composer-plus-group + .composer-plus-group {
  padding-top: 8px;
  border-top: 1px solid var(--line);
}

.composer-plus-label {
  margin: 2px 4px 4px;
  color: var(--muted);
  font-size: 11px;
  font-weight: 600;
}

.composer-plus-item {
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

.composer-plus-item > span {
  flex: 1;
  min-width: 0;
}

.composer-plus-item svg {
  flex: 0 0 auto;
  color: var(--muted);
}

.composer-plus-item.is-stacked {
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
}

.composer-plus-item.is-stacked small {
  color: var(--muted);
  font-size: 11px;
  font-weight: 400;
  line-height: 1.35;
}

.composer-plus-chevron {
  color: var(--muted);
  font-size: 16px;
  line-height: 1;
}

.composer-plus-check {
  color: var(--primary-strong);
}

.composer-plus-item:hover,
.composer-plus-item:focus-visible {
  background: var(--primary-soft);
  color: var(--primary-strong);
  outline: none;
}

.composer-plus-item:hover svg,
.composer-plus-item:focus-visible svg {
  color: var(--primary-strong);
}

.composer-plus-backdrop {
  display: none;
}

/* Transitions */
.plus-pop-enter-active,
.plus-pop-leave-active {
  transition: opacity 0.14s ease, transform 0.14s ease;
}

.plus-pop-enter-from,
.plus-pop-leave-to {
  opacity: 0;
  transform: translateY(6px);
}

.plus-sheet-enter-active,
.plus-sheet-leave-active {
  transition: transform 0.16s ease, opacity 0.16s ease;
}

.plus-sheet-enter-from,
.plus-sheet-leave-to {
  opacity: 0;
  transform: translateY(24px);
}

/* Mobile bottom sheet */
.composer-plus.is-compact .composer-plus-panel {
  position: fixed;
  left: 8px;
  right: 8px;
  bottom: 8px;
  width: auto;
  max-height: min(70vh, 560px);
  border-radius: 14px;
  z-index: 1701;
}

.composer-plus.is-compact .composer-plus-backdrop {
  display: block;
  position: fixed;
  inset: 0;
  border: 0;
  background: rgba(15, 23, 42, 0.28);
  z-index: 1700;
}

@media (prefers-reduced-motion: reduce) {
  .composer-plus-trigger,
  .composer-plus-item,
  .composer-plus-back,
  .composer-plus-close {
    transition: none;
  }

  .plus-pop-enter-active,
  .plus-pop-leave-active,
  .plus-sheet-enter-active,
  .plus-sheet-leave-active {
    transition: none;
  }
}
</style>
