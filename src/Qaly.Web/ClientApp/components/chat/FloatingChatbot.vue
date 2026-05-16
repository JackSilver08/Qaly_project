<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'
import { Send, X, MessageSquare, Sparkles } from 'lucide-vue-next'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import { useDashboardContext } from '../../composables/dashboard-context'
import { showError } from '../../composables/use-toast'

const { projects, selectedProject, currentUser } = useDashboardContext()

interface ChatMessage {
  id: string
  role: 'assistant' | 'user'
  text: string
}

const isOpen = ref(false)
const isThinking = ref(false)

const draft = ref('')
const messages = ref<ChatMessage[]>([
  {
    id: 'welcome',
    role: 'assistant',
    text: 'Chào bạn! Mình là Erumi, trợ lý AI của Qaly. Bạn cần mình giúp gì hôm nay?'
  }
])

const bodyRef = ref<HTMLElement | null>(null)
const showSuggestions = ref(false)

const markdown = new (MarkdownIt as any)({
  html: false,
  linkify: true,
  typographer: true
})

function renderMarkdown(content: string) {
  return DOMPurify.sanitize(markdown.render(content))
}

const suggestions = computed(() => {
  const parts = draft.value.split(' ')
  const lastPart = parts[parts.length - 1]
  if (lastPart.startsWith('@')) {
    const query = lastPart.slice(1).toLowerCase()
    return projects.value.filter((p: any) => p.name.toLowerCase().includes(query))
  }
  return []
})

watch(draft, (val) => {
  const parts = val.split(' ')
  const lastPart = parts[parts.length - 1]
  showSuggestions.value = lastPart.startsWith('@')
})

function tagProject(project: any) {
  const parts = draft.value.split(' ')
  parts[parts.length - 1] = `@${project.name} `
  draft.value = parts.join(' ')
  showSuggestions.value = false
}

async function submitChat(explicit?: string) {
  const prompt = (explicit ?? draft.value).trim()
  if (!prompt || isThinking.value) return

  messages.value.push({ id: `u-${Date.now()}`, role: 'user', text: prompt })
  draft.value = ''
  isThinking.value = true
  
  await scrollBottom()

  let taggedProjectId = null
  const tagMatch = prompt.match(/@([\w\s]+)/)
  if (tagMatch) {
    const name = tagMatch[1].trim().toLowerCase()
    const p = projects.value.find((x: any) => x.name.toLowerCase() === name)
    if (p) taggedProjectId = p.id
  }

  try {
    const assistantId = `a-${Date.now()}`
    messages.value.push({ id: assistantId, role: 'assistant', text: '' })

    const response = await fetch('/api/ai/chat/stream', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: prompt,
        projectId: taggedProjectId ?? selectedProject.value?.id ?? null
      })
    })

    if (!response.ok) throw new Error('Streaming failed')

    const reader = response.body?.getReader()
    const decoder = new TextDecoder()
    let fullText = ''

    if (reader) {
      isThinking.value = false
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        fullText += decoder.decode(value, { stream: true })
        const idx = messages.value.findIndex(m => m.id === assistantId)
        if (idx !== -1) messages.value[idx].text = fullText
        void scrollBottom()
      }
    }
  } catch (e) {
    messages.value.push({ id: `err-${Date.now()}`, role: 'assistant', text: 'Xin lỗi, Erumi đang gặp chút trục trặc. Thử lại sau nhé!' })
    showError('Không thể gửi yêu cầu tới trợ lý AI')
  } finally {
    isThinking.value = false
  }
}

async function scrollBottom() {
  await nextTick()
  if (bodyRef.value) {
    bodyRef.value.scrollTo({ top: bodyRef.value.scrollHeight, behavior: 'smooth' })
  }
}

function toggle() {
  isOpen.value = !isOpen.value
  if (isOpen.value) void scrollBottom()
}

function initials(name: string) {
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
}

const quickPrompts = [
  'Tóm tắt dự án hiện tại',
  'Có task nào quá hạn không?',
  'Ai đang rảnh để nhận việc?'
]
</script>

<template>
  <div class="floating-erumi">
    <!-- Floating Launcher -->
    <button 
      class="erumi-launcher shadow-lg" 
      :class="{ 'is-active': isOpen }"
      @click="toggle"
      aria-label="Toggle AI Assistant"
    >
      <ChatbotAvatar v-if="!isOpen" size="medium" />
      <X v-else :size="24" />
      <span v-if="!isOpen" class="launcher-badge"></span>
    </button>

    <!-- Chat Window -->
    <transition name="fade-up">
      <div v-if="isOpen" class="erumi-window glass-card shadow-2xl">
        <header class="erumi-header">
          <div class="erumi-identity">
            <ChatbotAvatar size="small" />
            <div>
              <div class="flex items-center gap-2">
                <h3>Erumi Agent</h3>
              </div>
              <div class="status-indicator">
                <span class="pulse"></span>
                Trực tuyến
              </div>
            </div>
          </div>
          <div class="header-actions">
            <button class="icon-btn" @click="toggle"><X :size="18" /></button>
          </div>
        </header>

        <div ref="bodyRef" class="erumi-body no-scrollbar">
          <div v-for="m in messages" :key="m.id" :class="['msg-row', `msg-${m.role}`]">
            <div v-if="m.role === 'assistant'" class="msg-avatar">
              <ChatbotAvatar size="small" />
            </div>
            <div class="msg-bubble" v-html="renderMarkdown(m.text)"></div>
            <div v-if="m.role === 'user'" class="msg-avatar user-icon">
              {{ currentUser ? initials(currentUser.fullName) : 'U' }}
            </div>
          </div>

          <div v-if="isThinking" class="msg-row msg-assistant">
            <div class="msg-avatar"><ChatbotAvatar size="small" /></div>
            <div class="msg-bubble thinking-dots">
              <span></span><span></span><span></span>
            </div>
          </div>
        </div>

        <div class="erumi-footer">
          <div class="quick-prompts no-scrollbar">
            <button 
              v-for="p in quickPrompts" 
              :key="p" 
              class="prompt-btn"
              @click="submitChat(p)"
            >
              <Sparkles :size="12" />
              {{ p }}
            </button>
          </div>

          <div v-if="showSuggestions && suggestions.length" class="mention-suggestions">
            <button v-for="s in suggestions" :key="s.id" @click="tagProject(s)">
              @{{ s.name }}
            </button>
          </div>

          <form class="composer" @submit.prevent="submitChat()">
            <input 
              v-model="draft" 
              placeholder="Hỏi Erumi... (Dùng @ để tag dự án)"
              :disabled="isThinking"
              ref="inputRef"
            />
            <button type="submit" :disabled="!draft.trim() || isThinking" class="send-btn">
              <Send :size="18" />
            </button>
          </form>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.floating-erumi {
  position: fixed;
  bottom: 24px;
  right: 24px;
  z-index: 1000;
  font-family: 'Inter', sans-serif;
}

.erumi-launcher {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: var(--primary);
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  position: relative;
}

.erumi-launcher:hover {
  transform: scale(1.1) rotate(5deg);
  box-shadow: 0 10px 25px rgba(31, 128, 255, 0.4);
}

.erumi-launcher.is-active {
  background: #f1f5f9;
  color: var(--text);
  transform: rotate(90deg);
}

.launcher-badge {
  position: absolute;
  top: 0;
  right: 0;
  width: 14px;
  height: 14px;
  background: #10b981;
  border: 2px solid white;
  border-radius: 50%;
}

.erumi-window {
  position: absolute;
  bottom: 80px;
  right: 0;
  width: 380px;
  height: 560px;
  max-height: calc(100vh - 120px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--glass-border);
  background: var(--glass-strong);
}

.erumi-header {
  padding: 16px;
  background: rgba(255, 255, 255, 0.6);
  border-bottom: 1px solid var(--line);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.erumi-identity {
  display: flex;
  align-items: center;
  gap: 12px;
}

.erumi-identity h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.status-indicator {
  font-size: 11px;
  color: #10b981;
  display: flex;
  align-items: center;
  gap: 4px;
  font-weight: 600;
}



.pulse {
  width: 6px;
  height: 6px;
  background: #10b981;
  border-radius: 50%;
  display: inline-block;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
  70% { box-shadow: 0 0 0 6px rgba(16, 185, 129, 0); }
  100% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
}

.erumi-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.msg-row {
  display: flex;
  gap: 10px;
  max-width: 85%;
}

.msg-assistant {
  align-self: flex-start;
}

.msg-user {
  align-self: flex-end;
  flex-direction: row-reverse;
  max-width: 80%;
}

.msg-avatar {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  flex-shrink: 0;
}

.user-icon {
  background: var(--primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 800;
  border-radius: 50%;
}

.msg-bubble {
  padding: 10px 14px;
  border-radius: 14px;
  font-size: 13px;
  line-height: 1.5;
  box-shadow: 0 2px 5px rgba(0,0,0,0.05);
}

.msg-assistant .msg-bubble {
  background: white;
  color: var(--text);
  border-top-left-radius: 2px;
}

.msg-user .msg-bubble {
  background: var(--primary);
  color: white;
  border-top-right-radius: 2px;
}

.msg-bubble :deep(p) { margin: 0 0 8px 0; }
.msg-bubble :deep(p:last-child) { margin-bottom: 0; }
.msg-bubble :deep(ul), .msg-bubble :deep(ol) { margin: 8px 0; padding-left: 20px; }

.erumi-footer {
  padding: 12px;
  background: rgba(255, 255, 255, 0.4);
  border-top: 1px solid var(--line);
}

.quick-prompts {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding-bottom: 12px;
  white-space: nowrap;
}

.prompt-btn {
  padding: 6px 12px;
  background: white;
  border: 1px solid var(--line);
  border-radius: 20px;
  font-size: 11px;
  font-weight: 600;
  color: var(--primary);
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s;
}

.prompt-btn:hover {
  background: var(--primary-soft);
  border-color: var(--primary);
}

.composer {
  display: flex;
  gap: 8px;
  background: white;
  padding: 4px 4px 4px 12px;
  border-radius: 24px;
  border: 1px solid var(--line);
  box-shadow: 0 2px 10px rgba(0,0,0,0.03);
}

.composer input {
  flex: 1;
  border: none;
  outline: none;
  font-size: 13px;
}

.send-btn {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: opacity 0.2s;
}

.send-btn:disabled {
  opacity: 0.5;
}

.mention-suggestions {
  position: absolute;
  bottom: 60px;
  left: 12px;
  right: 12px;
  background: white;
  border-radius: 12px;
  border: 1px solid var(--line);
  box-shadow: 0 -5px 15px rgba(0,0,0,0.05);
  display: flex;
  flex-direction: column;
  z-index: 10;
}

.mention-suggestions button {
  padding: 8px 12px;
  text-align: left;
  border: none;
  background: none;
  font-size: 12px;
  border-bottom: 1px solid #f1f5f9;
}

.mention-suggestions button:hover {
  background: #f8fafc;
}

.fade-up-enter-active, .fade-up-leave-active {
  transition: all 0.3s ease;
}
.fade-up-enter-from, .fade-up-leave-to {
  opacity: 0;
  transform: translateY(20px);
}

.thinking-dots {
  display: flex;
  gap: 4px;
  padding: 12px 16px;
}

.thinking-dots span {
  width: 6px;
  height: 6px;
  background: var(--muted);
  border-radius: 50%;
  animation: dots 1.4s infinite;
}

.thinking-dots span:nth-child(2) { animation-delay: 0.2s; }
.thinking-dots span:nth-child(3) { animation-delay: 0.4s; }

@keyframes dots {
  0%, 80%, 100% { transform: translateY(0); }
  40% { transform: translateY(-5px); }
}
</style>
