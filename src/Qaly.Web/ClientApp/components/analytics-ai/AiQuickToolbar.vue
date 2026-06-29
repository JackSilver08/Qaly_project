<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  BarChart3,
  Brain,
  Clock3,
  Command,
  Database,
  FileText,
  Gauge,
  ListChecks,
  Settings2,
  ShieldAlert,
  Wrench
} from 'lucide-vue-next'
import type { AiToolbarAction, AnalyticsMiniTab } from './types'

defineProps<{
  activeTab?: AnalyticsMiniTab
  historyCount?: number
}>()

const emit = defineEmits<{
  select: [action: AiToolbarAction]
  'palette-open-change': [open: boolean]
}>()

const isPaletteOpen = ref(false)
const rootRef = ref<HTMLElement | null>(null)

const toolActions: AiToolbarAction[] = [
  {
    key: 'risks',
    label: 'Rủi ro',
    description: 'Tìm dự án, task hoặc tiến độ cần chú ý.',
    tab: 'risks',
    behavior: 'fill-prompt',
    prompt: 'Phân tích các rủi ro hiện tại và đề xuất bước xử lý tiếp theo.'
  },
  {
    key: 'report',
    label: 'Báo cáo',
    description: 'Soạn bản nháp báo cáo theo dữ liệu hiện tại.',
    tab: 'report',
    behavior: 'fill-prompt',
    prompt: 'Tạo báo cáo tuần này dựa trên dữ liệu hiện tại.'
  },
  {
    key: 'sources',
    label: 'Nguồn',
    description: 'Xem nguồn và nhãn dữ liệu của câu trả lời gần nhất.',
    tab: 'sources',
    behavior: 'open-drawer'
  },
  {
    key: 'insights',
    label: 'Insight',
    description: 'Tóm tắt các tín hiệu nổi bật.',
    tab: 'insights',
    behavior: 'fill-prompt',
    prompt: 'Tóm tắt 3 insight quan trọng nhất từ dữ liệu hiện tại.'
  },
  {
    key: 'metrics',
    label: 'Số liệu',
    description: 'Xem metric từ phản hồi gần nhất.',
    tab: 'metrics',
    behavior: 'open-drawer'
  },
  {
    key: 'actions',
    label: 'Việc cần làm',
    description: 'Tạo đề xuất dạng nháp, chưa thực thi.',
    tab: 'actions',
    behavior: 'fill-prompt',
    prompt: 'Đề xuất các hành động tiếp theo dưới dạng bản nháp, chưa thực thi.',
    isWriteLike: true
  },
  {
    key: 'model',
    label: 'Model',
    description: 'Xem model hiện tại và provider planned.',
    tab: 'model',
    behavior: 'open-drawer'
  },
  {
    key: 'settings',
    label: 'Cài đặt AI',
    description: 'Mở registry provider UI-only.',
    tab: 'settings',
    behavior: 'open-drawer'
  }
]

const utilityActions: AiToolbarAction[] = [
  {
    key: 'history',
    label: 'Lịch sử',
    description: 'Xem các prompt gần đây.',
    tab: 'history',
    behavior: 'open-drawer'
  },
  {
    key: 'command',
    label: 'Lệnh',
    description: 'Mở slash commands.',
    tab: 'insights',
    behavior: 'fill-prompt',
    prompt: '/'
  }
]

const visibleActions = computed(() => toolActions.slice(0, 3))
const paletteActions = computed(() => [...toolActions.slice(3), utilityActions[0]])

const iconByKey = {
  risks: ShieldAlert,
  report: FileText,
  sources: Database,
  insights: Brain,
  metrics: Gauge,
  actions: ListChecks,
  model: BarChart3,
  settings: Settings2,
  history: Clock3,
  command: Command
}

function openPalette() {
  isPaletteOpen.value = true
}

function closePalette() {
  isPaletteOpen.value = false
}

function togglePalette() {
  isPaletteOpen.value = !isPaletteOpen.value
}

function handleSelect(action: AiToolbarAction) {
  emit('select', action)
  closePalette()
}

function handlePointerDown(event: PointerEvent) {
  const target = event.target as Node | null
  if (target && rootRef.value?.contains(target)) return
  closePalette()
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') closePalette()
}

watch(isPaletteOpen, open => {
  emit('palette-open-change', open)
})

onMounted(() => {
  document.addEventListener('pointerdown', handlePointerDown)
  document.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  emit('palette-open-change', false)
  document.removeEventListener('pointerdown', handlePointerDown)
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <nav ref="rootRef" class="ai-quick-toolbar" :class="{ 'is-palette-open': isPaletteOpen }" aria-label="Công cụ phân tích nhanh">
    <div class="ai-quick-primary">
      <button
        v-for="action in visibleActions"
        :key="action.key"
        class="ai-quick-tool"
        :class="{ 'is-active': activeTab === action.tab }"
        type="button"
        :aria-label="action.label"
        :title="action.description || action.label"
        @click="handleSelect(action)"
      >
        <component :is="iconByKey[action.key as keyof typeof iconByKey]" :size="15" aria-hidden="true" />
        <span>{{ action.label }}</span>
      </button>
    </div>

    <div class="ai-quick-utilities">
      <button
        class="ai-quick-utility"
        :class="{ 'is-active': isPaletteOpen }"
        type="button"
        aria-haspopup="dialog"
        :aria-expanded="isPaletteOpen"
        aria-label="Mở công cụ Erumi"
        title="Công cụ"
        @click="togglePalette"
      >
        <Wrench :size="15" aria-hidden="true" />
        <span>Công cụ</span>
      </button>

      <button
        class="ai-quick-utility"
        :class="{ 'is-active': activeTab === 'history' }"
        type="button"
        aria-label="Mở lịch sử trò chuyện"
        title="Lịch sử"
        @click="handleSelect(utilityActions[0])"
      >
        <Clock3 :size="15" aria-hidden="true" />
        <span>Lịch sử</span>
        <small v-if="historyCount">{{ historyCount }}</small>
      </button>

      <button
        class="ai-quick-utility is-icon-only"
        type="button"
        aria-label="Mở lệnh nhanh"
        title="Lệnh"
        @click="handleSelect(utilityActions[1])"
      >
        <Command :size="15" aria-hidden="true" />
      </button>
    </div>

    <Transition name="ai-tool-palette">
      <div v-if="isPaletteOpen" class="ai-tool-palette" role="dialog" aria-label="Công cụ Erumi">
        <header>
          <strong>Công cụ Erumi</strong>
          <span>Chọn tiện ích khi cần, không chiếm chỗ composer.</span>
        </header>

        <div class="ai-tool-palette-grid">
          <button
            v-for="action in paletteActions"
            :key="action.key"
            class="ai-tool-palette-item"
            type="button"
            :aria-label="action.label"
            :title="action.isWriteLike ? `${action.label}: chỉ điền prompt, không thực thi` : action.description"
            @click="handleSelect(action)"
          >
            <component :is="iconByKey[action.key as keyof typeof iconByKey]" :size="16" aria-hidden="true" />
            <span>
              <strong>{{ action.label }}</strong>
              <small>{{ action.description }}</small>
            </span>
          </button>
        </div>
      </div>
    </Transition>
  </nav>
</template>

<style scoped>
.ai-quick-toolbar {
  position: relative;
  width: 100%;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 0;
}

.ai-quick-toolbar.is-palette-open {
  z-index: 1450;
}

.ai-quick-primary,
.ai-quick-utilities {
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.ai-quick-primary {
  overflow-x: auto;
  scrollbar-width: none;
}

.ai-quick-primary::-webkit-scrollbar {
  display: none;
}

.ai-quick-tool,
.ai-quick-utility,
.ai-tool-palette-item {
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #335274;
  cursor: pointer;
  transition: border-color 0.16s ease, background 0.16s ease, color 0.16s ease, transform 0.16s ease;
}

.ai-quick-tool,
.ai-quick-utility {
  flex: 0 0 auto;
  height: 30px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0 9px;
  font-size: 11px;
  font-weight: 850;
  white-space: nowrap;
}

.ai-quick-utility.is-icon-only {
  width: 30px;
  padding: 0;
}

.ai-quick-utility small {
  min-width: 16px;
  height: 16px;
  display: inline-grid;
  place-items: center;
  border-radius: 999px;
  background: #eef4ff;
  color: #1d4ed8;
  font-size: 10px;
  font-weight: 900;
}

.ai-quick-tool:hover,
.ai-quick-tool:focus-visible,
.ai-quick-tool.is-active,
.ai-quick-utility:hover,
.ai-quick-utility:focus-visible,
.ai-quick-utility.is-active,
.ai-tool-palette-item:hover,
.ai-tool-palette-item:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  color: #1d4ed8;
  outline: none;
}

.ai-quick-tool:active,
.ai-quick-utility:active,
.ai-tool-palette-item:active {
  transform: translateY(1px);
}

.ai-tool-palette {
  position: absolute;
  right: 0;
  bottom: calc(100% + 10px);
  width: min(420px, calc(100vw - 32px));
  max-height: min(420px, calc(100vh - 160px));
  overflow-y: auto;
  display: grid;
  gap: 10px;
  border: 1px solid rgba(15, 23, 42, 0.1);
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.98);
  box-shadow: var(--qaly-shadow-md);
  padding: 12px;
  z-index: 1451;
}

.ai-tool-palette header {
  min-width: 0;
  display: grid;
  gap: 2px;
  text-align: left;
}

.ai-tool-palette header strong {
  color: #0f172a;
  font-size: 13px;
  font-weight: 900;
}

.ai-tool-palette header span {
  color: #64748b;
  font-size: 11px;
  font-weight: 650;
}

.ai-tool-palette-grid {
  min-width: 0;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.ai-tool-palette-item {
  min-width: 0;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: 9px;
  padding: 10px;
  text-align: left;
}

.ai-tool-palette-item > span {
  min-width: 0;
  display: grid;
  gap: 2px;
}

.ai-tool-palette-item strong,
.ai-tool-palette-item small {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-tool-palette-item strong {
  color: #0f172a;
  font-size: 12px;
  font-weight: 900;
}

.ai-tool-palette-item small {
  color: #64748b;
  font-size: 11px;
  font-weight: 650;
}

.ai-tool-palette-enter-active,
.ai-tool-palette-leave-active {
  transition: opacity 0.16s ease, transform 0.16s ease;
}

.ai-tool-palette-enter-from,
.ai-tool-palette-leave-to {
  opacity: 0;
  transform: translateY(6px);
}

:global(:root[data-theme='dark']) .ai-quick-tool,
:global(:root[data-theme='dark']) .ai-quick-utility,
:global(:root[data-theme='dark']) .ai-tool-palette,
:global(:root[data-theme='dark']) .ai-tool-palette-item {
  border-color: var(--line) !important;
  background: var(--panel) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .ai-quick-tool:hover,
:global(:root[data-theme='dark']) .ai-quick-tool:focus-visible,
:global(:root[data-theme='dark']) .ai-quick-tool.is-active,
:global(:root[data-theme='dark']) .ai-quick-utility:hover,
:global(:root[data-theme='dark']) .ai-quick-utility:focus-visible,
:global(:root[data-theme='dark']) .ai-quick-utility.is-active,
:global(:root[data-theme='dark']) .ai-tool-palette-item:hover,
:global(:root[data-theme='dark']) .ai-tool-palette-item:focus-visible {
  border-color: rgba(96, 165, 250, 0.46) !important;
  background: var(--panel-soft) !important;
  color: var(--primary-strong) !important;
}

:global(:root[data-theme='dark']) .ai-tool-palette header strong,
:global(:root[data-theme='dark']) .ai-tool-palette-item strong {
  color: var(--text-strong) !important;
}

:global(:root[data-theme='dark']) .ai-tool-palette header span,
:global(:root[data-theme='dark']) .ai-tool-palette-item small {
  color: var(--muted) !important;
}

@media (max-width: 720px) {
  .ai-quick-toolbar {
    justify-content: flex-start;
    gap: 6px;
  }

  .ai-quick-primary {
    flex: 1 1 auto;
    justify-content: flex-start;
  }

  .ai-quick-utilities {
    flex: 0 0 auto;
  }

  .ai-quick-tool span,
  .ai-quick-utility span {
    max-width: 58px;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .ai-tool-palette {
    position: fixed;
    left: 8px;
    right: 8px;
    bottom: 8px;
    width: auto;
    max-height: min(62vh, 520px);
    border-radius: var(--qaly-radius-lg);
  }

  .ai-tool-palette-grid {
    grid-template-columns: 1fr;
  }
}
</style>
