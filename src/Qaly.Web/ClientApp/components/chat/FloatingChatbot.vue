<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Activity, Clock3, ListChecks, MessageSquare, RotateCcw, X, Sparkles } from 'lucide-vue-next'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import AiActionComposerDrawer from '../AiActionComposerDrawer.vue'
import ErumiChatPanel from './ErumiChatPanel.vue'
import AiActivityPanel from './AiActivityPanel.vue'

interface ProjectOption {
  id: string
  name: string
  code?: string | null
  status?: string | null
  members?: Array<{ userId?: string; id?: string; fullName: string }>
}

interface AssistantOpenRequest {
  id: number
  view: 'chat' | 'activity'
  prompt?: string
  projectId?: string | null
}

const props = defineProps<{
  projectId?: string | null
  projects: ProjectOption[]
  openRequest?: AssistantOpenRequest | null
  conversationRuntimeEnabled?: boolean
}>()

const emit = defineEmits<{
  completed: [projectId: string]
}>()

const route = useRoute()
const router = useRouter()
const isOpen = ref(false)
const activeView = ref<'chat' | 'create' | 'activity'>('chat')
const actionPrompt = ref('')
const assistantPrompt = ref('')
const assistantPromptToken = ref(0)
const assistantHistoryToken = ref(0)
const assistantProjectId = ref<string | null>(props.projectId || null)
const actionProjectId = ref<string | null>(props.projectId || null)
const actionProviderHint = ref('auto')
const actionModelProfile = ref('balanced')
const actionComposerKey = ref(0)
const artifactAvailable = ref(false)
const drawerRef = ref<HTMLElement | null>(null)
const drawerBodyRef = ref<HTMLElement | null>(null)
const isCompactViewport = ref(false)
const isResizing = ref(false)
const hasSavedLayout = ref(false)

function openAssistantHistory() {
  if (props.conversationRuntimeEnabled === false) {
    window.dispatchEvent(new CustomEvent('qaly:focus-ai-primary-runtime', {
      detail: { openHistory: true },
    }))
    return
  }
  activeView.value = 'chat'
  assistantHistoryToken.value++
}

const LAYOUT_STORAGE_KEY = 'qaly-ai-agent-workspace-layout-v1'
const LAYOUT_VERSION = 1
const MOBILE_BREAKPOINT = 800
const SPLIT_BREAKPOINT = 920
const MIN_DRAWER_WIDTH = 380
const MIN_DRAWER_HEIGHT = 520
const VIEWPORT_GUTTER = 16

type ResizeEdge = 'left' | 'top' | 'corner'

interface WorkspaceLayout {
  width: number
  height: number
  conversationRatio: number
  artifactCollapsed: boolean
}

interface PointerResizeState {
  edge: ResizeEdge
  startX: number
  startY: number
  startWidth: number
  startHeight: number
}

interface SplitResizeState {
  startX: number
  startRatio: number
  bodyWidth: number
}

const layout = reactive<WorkspaceLayout>({
  width: 440,
  height: 760,
  conversationRatio: 0.4,
  artifactCollapsed: false,
})

let pointerResizeState: PointerResizeState | null = null
let splitResizeState: SplitResizeState | null = null
let restoreFocusTarget: HTMLElement | null = null

const drawerStyle = computed(() => {
  if (isCompactViewport.value) return undefined
  return {
    width: `${layout.width}px`,
    height: `${layout.height}px`,
  }
})

const compactArtifactWorkspace = computed(
  () => isCompactViewport.value || layout.width < SPLIT_BREAKPOINT,
)

const showArtifactPane = computed(
  () => activeView.value === 'create' && !layout.artifactCollapsed,
)

const showConversationPane = computed(
  () => props.conversationRuntimeEnabled !== false
    && activeView.value !== 'activity'
    && !(activeView.value === 'create' && compactArtifactWorkspace.value && !layout.artifactCollapsed),
)

const showArtifactSplitter = computed(
  () => props.conversationRuntimeEnabled !== false
    && activeView.value === 'create'
    && !layout.artifactCollapsed
    && !compactArtifactWorkspace.value,
)

const drawerBodyStyle = computed(() => {
  if (!showArtifactSplitter.value) return undefined
  const conversation = Math.round(layout.conversationRatio * 1000)
  const artifact = 1000 - conversation
  return {
    gridTemplateColumns: `minmax(360px, ${conversation}fr) 8px minmax(480px, ${artifact}fr)`,
  }
})

function workspaceBounds() {
  const maxWidth = Math.max(320, window.innerWidth - VIEWPORT_GUTTER * 2)
  const maxHeight = Math.max(420, window.innerHeight - VIEWPORT_GUTTER * 2)
  return {
    minWidth: Math.min(MIN_DRAWER_WIDTH, maxWidth),
    maxWidth,
    minHeight: Math.min(MIN_DRAWER_HEIGHT, maxHeight),
    maxHeight,
  }
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value))
}

function clampLayout() {
  if (typeof window === 'undefined' || isCompactViewport.value) return
  const bounds = workspaceBounds()
  layout.width = clamp(layout.width, bounds.minWidth, bounds.maxWidth)
  layout.height = clamp(layout.height, bounds.minHeight, bounds.maxHeight)
  layout.conversationRatio = clamp(layout.conversationRatio, 0.36, 0.56)
}

function persistLayout() {
  if (typeof window === 'undefined' || isCompactViewport.value) return
  clampLayout()
  window.localStorage.setItem(LAYOUT_STORAGE_KEY, JSON.stringify({
    version: LAYOUT_VERSION,
    width: layout.width,
    height: layout.height,
    conversationRatio: layout.conversationRatio,
    artifactCollapsed: layout.artifactCollapsed,
  }))
  hasSavedLayout.value = true
}

function applyDefaultLayout(forArtifact = false) {
  if (typeof window === 'undefined') return
  const bounds = workspaceBounds()
  layout.width = clamp(forArtifact ? 1120 : 440, bounds.minWidth, bounds.maxWidth)
  layout.height = clamp(forArtifact ? 820 : 760, bounds.minHeight, bounds.maxHeight)
  layout.conversationRatio = 0.4
  layout.artifactCollapsed = false
}

function restoreLayout() {
  if (typeof window === 'undefined') return
  const raw = window.localStorage.getItem(LAYOUT_STORAGE_KEY)
  if (!raw) {
    applyDefaultLayout(false)
    return
  }

  try {
    const saved = JSON.parse(raw) as Partial<WorkspaceLayout> & { version?: number }
    if (
      saved.version !== LAYOUT_VERSION
      || !Number.isFinite(saved.width)
      || !Number.isFinite(saved.height)
      || !Number.isFinite(saved.conversationRatio)
    ) {
      throw new Error('Unsupported workspace layout')
    }

    layout.width = Number(saved.width)
    layout.height = Number(saved.height)
    layout.conversationRatio = Number(saved.conversationRatio)
    layout.artifactCollapsed = Boolean(saved.artifactCollapsed)
    hasSavedLayout.value = true
    clampLayout()
  } catch {
    window.localStorage.removeItem(LAYOUT_STORAGE_KEY)
    hasSavedLayout.value = false
    applyDefaultLayout(false)
  }
}

function resetAssistantLayout() {
  hasSavedLayout.value = false
  applyDefaultLayout(activeView.value === 'create')
  persistLayout()
}

function syncViewport() {
  isCompactViewport.value = window.innerWidth < MOBILE_BREAKPOINT
  if (!isCompactViewport.value) clampLayout()
}

function ensureArtifactWorkspace() {
  layout.artifactCollapsed = false
  if (!isCompactViewport.value && !hasSavedLayout.value && layout.width < SPLIT_BREAKPOINT) {
    applyDefaultLayout(true)
  }
}

function removeResizeListeners() {
  window.removeEventListener('mousemove', handlePointerResize)
  window.removeEventListener('mouseup', finishPointerResize)
  window.removeEventListener('mousemove', handleSplitResize)
  window.removeEventListener('mouseup', finishSplitResize)
}

function startPointerResize(event: MouseEvent, edge: ResizeEdge) {
  if (isCompactViewport.value) return
  event.preventDefault()
  pointerResizeState = {
    edge,
    startX: event.clientX,
    startY: event.clientY,
    startWidth: layout.width,
    startHeight: layout.height,
  }
  isResizing.value = true
  window.addEventListener('mousemove', handlePointerResize)
  window.addEventListener('mouseup', finishPointerResize)
}

function handlePointerResize(event: MouseEvent) {
  const state = pointerResizeState
  if (!state) return
  const bounds = workspaceBounds()
  if (state.edge === 'left' || state.edge === 'corner') {
    layout.width = clamp(
      state.startWidth + state.startX - event.clientX,
      bounds.minWidth,
      bounds.maxWidth,
    )
  }
  if (state.edge === 'top' || state.edge === 'corner') {
    layout.height = clamp(
      state.startHeight + state.startY - event.clientY,
      bounds.minHeight,
      bounds.maxHeight,
    )
  }
}

function finishPointerResize() {
  pointerResizeState = null
  isResizing.value = false
  removeResizeListeners()
  persistLayout()
}

function handleResizeKeydown(event: KeyboardEvent, edge: ResizeEdge) {
  if (isCompactViewport.value) return
  const step = event.shiftKey ? 8 : 24
  let changed = false
  if ((edge === 'left' || edge === 'corner') && event.key === 'ArrowLeft') {
    layout.width += step
    changed = true
  }
  if ((edge === 'left' || edge === 'corner') && event.key === 'ArrowRight') {
    layout.width -= step
    changed = true
  }
  if ((edge === 'top' || edge === 'corner') && event.key === 'ArrowUp') {
    layout.height += step
    changed = true
  }
  if ((edge === 'top' || edge === 'corner') && event.key === 'ArrowDown') {
    layout.height -= step
    changed = true
  }
  if (!changed) return
  event.preventDefault()
  clampLayout()
  persistLayout()
}

function startSplitResize(event: MouseEvent) {
  if (!showArtifactSplitter.value || !drawerBodyRef.value) return
  event.preventDefault()
  splitResizeState = {
    startX: event.clientX,
    startRatio: layout.conversationRatio,
    bodyWidth: drawerBodyRef.value.getBoundingClientRect().width,
  }
  isResizing.value = true
  window.addEventListener('mousemove', handleSplitResize)
  window.addEventListener('mouseup', finishSplitResize)
}

function handleSplitResize(event: MouseEvent) {
  const state = splitResizeState
  if (!state || state.bodyWidth <= 0) return
  layout.conversationRatio = clamp(
    state.startRatio + (event.clientX - state.startX) / state.bodyWidth,
    0.36,
    0.56,
  )
}

function finishSplitResize() {
  splitResizeState = null
  isResizing.value = false
  removeResizeListeners()
  persistLayout()
}

function handleSplitKeydown(event: KeyboardEvent) {
  if (!showArtifactSplitter.value) return
  if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return
  event.preventDefault()
  layout.conversationRatio = clamp(
    layout.conversationRatio + (event.key === 'ArrowLeft' ? -0.03 : 0.03),
    0.36,
    0.56,
  )
  persistLayout()
}

function toggleArtifactPane() {
  if (!artifactAvailable.value) return
  if (activeView.value !== 'create') {
    activeView.value = 'create'
    ensureArtifactWorkspace()
    return
  }
  layout.artifactCollapsed = !layout.artifactCollapsed
  persistLayout()
}

function toggleDrawer() {
  if (isOpen.value) {
    closeDrawer()
    return
  }

  if (props.conversationRuntimeEnabled === false) {
    window.dispatchEvent(new CustomEvent('qaly:focus-ai-primary-runtime'))
    return
  }

  isOpen.value = true
}

function closeDrawer() {
  isOpen.value = false

  if (!route.query.aiActivity && !route.query.aiJob && !route.query.aiDraft) return

  const query = { ...route.query }
  delete query.aiActivity
  delete query.aiTab
  delete query.aiJob
  delete query.aiDraft
  void router.replace({ query })
}

function applyAssistantOpenRequest(detail?: Partial<AssistantOpenRequest> | null) {
  if (props.conversationRuntimeEnabled === false && detail?.view !== 'activity') {
    window.dispatchEvent(new CustomEvent('qaly:focus-ai-primary-runtime', {
      detail: {
        prompt: String(detail?.prompt || '').trim(),
        projectId: String(detail?.projectId || props.projectId || '') || null,
      },
    }))
    return
  }

  isOpen.value = true
  activeView.value = detail?.view === 'activity' ? 'activity' : 'chat'
  assistantPrompt.value = String(detail?.prompt || '').trim()
  assistantProjectId.value = String(detail?.projectId || props.projectId || '') || null
  assistantPromptToken.value += 1
}

function openAssistant(event?: Event) {
  const detail = event instanceof CustomEvent ? event.detail : null
  applyAssistantOpenRequest(detail)
}

function openActionComposer(event?: Event) {
  const detail = event instanceof CustomEvent ? event.detail : null
  actionProjectId.value = String(detail?.projectId || props.projectId || '') || null
  actionPrompt.value = String(detail?.message || detail?.prompt || '').trim()
  actionProviderHint.value = String(detail?.providerHint || 'auto')
  actionModelProfile.value = String(detail?.modelProfile || 'balanced')
  if (!actionProjectId.value || !actionPrompt.value) {
    if (props.conversationRuntimeEnabled === false) {
      window.dispatchEvent(new CustomEvent('qaly:focus-ai-primary-runtime', {
        detail: { prompt: actionPrompt.value, projectId: actionProjectId.value },
      }))
      return
    }
    isOpen.value = true
    activeView.value = 'chat'
    return
  }
  actionComposerKey.value += 1
  artifactAvailable.value = true
  isOpen.value = true
  activeView.value = 'create'
  ensureArtifactWorkspace()
}

function handleComposeAction(payload: { message: string; projectId: string; providerHint: string; modelProfile: string }) {
  actionProjectId.value = payload.projectId
  actionPrompt.value = payload.message
  actionProviderHint.value = payload.providerHint
  actionModelProfile.value = payload.modelProfile
  actionComposerKey.value += 1
  artifactAvailable.value = true
  activeView.value = 'create'
  isOpen.value = true
  ensureArtifactWorkspace()
}

function handleCompleted(projectId: string) {
  artifactAvailable.value = true
  emit('completed', projectId)
}

function handleComposerStarted() {
  actionPrompt.value = ''
}

function handleSessionReset() {
  artifactAvailable.value = false
  actionPrompt.value = ''
  if (props.conversationRuntimeEnabled === false) {
    isOpen.value = false
    window.dispatchEvent(new CustomEvent('qaly:focus-ai-primary-runtime'))
  } else {
    activeView.value = 'chat'
  }
}

onMounted(() => {
  syncViewport()
  restoreLayout()
  artifactAvailable.value = Boolean(window.localStorage.getItem('qaly-ai-action-composer-session-v1'))
  window.addEventListener('qaly:open-ai-assistant', openAssistant)
  window.addEventListener('qaly:open-ai-action', openActionComposer)
  window.addEventListener('resize', syncViewport)
})

onBeforeUnmount(() => {
  window.removeEventListener('qaly:open-ai-assistant', openAssistant)
  window.removeEventListener('qaly:open-ai-action', openActionComposer)
  window.removeEventListener('resize', syncViewport)
  removeResizeListeners()
})

watch(
  () => props.openRequest?.id,
  () => {
    if (props.openRequest) applyAssistantOpenRequest(props.openRequest)
  },
  { immediate: true, flush: 'post' },
)

watch(
  () => [route.query.aiActivity, route.query.aiJob, route.query.aiDraft],
  ([activity, jobId, draftId]) => {
    if (activity === '1' || jobId || draftId) {
      isOpen.value = true
      activeView.value = 'activity'
    }
  },
  { immediate: true },
)

watch(isOpen, async open => {
  if (open) {
    restoreFocusTarget = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null
    await nextTick()
    drawerRef.value?.focus({ preventScroll: true })
    return
  }

  restoreFocusTarget?.focus({ preventScroll: true })
  restoreFocusTarget = null
})
</script>

<template>
  <div class="global-erumi-chatbot-widget">
    <!-- Floating Bubble Trigger -->
    <button 
      class="erumi-bubble-trigger" 
      :class="{ 'is-active': isOpen }" 
      aria-label="Mở Trợ lý AI"
      @click="toggleDrawer"
    >
      <ChatbotAvatar size="medium" />
      <span class="bubble-sparkle">
        <Sparkles :size="14" />
      </span>
    </button>

    <!-- Side Drawer Overlay -->
    <Transition name="slide-drawer">
      <div
        v-if="isOpen"
        ref="drawerRef"
        class="erumi-side-drawer"
        :class="{
          'is-workspace': activeView === 'create',
          'is-resizing': isResizing,
          'is-compact-artifact': compactArtifactWorkspace,
        }"
        :style="drawerStyle"
        role="dialog"
        aria-modal="false"
        aria-label="Trợ lý AI"
        tabindex="-1"
        @keydown.esc="closeDrawer"
      >
        <div
          class="workspace-resize-handle workspace-resize-handle--left"
          role="separator"
          tabindex="0"
          aria-label="Thay đổi chiều rộng Trợ lý AI"
          aria-orientation="vertical"
          @mousedown="startPointerResize($event, 'left')"
          @keydown="handleResizeKeydown($event, 'left')"
        ></div>
        <div
          class="workspace-resize-handle workspace-resize-handle--top"
          role="separator"
          tabindex="0"
          aria-label="Thay đổi chiều cao Trợ lý AI"
          aria-orientation="horizontal"
          @mousedown="startPointerResize($event, 'top')"
          @keydown="handleResizeKeydown($event, 'top')"
        ></div>
        <div
          class="workspace-resize-handle workspace-resize-handle--corner"
          role="separator"
          tabindex="0"
          aria-label="Thay đổi kích thước Trợ lý AI"
          @mousedown="startPointerResize($event, 'corner')"
          @keydown="handleResizeKeydown($event, 'corner')"
        ></div>
        <header class="drawer-header">
          <div class="drawer-header-left">
            <ChatbotAvatar size="small" />
            <div class="drawer-title-wrap">
              <span class="drawer-title">Trợ lý AI</span>
              <span class="drawer-subtitle">
                {{ activeView === 'chat' ? 'Nói điều bạn cần · AI sẽ hỏi rõ hoặc chuẩn bị bản nháp' : activeView === 'create' ? 'Trao đổi bên trái · bản nháp có cấu trúc bên phải' : 'Jobs, tiến trình và bản nháp cần duyệt' }}
              </span>
            </div>
          </div>
          <div class="drawer-header-actions">
            <button class="drawer-icon-btn" :class="{ active: activeView === 'chat' }" title="Trò chuyện" @click="activeView = 'chat'"><MessageSquare :size="17" /></button>
            <button
              v-if="artifactAvailable"
              class="drawer-icon-btn"
              :class="{ active: activeView === 'create' && !layout.artifactCollapsed }"
              :title="activeView === 'create' && !layout.artifactCollapsed ? 'Thu gọn bản nháp AI' : 'Mở bản nháp AI'"
              :aria-label="activeView === 'create' && !layout.artifactCollapsed ? 'Thu gọn bản nháp AI' : 'Mở bản nháp AI'"
              @click="toggleArtifactPane"
            ><ListChecks :size="17" /></button>
            <button class="drawer-icon-btn" :class="{ active: activeView === 'activity' }" title="Hoạt động AI" @click="activeView = 'activity'"><Activity :size="17" /></button>
            <button class="drawer-icon-btn" title="Lịch sử phiên Trợ lý AI" aria-label="Lịch sử phiên Trợ lý AI" data-testid="assistant-session-history-toolbar" @click="openAssistantHistory"><Clock3 :size="17" /></button>
            <button class="drawer-icon-btn reset-layout-btn" title="Đặt lại kích thước" aria-label="Đặt lại kích thước Trợ lý AI" @click="resetAssistantLayout"><RotateCcw :size="17" /></button>
            <button class="drawer-close-btn" @click="closeDrawer" aria-label="Đóng"><X :size="20" /></button>
          </div>
        </header>
        
        <div
          ref="drawerBodyRef"
          class="drawer-body"
          :class="{
            'is-create': activeView === 'create',
            'is-artifact-collapsed': layout.artifactCollapsed,
            'is-compact-workspace': compactArtifactWorkspace,
          }"
          :style="drawerBodyStyle"
        >
          <section v-if="showConversationPane" class="assistant-conversation-pane">
            <ErumiChatPanel
              :is-drawer="true"
              :external-prompt="assistantPrompt"
              :external-prompt-token="assistantPromptToken"
              :external-history-token="assistantHistoryToken"
              :external-project-id="assistantProjectId"
              @compose-action="handleComposeAction"
            />
          </section>
          <div
            v-if="showArtifactSplitter"
            class="assistant-pane-splitter"
            role="separator"
            tabindex="0"
            aria-label="Thay đổi độ rộng hội thoại và bản nháp"
            aria-orientation="vertical"
            :aria-valuenow="Math.round(layout.conversationRatio * 100)"
            aria-valuemin="36"
            aria-valuemax="56"
            @mousedown="startSplitResize"
            @keydown="handleSplitKeydown"
          ><span aria-hidden="true"></span></div>
          <section v-if="showArtifactPane" class="assistant-artifact-pane">
            <AiActionComposerDrawer
              :key="actionComposerKey"
              embedded
              artifact-only
              :auto-start="Boolean(actionPrompt && actionProjectId)"
              :initial-prompt="actionPrompt"
              :project-id="actionProjectId"
              :projects="projects"
              :provider-hint="actionProviderHint"
              :model-profile="actionModelProfile"
              @close="activeView = 'chat'"
              @completed="handleCompleted"
              @started="handleComposerStarted"
              @session-reset="handleSessionReset"
            />
          </section>
          <AiActivityPanel v-if="activeView === 'activity'" />
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.global-erumi-chatbot-widget {
  position: relative;
  z-index: 9999;
}

/* Floating Bubble Button */
.erumi-bubble-trigger {
  position: fixed;
  bottom: 24px;
  right: 24px;
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  border: none;
  box-shadow: var(--qaly-shadow-md);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.erumi-bubble-trigger:hover {
  transform: translateY(-4px) scale(1.06);
  box-shadow: var(--qaly-shadow-md);
}

.erumi-bubble-trigger.is-active {
  transform: rotate(90deg) scale(0.9);
  background: #64748b;
  box-shadow: var(--qaly-shadow-md);
}

.bubble-sparkle {
  position: absolute;
  top: -2px;
  right: -2px;
  background: #f59e0b;
  color: white;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 6px rgba(245, 158, 11, 0.4);
}

/* Side Drawer Panel */
.erumi-side-drawer {
  position: fixed;
  right: 16px;
  bottom: 16px;
  width: 440px;
  height: min(760px, calc(100vh - 32px));
  max-width: calc(100vw - 32px);
  max-height: calc(100vh - 32px);
  background: var(--panel, #ffffff);
  box-shadow: var(--qaly-shadow-md);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--line, #e2e8f0);
  border-radius: 16px;
  transition: box-shadow .2s ease;
}

.erumi-side-drawer.is-workspace {
  box-shadow: 0 24px 64px rgba(15, 23, 42, .2);
}

.erumi-side-drawer.is-resizing {
  user-select: none;
  transition: none;
}

.workspace-resize-handle {
  position: absolute;
  z-index: 30;
  touch-action: none;
  outline: none;
}

.workspace-resize-handle::after {
  content: '';
  position: absolute;
  border-radius: 999px;
  background: transparent;
  transition: background-color .16s ease;
}

.workspace-resize-handle:hover::after,
.workspace-resize-handle:focus-visible::after,
.erumi-side-drawer.is-resizing .workspace-resize-handle::after {
  background: var(--primary, #2563eb);
}

.workspace-resize-handle--left {
  top: 14px;
  bottom: 14px;
  left: 0;
  width: 10px;
  cursor: ew-resize;
}

.workspace-resize-handle--left::after {
  top: 12px;
  bottom: 12px;
  left: 2px;
  width: 3px;
}

.workspace-resize-handle--top {
  top: 0;
  right: 14px;
  left: 14px;
  height: 10px;
  cursor: ns-resize;
}

.workspace-resize-handle--top::after {
  top: 2px;
  right: 12px;
  left: 12px;
  height: 3px;
}

.workspace-resize-handle--corner {
  top: 0;
  left: 0;
  width: 18px;
  height: 18px;
  cursor: nwse-resize;
}

.workspace-resize-handle--corner::after {
  top: 4px;
  left: 4px;
  width: 7px;
  height: 7px;
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 58px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--line, #f1f5f9);
  background: var(--panel, #ffffff);
}

.drawer-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.drawer-header-left :deep(.chatbot-avatar--small) {
  width: 34px;
  height: 34px;
}

.drawer-title-wrap {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.drawer-title {
  font-weight: 700;
  color: #0f172a;
  font-size: 15px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.drawer-subtitle {
  font-size: 11px;
  color: #64748b;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.drawer-close-btn {
  background: transparent;
  border: none;
  color: #64748b;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  transition: all 0.2s ease;
}

.drawer-close-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

.drawer-header-actions {
  display: flex;
  align-items: center;
  gap: 2px;
  flex: 0 0 auto;
}

.drawer-icon-btn {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 1px solid transparent;
  background: transparent;
  color: #64748b;
  cursor: pointer;
}

.drawer-icon-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

.drawer-icon-btn.active {
  border-color: #b9d8cc;
  background: #eaf6f0;
  color: #24735b;
}

.drawer-body {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.drawer-body.is-create {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
}

.drawer-body.is-create.is-artifact-collapsed,
.drawer-body.is-create.is-compact-workspace {
  grid-template-columns: minmax(0, 1fr) !important;
}

.assistant-conversation-pane,
.assistant-artifact-pane {
  min-width: 0;
  min-height: 0;
  height: 100%;
  overflow: hidden;
}

.assistant-artifact-pane {
  border-left: 1px solid var(--line, #e2e8f0);
}

.assistant-pane-splitter {
  position: relative;
  z-index: 10;
  display: grid;
  width: 8px;
  min-width: 8px;
  height: 100%;
  place-items: center;
  border: 0;
  background: var(--panel-soft, #f8fafc);
  cursor: col-resize;
  touch-action: none;
  outline: none;
}

.assistant-pane-splitter span {
  width: 3px;
  height: 48px;
  border-radius: 999px;
  background: var(--line, #cbd5e1);
  transition: background-color .16s ease, height .16s ease;
}

.assistant-pane-splitter:hover span,
.assistant-pane-splitter:focus-visible span {
  height: 72px;
  background: var(--primary, #2563eb);
}

/* Drawer slide transition */
.slide-drawer-enter-active,
.slide-drawer-leave-active {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-drawer-enter-from,
.slide-drawer-leave-to {
  transform: translateX(100%);
}

@media (max-width: 480px) {
  .erumi-side-drawer {
    right: 0;
    bottom: 0;
    width: 100vw !important;
    height: 100dvh !important;
    max-width: none;
    max-height: none;
    border-radius: 0;
  }

  .drawer-header {
    padding-inline: 10px;
  }

  .drawer-header-left {
    gap: 8px;
  }
}

@media (max-width: 800px) {
  .erumi-side-drawer {
    right: 0;
    bottom: 0;
    width: 100vw !important;
    height: 100dvh !important;
    max-width: none;
    max-height: none;
    border-radius: 0;
  }

  .drawer-body.is-create {
    grid-template-columns: minmax(0, 1fr) !important;
  }

  .workspace-resize-handle,
  .assistant-pane-splitter,
  .reset-layout-btn {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .erumi-side-drawer,
  .workspace-resize-handle::after,
  .assistant-pane-splitter span {
    transition: none;
  }
}
</style>
