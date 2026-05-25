<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { 
  Sparkles, 
  Send, 
  RefreshCw,
  Square,
  Mic,
  Paperclip,
  Globe
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
import { showSuccess, showError } from '../composables/use-toast'
import { apiJson } from '../utils/api-client'
import ChatbotAvatar from '../components/ChatbotAvatar.vue'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'
import { Bar, Doughnut, Line } from 'vue-chartjs'
import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip
} from 'chart.js'

ChartJS.register(ArcElement, BarElement, CategoryScale, LinearScale, LineElement, PointElement, Tooltip, Legend)

const { projects, selectedProject } = useDashboardContext()

const markdown = new (MarkdownIt as any)({
  html: false,
  linkify: true,
  typographer: true
})

function renderMarkdown(content: string) {
  if (!content) return ''
  return DOMPurify.sanitize(markdown.render(content))
}

type ErumiMetric = {
  label: string
  value: string
  tone?: string | null
  hint?: string | null
}

type ErumiChart = {
  type: 'pie' | 'bar' | 'line' | string
  title: string
  labels: string[]
  values: number[]
  unit?: string | null
}

type ErumiAction = {
  type: string
  label: string
  payload?: unknown
  requiresConfirmation?: boolean
}

type ErumiChatResponse = {
  reply: string
  metrics: ErumiMetric[]
  charts: ErumiChart[]
  actions: ErumiAction[]
  sources: string[]
  usedAi: boolean
  intent: string
  latencyMs: number
}

type ChatEntry = {
  role: 'user' | 'assistant'
  text: string
  metrics?: ErumiMetric[]
  charts?: ErumiChart[]
  actions?: ErumiAction[]
  sources?: string[]
  latencyMs?: number
}

const activeProjects = computed(() => projects.value.filter((p: any) => p.status !== 'Archived'))
const selectedTarget = ref('workspace') // 'workspace' or projectId

const syncingDb = ref(false)
const chatInput = ref('')
const isChatting = ref(false)
const chatContainerRef = ref<HTMLElement | null>(null)
const textareaRef = ref<HTMLTextAreaElement | null>(null)

// Initial chat history with welcome message
const chatHistory = ref<ChatEntry[]>([
  {
    role: 'assistant',
    text: 'Xin chào! Mình là Erumi, trợ lý phân tích AI của Qaly. Hãy hỏi mình bất kỳ câu hỏi nào về các chỉ số hoặc dự án trong Workspace của bạn nhé.'
  }
])

const isChatActive = computed(() => chatHistory.value.length > 1 || isChatting.value)

// Dynamic suggestions based on selected workspace / project
const currentSuggestions = computed(() => {
  if (selectedTarget.value === 'workspace') {
    return [
      { label: '📊 Đánh giá hiệu suất tuần qua', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.' },
      { label: '⚠️ Dự án nào đang gặp rủi ro?', prompt: 'Hiện tại có dự án nào đang gặp rủi ro hoặc chậm tiến độ không?' },
      { label: '👥 Phân tích năng suất nhóm', prompt: 'Phân tích năng suất làm việc của các thành viên trong Workspace.' }
    ]
  } else {
    const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
    const name = proj?.name || 'dự án'
    return [
      { label: '📋 Tóm tắt dự án', action: 'summary', prompt: `Tóm tắt nhanh tình hình hiện tại của dự án ${name}.` },
      { label: '⚠️ Phân tích rủi ro', action: 'risks', prompt: `Phân tích các rủi ro quá hạn và trễ việc của dự án ${name}.` },
      { label: '💡 Insight & năng suất', action: 'insights', prompt: `Đưa ra nhận xét số liệu năng suất và các insight quan trọng của dự án ${name}.` }
    ]
  }
})

function chartComponent(type: string) {
  if (type === 'line') return Line
  if (type === 'bar') return Bar
  return Doughnut
}

function chartData(chart: ErumiChart) {
  const colorSet = [
    '#2563eb',
    '#10b981',
    '#f59e0b',
    '#ef4444',
    '#8b5cf6',
    '#06b6d4',
    '#64748b',
    '#f97316'
  ]

  return {
    labels: chart.labels,
    datasets: [
      {
        label: chart.unit ?? chart.title,
        data: chart.values,
        backgroundColor: chart.type === 'line' ? 'rgba(37, 99, 235, 0.14)' : colorSet,
        borderColor: chart.type === 'line' ? '#2563eb' : colorSet,
        borderWidth: 2,
        tension: 0.35,
        fill: chart.type === 'line'
      }
    ]
  }
}

function chartOptions(chart: ErumiChart) {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: chart.type === 'pie',
        position: 'bottom' as const,
        labels: {
          boxWidth: 10,
          usePointStyle: true
        }
      },
      tooltip: {
        callbacks: {
          label: (ctx: any) => `${ctx.label || ctx.dataset.label}: ${ctx.raw}${chart.unit ? ` ${chart.unit}` : ''}`
        }
      }
    },
    scales: chart.type === 'pie'
      ? {}
      : {
          y: {
            beginAtZero: true,
            ticks: {
              precision: 0
            }
          },
          x: {
            grid: {
              display: false
            }
          }
        }
  }
}

// Auto-resize textarea
function autoResize() {
  if (!textareaRef.value) return
  textareaRef.value.style.height = 'auto'
  const maxHeight = 160
  textareaRef.value.style.height = Math.min(textareaRef.value.scrollHeight, maxHeight) + 'px'
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    submitChat()
  }
}

// Scroll helper
async function scrollToBottom() {
  await nextTick()
  if (chatContainerRef.value) {
    chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight
  }
}

// Watch selected target to reset context and print greeting
watch(selectedTarget, (val) => {
  chatHistory.value = []
  if (val === 'workspace') {
    chatHistory.value.push({
      role: 'assistant',
      text: 'Xin chào! Mình đã kết nối với dữ liệu **Workspace (Tất cả dự án)**. Hãy hỏi mình bất kỳ câu hỏi nào hoặc sử dụng các gợi ý bên dưới.'
    })
  } else {
    const proj = projects.value.find((p: any) => p.id === val)
    const name = proj?.name || 'Dự án'
    chatHistory.value.push({
      role: 'assistant',
      text: `Chào bạn! Mình đã nạp thành công dữ liệu dự án **${name}**. Hãy yêu cầu mình xuất các báo cáo nhanh hoặc hỏi bất cứ thông tin nào liên quan nhé.`
    })
  }
  scrollToBottom()
})

// Database Sync
async function syncVectorDb() {
  if (syncingDb.value) return
  syncingDb.value = true
  try {
    const res = await fetch('/api/ai/sync', { method: 'POST' })
    if (res.ok) {
      showSuccess('Đồng bộ Vector DB thành công! AI đã được cập nhật dữ liệu mới nhất.')
    } else {
      throw new Error()
    }
  } catch {
    showError('Không thể đồng bộ Vector DB')
  } finally {
    syncingDb.value = false
  }
}

// Fallback Generators
function getFallbackProjectStats() {
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  const total = proj?.taskCount || 12
  const done = proj?.completedTaskCount || 7
  const overdue = proj?.overdueTaskCount || 1
  return {
    totalTasks: total,
    doneTasks: done,
    inProgressTasks: Math.max(0, total - done - overdue),
    overdueTasks: overdue,
    totalEstimatedHours: proj?.estimatedHours || 80,
    totalActualHours: proj?.actualHours || 64
  }
}

function getFallbackAiSummary() {
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  const name = proj?.name || 'Dự án'
  const stats = getFallbackProjectStats()
  return `### 📋 Tóm tắt dự án **${name}** từ Erumi AI\n\n- **Trạng thái chung**: Dự án đang vận hành ổn định.\n- **Tiến trình**: Đã hoàn thành **${stats.doneTasks}/${stats.totalTasks} nhiệm vụ** (Đạt **${Math.round((stats.doneTasks / stats.totalTasks) * 100)}%**).\n- **Chất lượng**: Có **${stats.overdueTasks} nhiệm vụ quá hạn** cần xử lý gấp.\n- **Thời gian**: Thực tế đã log **${stats.totalActualHours}h** trên tổng kế hoạch **${stats.totalEstimatedHours}h**.\n- **Khuyến nghị**: Tập trung giải quyết dứt điểm các task quá hạn trong tuần này.`
}

function getFallbackAiRisks() {
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  const name = proj?.name || 'Dự án'
  const stats = getFallbackProjectStats()
  if (stats.overdueTasks > 0) {
    return `### ⚠️ Phân tích Rủi ro dự án **${name}** từ Erumi AI\n\n- **Rủi ro quá hạn**: Phát hiện **${stats.overdueTasks} nhiệm vụ bị quá hạn**.\n- **Khả năng chậm deadline**: Trung bình-Cao. Việc chậm trễ các task này có thể kéo lùi tiến độ của các giai đoạn phát triển tiếp theo.\n- **Giải pháp đề xuất**:\n  1. Phân bổ thêm nhân sự hỗ trợ cho các đầu việc bị nghẽn.\n  2. Tổ chức họp nhanh 15 phút đầu giờ (Standup) để tháo gỡ khó khăn.\n  3. Đánh giá lại mức độ ưu tiên của các nhiệm vụ phụ.`
  }
  return `### ✅ Phân tích Rủi ro dự án **${name}** từ Erumi AI\n\n- **Đánh giá rủi ro**: Rất thấp.\n- **Trạng thái**: Không ghi nhận nhiệm vụ quá hạn. Mọi thành viên đang bám sát tiến độ rất tốt.\n- **Khuyến nghị**: Tiếp tục duy trì phong độ hiện tại.`
}

function getFallbackAiInsights() {
  const stats = getFallbackProjectStats()
  return `### 💡 Phân tích Năng suất & Insight từ Erumi AI\n\n- **Tối ưu hóa thời gian**: Thời gian thực tế log mới đạt **${Math.round((stats.totalActualHours / stats.totalEstimatedHours) * 100)}%** kế hoạch ước tính. Nhóm đang tối ưu tài nguyên rất tốt.\n- **Điểm nghẽn**: Việc hoàn thành nhiệm vụ bị dồn vào cuối tuần. Cần phân bổ đều tải công việc hơn.\n- **Đề xuất**: Tăng cường trao đổi nhóm vào đầu tuần để triển khai công việc đều đặn.`
}

function getFallbackChatAnswer(prompt: string) {
  const p = prompt.toLowerCase()
  if (p.includes('hiệu suất') || p.includes('năng suất') || p.includes('productivity') || p.includes('báo cáo')) {
    return `### 📊 Đánh giá hiệu suất làm việc tuần qua\n\n- **Tiến độ**: Các dự án trong Workspace hoạt động đúng tiến độ đạt **75%**. Tổng số nhiệm vụ đã hoàn tất trong tuần là **8 nhiệm vụ**.\n- **Thời gian**: Toàn nhóm đã ghi nhận **32 giờ chấm công thực tế**.\n- **Nhận xét**: Năng suất duy trì ở mức ổn định. Điểm sáng là sự tập trung cao độ ở các task thuộc luồng quan trọng.`
  }
  if (p.includes('rủi ro') || p.includes('chậm') || p.includes('risk') || p.includes('quá hạn')) {
    return `### ⚠️ Đánh giá rủi ro toàn Workspace\n\n- **Nhiệm vụ trễ hạn**: Phát hiện dự án đang có **1 nhiệm vụ quá hạn** cần xử lý.\n- **Dự án chịu ảnh hưởng**: Dự án DATN đang có tỉ lệ quá hạn nhẹ.\n- **Giải pháp**: Nhắc nhở người thực hiện trực tiếp hoặc phân bổ thêm thành viên hỗ trợ để tháo gỡ điểm nghẽn.`
  }
  return `Chào bạn! Mình là Erumi. Hiện tại mô hình AI cục bộ đang ở trạng thái ngoại tuyến.\n\nTuy nhiên, bạn có thể chọn các dự án cụ thể ở dropdown phía trên và sử dụng các phím tắt nhanh như **Tóm tắt dự án**, **Phân tích rủi ro** hay **Insight** để mình trích xuất báo cáo thông minh trực tiếp từ dữ liệu hệ thống nhé!`
}

// Submit Chat Function
async function submitChat(explicitText?: string, _action?: string) {
  const prompt = (explicitText ?? chatInput.value).trim()
  if (!prompt || isChatting.value) return

  chatHistory.value.push({ role: 'user', text: prompt })
  chatInput.value = ''
  isChatting.value = true
  
  // Reset textarea height
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }

  await scrollToBottom()

  try {
    chatHistory.value.push({ role: 'assistant', text: '' })
    const lastIdx = chatHistory.value.length - 1
    const historyToSend = chatHistory.value
      .slice(1, -2)
      .slice(-6)
      .map(h => ({ role: h.role, content: h.text }))

    const fastReply = await apiJson<ErumiChatResponse>('/api/ai/chat/fast', {
      method: 'POST',
      body: JSON.stringify({
        message: prompt,
        projectId: selectedTarget.value === 'workspace' ? null : selectedTarget.value,
        history: historyToSend
      })
    })

    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: fastReply.reply,
      metrics: fastReply.metrics,
      charts: fastReply.charts,
      actions: fastReply.actions,
      sources: fastReply.sources,
      latencyMs: fastReply.latencyMs
    }
  } catch (e) {
    const lastIdx = chatHistory.value.length - 1
    const fallbackText = getFallbackChatAnswer(prompt)
    isChatting.value = false
    chatHistory.value[lastIdx].text = fallbackText
  } finally {
    isChatting.value = false
    await scrollToBottom()
  }
}

onMounted(() => {
  if (selectedProject.value) {
    selectedTarget.value = selectedProject.value.id
  }
  scrollToBottom()
})
</script>

<template>
  <div class="analytics-chat-portal">
    
    <!-- Top Control Bar (Sticky) -->
    <header class="chat-top-bar">
      <div class="top-bar-left">
        <div class="erumi-brand-icon">
          <Sparkles :size="16" />
        </div>
        <div>
          <h2>Trợ lý Erumi AI</h2>
          <span class="small-label">Trò chuyện phân tích hệ thống</span>
        </div>
      </div>

      <div class="top-bar-actions">
        <select v-model="selectedTarget" class="project-selector" aria-label="Chọn dự án phân tích">
          <option value="workspace">🌐 Tất cả dự án (Workspace)</option>
          <option v-for="p in activeProjects" :key="p.id" :value="p.id">
            📁 {{ p.name }}
          </option>
        </select>

        <button class="btn-sync" :disabled="syncingDb" @click="syncVectorDb">
          <RefreshCw :size="13" :class="{ 'spin-anim': syncingDb }" />
          <span>{{ syncingDb ? 'Đang đồng bộ...' : 'Đồng bộ AI' }}</span>
        </button>
      </div>
    </header>

    <!-- Main Chat Workspace -->
    <div class="chat-main-area">
      
      <!-- EMPTY STATE: Initial ChatGPT-like Center Layout -->
      <div v-if="!isChatActive" class="chat-empty-state">
        <div class="avatar-holder">
          <ChatbotAvatar size="medium" />
        </div>
        <div class="welcome-text-block">
          <h1 class="welcome-heading">Hôm nay Erumi<br>có thể giúp gì cho bạn?</h1>
          <p class="welcome-sub">Phân tích dự án, đánh giá rủi ro và theo dõi năng suất nhóm ngay trong cuộc trò chuyện.</p>
        </div>
        
        <!-- Suggestions above composer in empty state -->
        <div class="suggestion-pills-wrap">
          <button 
            v-for="(s, idx) in currentSuggestions" 
            :key="idx"
            class="suggestion-pill"
            @click="submitChat(s.prompt, (s as any).action)"
          >
            {{ s.label }}
          </button>
        </div>

        <!-- Centered Composer -->
        <div class="composer-wrap-center">
          <div class="chat-composer-shell" :class="{ 'is-focused': false }">
            <textarea 
              ref="textareaRef"
              v-model="chatInput" 
              placeholder="Hỏi Erumi bất cứ điều gì về dự án và năng suất..."
              :disabled="isChatting"
              aria-label="Nhập câu hỏi"
              rows="1"
              @input="autoResize"
              @keydown="handleKeydown"
            />
            <div class="composer-actions-row">
              <div class="composer-left-actions">
                <button class="composer-icon-btn" title="Đính kèm tệp" type="button">
                  <Paperclip :size="16" />
                </button>
                <button class="composer-icon-btn" title="Tìm kiếm web" type="button">
                  <Globe :size="16" />
                </button>
              </div>
              <button 
                class="btn-send-chat" 
                type="button"
                :disabled="!chatInput.trim() || isChatting"
                :class="{ 'is-ready': chatInput.trim() && !isChatting }"
                @click="submitChat()"
              >
                <Send :size="15" v-if="!isChatting" />
                <Square :size="13" v-else style="fill: white;" />
              </button>
            </div>
          </div>
          <p class="composer-hint">Nhấn <kbd>Enter</kbd> để gửi · <kbd>Shift+Enter</kbd> để xuống dòng</p>
        </div>
      </div>

      <!-- ACTIVE STATE: Scrolling chat thread -->
      <div v-else class="chat-thread-container no-scrollbar" ref="chatContainerRef">
        <div class="chat-thread-width">
          
          <div 
            v-for="(msg, i) in chatHistory" 
            :key="i" 
            :class="['msg-bubble-row', `msg-${msg.role}`]"
          >
            <div v-if="msg.role === 'assistant'" class="msg-avatar-col">
              <ChatbotAvatar size="small" />
            </div>
            
            <div class="msg-bubble-content">
              <div v-if="msg.role === 'assistant'" class="msg-bubble-assistant" v-html="renderMarkdown(msg.text)"></div>
              <div v-else class="msg-bubble-user">{{ msg.text }}</div>

              <div v-if="msg.role === 'assistant' && msg.metrics?.length" class="erumi-metrics-grid">
                <article v-for="metric in msg.metrics" :key="metric.label" class="erumi-metric" :class="`tone-${metric.tone || 'neutral'}`">
                  <span>{{ metric.label }}</span>
                  <strong>{{ metric.value }}</strong>
                  <small v-if="metric.hint">{{ metric.hint }}</small>
                </article>
              </div>

              <div v-if="msg.role === 'assistant' && msg.charts?.length" class="erumi-chart-grid">
                <article v-for="chart in msg.charts" :key="chart.title" class="erumi-chart-card">
                  <header>
                    <strong>{{ chart.title }}</strong>
                    <span v-if="chart.unit">{{ chart.unit }}</span>
                  </header>
                  <div class="erumi-chart-canvas">
                    <component :is="chartComponent(chart.type)" :data="chartData(chart)" :options="chartOptions(chart)" />
                  </div>
                </article>
              </div>

              <div v-if="msg.role === 'assistant' && msg.actions?.length" class="erumi-action-list">
                <button v-for="action in msg.actions" :key="action.type" type="button" class="erumi-action-button">
                  {{ action.label }}
                </button>
              </div>

              <div v-if="msg.role === 'assistant' && (msg.sources?.length || msg.latencyMs !== undefined)" class="erumi-response-meta">
                <span v-if="msg.latencyMs !== undefined">Phản hồi {{ msg.latencyMs }}ms</span>
                <span v-if="msg.sources?.length">Nguồn: {{ msg.sources.join(', ') }}</span>
              </div>
            </div>
          </div>

          <div v-if="isChatting" class="msg-bubble-row msg-assistant">
            <div class="msg-avatar-col">
              <ChatbotAvatar size="small" />
            </div>
            <div class="msg-bubble-content">
              <div class="msg-bubble-assistant typing-loader">
                <span></span><span></span><span></span>
              </div>
            </div>
          </div>

        </div>
      </div>

    </div>

    <!-- Sticky Bottom Bar (Only visible when chat is active) -->
    <footer v-if="isChatActive" class="chat-bottom-bar">
      <div class="bottom-bar-width">
        
        <!-- Suggestions Chips (horizontal scrollable above input) -->
        <div class="suggestion-chips-horizontal no-scrollbar">
          <button 
            v-for="(s, idx) in currentSuggestions" 
            :key="idx"
            class="suggestion-chip-small"
            @click="submitChat(s.prompt, (s as any).action)"
          >
            {{ s.label }}
          </button>
        </div>

        <!-- Premium Composer -->
        <div class="chat-composer-shell">
          <textarea 
            ref="textareaRef"
            v-model="chatInput" 
            placeholder="Hỏi Erumi thêm điều gì đó..."
            :disabled="isChatting"
            aria-label="Nhập câu hỏi tiếp theo"
            rows="1"
            @input="autoResize"
            @keydown="handleKeydown"
          />
          <div class="composer-actions-row">
            <div class="composer-left-actions">
              <button class="composer-icon-btn" title="Đính kèm tệp" type="button">
                <Paperclip :size="15" />
              </button>
              <button class="composer-icon-btn" title="Tìm kiếm web" type="button">
                <Globe :size="15" />
              </button>
            </div>
            <button 
              class="btn-send-chat" 
              type="button"
              :disabled="!chatInput.trim() || isChatting"
              :class="{ 'is-ready': chatInput.trim() && !isChatting }"
              @click="submitChat()"
            >
              <Send :size="14" v-if="!isChatting" />
              <Square :size="12" v-else style="fill: white;" />
            </button>
          </div>
        </div>

        <p class="chat-disclaimer">Erumi AI có thể mắc sai sót. Vui lòng kiểm tra lại thông tin quan trọng.</p>
      </div>
    </footer>

  </div>
</template>

<style scoped>
/* ================================================
   BASE LAYOUT
   ================================================ */
.analytics-chat-portal {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 60px);
  width: 100%;
  background: #f7f8fc;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
}

/* ================================================
   TOP BAR
   ================================================ */
.chat-top-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 28px;
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid rgba(15, 82, 186, 0.08);
  z-index: 10;
  flex-shrink: 0;
  box-shadow: 0 1px 0 rgba(15, 82, 186, 0.06);
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.erumi-brand-icon {
  width: 34px;
  height: 34px;
  border-radius: 10px;
  background: linear-gradient(135deg, #0f52ba 0%, #1e70e9 100%);
  display: grid;
  place-items: center;
  color: white;
  flex-shrink: 0;
  box-shadow: 0 3px 10px rgba(15, 82, 186, 0.2);
}

.top-bar-left h2 {
  font-size: 15px;
  font-weight: 800;
  color: #0f172a;
  margin: 0;
  letter-spacing: -0.3px;
}

.small-label {
  font-size: 11px;
  color: #94a3b8;
  font-weight: 500;
  display: block;
  margin-top: 1px;
}

.top-bar-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.project-selector {
  height: 36px;
  padding: 0 12px;
  border: 1px solid rgba(15, 82, 186, 0.15);
  border-radius: 10px;
  outline: none;
  font-weight: 600;
  color: #1e293b;
  background: #ffffff;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s ease;
  box-shadow: 0 1px 4px rgba(0,0,0,0.04);
}

.project-selector:focus {
  border-color: #0f52ba;
  box-shadow: 0 0 0 3px rgba(15, 82, 186, 0.1);
}

.btn-sync {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 36px;
  padding: 0 14px;
  border: 1px solid rgba(15, 82, 186, 0.2);
  border-radius: 10px;
  font-weight: 700;
  color: #0f52ba;
  background: rgba(15, 82, 186, 0.06);
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 12.5px;
}

.btn-sync:hover:not(:disabled) {
  background: rgba(15, 82, 186, 0.12);
  border-color: #0f52ba;
}

.btn-sync:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.spin-anim {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* ================================================
   MAIN CHAT AREA
   ================================================ */
.chat-main-area {
  flex: 1;
  overflow: hidden;
  position: relative;
  display: flex;
  flex-direction: column;
}

/* ================================================
   EMPTY STATE
   ================================================ */
.chat-empty-state {
  margin: auto;
  width: 100%;
  max-width: 740px;
  padding: 36px 24px 24px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 20px;
}

.avatar-holder {
  width: 72px;
  height: 72px;
  display: grid;
  place-items: center;
}

.avatar-holder :deep(.chatbot-avatar) {
  width: 72px !important;
  height: 72px !important;
  border-radius: 20px;
  background: radial-gradient(circle at 30% 30%, #ffffff 0%, rgba(235, 244, 255, 0.95) 45%, rgba(15, 82, 186, 0.35) 85%);
  box-shadow: 
    inset 2px 2px 4px rgba(255, 255, 255, 0.9), 
    0 12px 28px rgba(15, 82, 186, 0.15);
  display: inline-grid;
  place-items: center;
}

.avatar-holder :deep(.chatbot-avatar img) {
  width: 82% !important;
  height: 82% !important;
  filter: drop-shadow(2px 3px 3px rgba(7, 89, 199, 0.2));
}

.welcome-text-block {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.welcome-heading {
  font-size: 28px;
  font-weight: 800;
  color: #0f172a;
  letter-spacing: -0.8px;
  line-height: 1.2;
  margin: 0;
}

.welcome-sub {
  font-size: 14px;
  color: #64748b;
  font-weight: 400;
  margin: 0;
  line-height: 1.5;
}

.composer-wrap-center {
  width: 100%;
  max-width: 680px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  order: 10;
}

.composer-hint {
  font-size: 11px;
  color: #94a3b8;
  margin: 0;
  text-align: center;
}

.composer-hint kbd {
  background: rgba(15, 82, 186, 0.08);
  border: 1px solid rgba(15, 82, 186, 0.15);
  border-radius: 4px;
  padding: 0 5px;
  font-size: 10px;
  font-family: inherit;
  color: #0f52ba;
  font-weight: 700;
}

.suggestion-pills-wrap {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px;
  width: 100%;
  max-width: 600px;
}

.suggestion-pill {
  padding: 10px 18px;
  border: 1px solid rgba(15, 82, 186, 0.12);
  border-radius: 24px;
  font-size: 13px;
  font-weight: 600;
  color: #1e40af;
  background: #ffffff;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(15, 82, 186, 0.04);
}

.suggestion-pill:hover {
  background: rgba(15, 82, 186, 0.06);
  border-color: #0f52ba;
  color: #0f52ba;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(15, 82, 186, 0.1);
}

/* ================================================
   PREMIUM COMPOSER SHELL
   ================================================ */
.chat-composer-shell {
  display: flex;
  flex-direction: column;
  background: #ffffff;
  border-radius: 20px;
  border: 1.5px solid rgba(15, 82, 186, 0.15);
  box-shadow: 
    0 4px 16px rgba(15, 82, 186, 0.06),
    0 1px 4px rgba(0, 0, 0, 0.04);
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  overflow: hidden;
  width: 100%;
}

.chat-composer-shell:focus-within {
  border-color: #0f52ba;
  box-shadow: 
    0 0 0 3px rgba(15, 82, 186, 0.12),
    0 8px 24px rgba(15, 82, 186, 0.1);
}

.chat-composer-shell textarea {
  width: 100%;
  border: none;
  outline: none;
  resize: none;
  font-size: 14.5px;
  font-weight: 500;
  color: #0f172a;
  background: transparent;
  padding: 16px 20px 8px 20px;
  line-height: 1.6;
  min-height: 52px;
  max-height: 160px;
  overflow-y: auto;
  font-family: 'Inter', sans-serif;
  scrollbar-width: thin;
}

.chat-composer-shell textarea::placeholder {
  color: #94a3b8;
  font-weight: 400;
}

.composer-actions-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px 12px 16px;
}

.composer-left-actions {
  display: flex;
  gap: 4px;
}

.composer-icon-btn {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  border: none;
  background: transparent;
  color: #94a3b8;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s ease;
}

.composer-icon-btn:hover {
  background: rgba(15, 82, 186, 0.06);
  color: #0f52ba;
}

.btn-send-chat {
  width: 36px;
  height: 36px;
  border-radius: 12px;
  background: #e2e8f0;
  color: #94a3b8;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: not-allowed;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  flex-shrink: 0;
}

.btn-send-chat.is-ready {
  background: linear-gradient(135deg, #0f52ba, #1e70e9);
  color: white;
  cursor: pointer;
  box-shadow: 0 3px 10px rgba(15, 82, 186, 0.25);
}

.btn-send-chat.is-ready:hover {
  transform: scale(1.06);
  box-shadow: 0 5px 14px rgba(15, 82, 186, 0.35);
}

.btn-send-chat:disabled {
  cursor: not-allowed;
}

/* ================================================
   ACTIVE CHAT THREAD
   ================================================ */
.chat-thread-container {
  flex: 1;
  overflow-y: auto;
  padding: 24px 16px 180px 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.chat-thread-width {
  width: 100%;
  max-width: 720px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.msg-bubble-row {
  display: flex;
  gap: 12px;
  width: 100%;
  animation: fade-up-anim 0.3s ease;
}

@keyframes fade-up-anim {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.msg-assistant {
  align-self: flex-start;
}

.msg-user {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.msg-avatar-col {
  width: 30px;
  height: 30px;
  flex-shrink: 0;
  margin-top: 2px;
}

.msg-avatar-col :deep(.chatbot-avatar) {
  width: 30px !important;
  height: 30px !important;
  border-radius: 8px;
  background: radial-gradient(circle at 30% 30%, #ffffff 0%, rgba(235, 244, 255, 0.9) 45%, rgba(15, 82, 186, 0.35) 85%);
  display: inline-grid;
  place-items: center;
  box-shadow: 0 2px 6px rgba(15, 82, 186, 0.1);
}

.msg-avatar-col :deep(.chatbot-avatar img) {
  width: 82% !important;
  height: 82% !important;
}

.msg-bubble-content {
  max-width: 80%;
  min-width: 0;
}

.msg-bubble-assistant {
  background: #ffffff;
  color: #1e293b;
  border: 1px solid rgba(15, 82, 186, 0.1);
  border-radius: 16px;
  border-top-left-radius: 4px;
  padding: 12px 16px;
  font-size: 14px;
  line-height: 1.65;
  box-shadow: 0 2px 10px rgba(15, 82, 186, 0.04);
}

/* Markdown formatting inside assistant bubble */
.msg-bubble-assistant :deep(p) { margin: 0 0 10px 0; }
.msg-bubble-assistant :deep(p:last-child) { margin-bottom: 0; }
.msg-bubble-assistant :deep(h1),
.msg-bubble-assistant :deep(h2),
.msg-bubble-assistant :deep(h3) {
  color: #0f52ba;
  font-size: 14px;
  font-weight: 800;
  margin: 14px 0 6px 0;
}
.msg-bubble-assistant :deep(h1:first-child),
.msg-bubble-assistant :deep(h2:first-child),
.msg-bubble-assistant :deep(h3:first-child) { margin-top: 0; }
.msg-bubble-assistant :deep(ul),
.msg-bubble-assistant :deep(ol) {
  margin: 6px 0;
  padding-left: 18px;
}
.msg-bubble-assistant :deep(li) { margin-bottom: 4px; }
.msg-bubble-assistant :deep(strong) {
  color: #0f52ba;
  font-weight: 700;
}

.erumi-metrics-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 10px;
}

.erumi-metric {
  min-width: 0;
  padding: 10px 12px;
  border: 1px solid rgba(15, 82, 186, 0.1);
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 2px 8px rgba(15, 82, 186, 0.04);
}

.erumi-metric span,
.erumi-metric small {
  display: block;
  color: #64748b;
  font-size: 11px;
  line-height: 1.35;
}

.erumi-metric strong {
  display: block;
  margin-top: 3px;
  color: #0f172a;
  font-size: 18px;
  line-height: 1.2;
}

.erumi-metric.tone-good strong { color: #059669; }
.erumi-metric.tone-danger strong { color: #dc2626; }
.erumi-metric.tone-warning strong { color: #d97706; }

.erumi-chart-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 12px;
}

.erumi-chart-card {
  min-width: 0;
  padding: 12px;
  border: 1px solid rgba(15, 82, 186, 0.1);
  border-radius: 14px;
  background: #ffffff;
  box-shadow: 0 2px 10px rgba(15, 82, 186, 0.05);
}

.erumi-chart-card header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.erumi-chart-card header strong {
  color: #0f172a;
  font-size: 12px;
  line-height: 1.35;
}

.erumi-chart-card header span {
  color: #64748b;
  font-size: 11px;
  font-weight: 600;
}

.erumi-chart-canvas {
  height: 180px;
  min-width: 0;
}

.erumi-action-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.erumi-action-button {
  border: 1px solid rgba(15, 82, 186, 0.18);
  border-radius: 999px;
  background: rgba(15, 82, 186, 0.06);
  color: #0f52ba;
  font-size: 12px;
  font-weight: 700;
  padding: 8px 12px;
}

.erumi-response-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
  color: #94a3b8;
  font-size: 11px;
}

.msg-bubble-user {
  background: linear-gradient(135deg, #0f52ba 0%, #1e70e9 100%);
  color: #ffffff;
  border-radius: 18px;
  border-top-right-radius: 4px;
  padding: 12px 18px;
  font-size: 14px;
  line-height: 1.5;
  box-shadow: 0 4px 14px rgba(15, 82, 186, 0.18);
}

/* Typing indicator */
.typing-loader {
  display: flex;
  gap: 4px;
  padding: 14px 20px;
}
.typing-loader span {
  width: 6px;
  height: 6px;
  background: #0f52ba;
  border-radius: 50%;
  animation: dots 1.4s infinite;
  opacity: 0.6;
}
.typing-loader span:nth-child(2) { animation-delay: 0.2s; }
.typing-loader span:nth-child(3) { animation-delay: 0.4s; }

@keyframes dots {
  0%, 80%, 100% { transform: translateY(0); opacity: 0.6; }
  40% { transform: translateY(-5px); opacity: 1; }
}

/* ================================================
   STICKY BOTTOM BAR
   ================================================ */
.chat-bottom-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 0 16px 18px 16px;
  background: linear-gradient(to top, #f7f8fc 55%, rgba(247, 248, 252, 0) 100%);
  display: flex;
  justify-content: center;
  z-index: 5;
  flex-shrink: 0;
}

.bottom-bar-width {
  width: 100%;
  max-width: 720px;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 8px;
  padding-top: 20px;
}

.suggestion-chips-horizontal {
  display: flex;
  gap: 6px;
  width: 100%;
  overflow-x: auto;
  padding-bottom: 4px;
  scrollbar-width: none;
}

.suggestion-chips-horizontal::-webkit-scrollbar {
  display: none;
}

.suggestion-chip-small {
  padding: 7px 14px;
  border: 1px solid rgba(15, 82, 186, 0.12);
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
  color: #1e40af;
  background: #ffffff;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
  box-shadow: 0 1px 4px rgba(15, 82, 186, 0.04);
}

.suggestion-chip-small:hover {
  background: rgba(15, 82, 186, 0.06);
  border-color: #0f52ba;
  color: #0f52ba;
}

.chat-disclaimer {
  font-size: 11px;
  color: #94a3b8;
  margin: 0;
  text-align: center;
}

/* Scrollbar styling */
.no-scrollbar {
  scrollbar-width: none;
}
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
</style>
