<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { 
  ChevronDown,
  Folder,
  Send, 
  Square,
  Paperclip
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
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

const { projects, selectedProject, currentUser, loadDashboard } = useDashboardContext()

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

type ErumiTableColumn = {
  key: string
  label: string
  type?: string
  align?: 'left' | 'right' | 'center' | string
}

type ErumiTable = {
  title: string
  columns: ErumiTableColumn[]
  rows: Record<string, unknown>[]
  description?: string | null
}

type ErumiAction = {
  type: string
  label: string
  payload?: unknown
  requiresConfirmation?: boolean
}

type ErumiFile = {
  label: string
  format: string
  url: string
  description?: string | null
}

type ErumiChatResponse = {
  reply: string
  metrics: ErumiMetric[]
  tables: ErumiTable[]
  charts: ErumiChart[]
  actions: ErumiAction[]
  files: ErumiFile[]
  sources: string[]
  confidence: number
  usedAi: boolean
  intent: string
  latencyMs: number
}

type ChatEntry = {
  role: 'user' | 'assistant'
  text: string
  attachments?: { name: string; size: number }[]
  metrics?: ErumiMetric[]
  tables?: ErumiTable[]
  charts?: ErumiChart[]
  actions?: ErumiAction[]
  files?: ErumiFile[]
  sources?: string[]
  confidence?: number
  latencyMs?: number
}

type ErumiUploadedFile = {
  fileName: string
  contentType?: string | null
  size: number
  headers?: string[] | null
  previewRows?: string[][] | null
  totalRowCount?: number | null
  error?: string | null
}

const activeProjects = computed(() => projects.value.filter((p: any) => p.status !== 'Archived'))
const selectedTarget = ref('workspace') // 'workspace' or projectId
const selectedTargetLabel = computed(() => {
  if (selectedTarget.value === 'workspace') return 'Tất cả dự án'
  const project = projects.value.find((p: any) => p.id === selectedTarget.value)
  return project?.name || 'Dự án'
})

const chatInput = ref('')
const isChatting = ref(false)
const selectedFiles = ref<File[]>([])
const backgroundRefreshing = ref(false)
const lastRefreshedAt = ref<Date | null>(null)
const chatContainerRef = ref<HTMLElement | null>(null)
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
let refreshTimer: number | undefined

const isDropdownOpen1 = ref(false)
const isDropdownOpen2 = ref(false)
const dropdownRef1 = ref<HTMLElement | null>(null)
const dropdownRef2 = ref<HTMLElement | null>(null)

function selectTarget(id: string, instance: number) {
  selectedTarget.value = id
  if (instance === 1) isDropdownOpen1.value = false
  if (instance === 2) isDropdownOpen2.value = false
}

// Initial chat history with welcome message
const chatHistory = ref<ChatEntry[]>([
  {
    role: 'assistant',
    text: 'Xin chào! Mình là Erumi, trợ lý phân tích AI của Qaly. Hãy hỏi mình bất kỳ câu hỏi nào về các chỉ số hoặc dự án trong Workspace của bạn nhé.'
  }
])

const isChatActive = computed(() => chatHistory.value.length > 1 || isChatting.value)
const canSubmit = computed(() => (!!chatInput.value.trim() || selectedFiles.value.length > 0) && !isChatting.value)
const autoSyncLabel = computed(() => {
  if (backgroundRefreshing.value) return 'Đang cập nhật nền...'
  if (!lastRefreshedAt.value) return 'Tự cập nhật dữ liệu'
  return `Đã cập nhật ${lastRefreshedAt.value.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}`
})

// Dynamic suggestions based on selected workspace / project
const currentSuggestions = computed(() => {
  if (selectedTarget.value === 'workspace') {
    return [
      { label: 'Đánh giá hiệu suất tuần qua', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.' },
      { label: 'Dự án nào đang rủi ro?', prompt: 'Hiện tại có dự án nào đang gặp rủi ro hoặc chậm tiến độ không?' },
      { label: 'So sánh các dự án', prompt: 'So sánh các dự án đang hoạt động dưới dạng bảng.' }
    ]
  }

  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  const name = proj?.name || 'dự án'
  return [
    { label: 'Tóm tắt dự án', action: 'summary', prompt: `Tóm tắt nhanh tình hình hiện tại của dự án ${name}.` },
    { label: 'Phân tích rủi ro', action: 'risks', prompt: `Phân tích các rủi ro quá hạn và trễ việc của dự án ${name}.` },
    { label: 'Bảng workload', action: 'workload', prompt: `Liệt kê workload thành viên của dự án ${name} dưới dạng bảng.` },
    { label: 'Task quá hạn', action: 'overdue', prompt: `Liệt kê các task quá hạn của dự án ${name} dưới dạng bảng.` }
  ]
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

function tableCell(row: Record<string, unknown>, key: string) {
  const value = row[key]
  if (value === null || value === undefined || value === '') return '-'
  return String(value)
}

function confidenceLabel(value?: number) {
  if (value === undefined || value === null) return ''
  return `${Math.round(value * 100)}% tin cậy`
}

function formatUploadSize(size: number) {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${Math.round(size / 102.4) / 10} KB`
  return `${Math.round(size / 1024 / 102.4) / 10} MB`
}

function openFilePicker() {
  fileInputRef.value?.click()
}

function handleFileSelection(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  if (files.length) {
    selectedFiles.value = [...selectedFiles.value, ...files].slice(0, 4)
  }
  input.value = ''
}

function removeSelectedFile(index: number) {
  selectedFiles.value = selectedFiles.value.filter((_, i) => i !== index)
}

async function parseAttachedFiles(files: File[]): Promise<ErumiUploadedFile[]> {
  const parsed: ErumiUploadedFile[] = []

  for (const file of files) {
    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('firstRowIsHeader', 'true')

      const res = await fetch('/api/import/parse', {
        method: 'POST',
        body: formData
      })
      const data = await res.json()

      if (!res.ok || !data.isSuccess) {
        parsed.push({
          fileName: file.name,
          contentType: file.type,
          size: file.size,
          error: data?.error || 'Không thể đọc file bằng bộ parse hiện tại.'
        })
        continue
      }

      parsed.push({
        fileName: file.name,
        contentType: file.type,
        size: file.size,
        headers: data.data.headers ?? [],
        previewRows: data.data.previewRows ?? [],
        totalRowCount: data.data.totalRowCount ?? null
      })
    } catch {
      parsed.push({
        fileName: file.name,
        contentType: file.type,
        size: file.size,
        error: 'Không thể tải file lên để phân tích.'
      })
    }
  }

  return parsed
}

async function refreshAnalyticsContext() {
  if (backgroundRefreshing.value) return
  backgroundRefreshing.value = true
  try {
    await loadDashboard?.()
    lastRefreshedAt.value = new Date()
  } finally {
    backgroundRefreshing.value = false
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
  const about = proj?.description
    ? `Dự án **${name}** là về: ${proj.description}`
    : `Dự án **${name}** hiện chưa có mô tả chi tiết trong hệ thống.`
  const stats = getFallbackProjectStats()
  return `### 📋 Tóm tắt dự án **${name}** từ Erumi AI\n\n${about}\n\n- **Tiến trình**: Đã hoàn thành **${stats.doneTasks}/${stats.totalTasks} nhiệm vụ** (Đạt **${Math.round((stats.doneTasks / stats.totalTasks) * 100)}%**).\n- **Chất lượng**: Có **${stats.overdueTasks} nhiệm vụ quá hạn** cần xử lý.\n- **Thời gian**: Thực tế đã log **${stats.totalActualHours}h** trên tổng kế hoạch **${stats.totalEstimatedHours}h**.`
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

function isBossPrompt(value: string) {
  return ['sếp', 'sep', 'cấp trên', 'cap tren', 'leader', 'owner', 'người tạo dự án', 'nguoi tao du an', 'chủ dự án', 'chu du an']
    .some(term => value.includes(term))
}

function isTeamPrompt(value: string) {
  return isBossPrompt(value)
    || ['đồng đội', 'dong doi', 'thành viên', 'thanh vien', 'team', 'vai trò', 'vai tro', 'role']
      .some(term => value.includes(term))
}

function getFallbackTeamAnswer(prompt: string) {
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  if (!proj || selectedTarget.value === 'workspace') {
    return 'Bạn hãy chọn một dự án cụ thể ở dropdown phía trên, rồi hỏi lại về sếp hoặc thành viên trong dự án nhé.'
  }

  const userId = String(currentUser.value?.id || '').toLowerCase()
  const ownerId = String(proj.ownerId || '').toLowerCase()
  const requesterIsOwner = userId && userId === ownerId
  const ownerName = proj.ownerName || 'người tạo dự án'

  if (isBossPrompt(prompt)) {
    return requesterIsOwner
      ? `Bạn là người tạo dự án **${proj.name}**, nên trong dự án này bạn chính là **sếp/Owner mặc định**.`
      : `Sếp/Owner mặc định của dự án **${proj.name}** là **${ownerName}** - người tạo dự án này.`
  }

  const members = Array.isArray(proj.members) ? proj.members : []
  const rows = members
    .slice()
    .sort((a: any, b: any) => String(b.userId || '').toLowerCase() === ownerId ? 1 : String(a.userId || '').toLowerCase() === ownerId ? -1 : 0)
    .map((m: any) => `- **${m.fullName}**: ${m.role || 'Member'}${String(m.userId || '').toLowerCase() === ownerId ? ' - sếp/Owner mặc định' : ''}`)

  const ownerLine = requesterIsOwner
    ? 'Bạn là người tạo dự án nên bạn là **sếp/Owner mặc định**.'
    : `Sếp/Owner mặc định là **${ownerName}**.`

  return rows.length
    ? `Dự án **${proj.name}** hiện có **${rows.length} thành viên**. ${ownerLine}\n\n${rows.join('\n')}`
    : `Mình chưa thấy danh sách thành viên của **${proj.name}**. ${ownerLine}`
}

function getFallbackChatAnswer(prompt: string) {
  const p = prompt.toLowerCase()
  if (isTeamPrompt(p)) {
    return getFallbackTeamAnswer(p)
  }
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
  const filesToSend = selectedFiles.value.slice()
  if ((!prompt && filesToSend.length === 0) || isChatting.value) return

  const userText = prompt || 'Phân tích file đã đính kèm'
  chatHistory.value.push({
    role: 'user',
    text: userText,
    attachments: filesToSend.map(file => ({ name: file.name, size: file.size }))
  })
  chatInput.value = ''
  selectedFiles.value = []
  isChatting.value = true
  
  // Reset textarea height
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }

  await scrollToBottom()

  try {
    chatHistory.value.push({ role: 'assistant', text: '' })
    const lastIdx = chatHistory.value.length - 1
    const attachedFileContexts = filesToSend.length ? await parseAttachedFiles(filesToSend) : []
    const historyToSend = chatHistory.value
      .slice(1, -2)
      .slice(-6)
      .map(h => ({ role: h.role, content: h.text }))

    const fastReply = await apiJson<ErumiChatResponse>('/api/ai/chat/fast', {
      method: 'POST',
      body: JSON.stringify({
        message: userText,
        projectId: selectedTarget.value === 'workspace' ? null : selectedTarget.value,
        history: historyToSend,
        files: attachedFileContexts
      })
    })

    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: fastReply.reply,
      metrics: fastReply.metrics,
      tables: fastReply.tables,
      charts: fastReply.charts,
      actions: fastReply.actions,
      files: fastReply.files,
      sources: fastReply.sources,
      confidence: fastReply.confidence,
      latencyMs: fastReply.latencyMs
    }
  } catch (e) {
    const lastIdx = chatHistory.value.length - 1
    const fallbackText = getFallbackChatAnswer(userText)
    isChatting.value = false
    chatHistory.value[lastIdx].text = fallbackText
  } finally {
    isChatting.value = false
    await scrollToBottom()
  }
}

function handleDocumentClick(e: MouseEvent) {
  if (dropdownRef1.value && !dropdownRef1.value.contains(e.target as Node)) {
    isDropdownOpen1.value = false
  }
  if (dropdownRef2.value && !dropdownRef2.value.contains(e.target as Node)) {
    isDropdownOpen2.value = false
  }
}

onMounted(() => {
  if (selectedProject.value) {
    selectedTarget.value = selectedProject.value.id
  }
  refreshAnalyticsContext()
  refreshTimer = window.setInterval(refreshAnalyticsContext, 30000)
  window.addEventListener('focus', refreshAnalyticsContext)
  window.addEventListener('click', handleDocumentClick)
  scrollToBottom()
})

onBeforeUnmount(() => {
  if (refreshTimer) window.clearInterval(refreshTimer)
  window.removeEventListener('focus', refreshAnalyticsContext)
  window.removeEventListener('click', handleDocumentClick)
})
</script>

<template>
  <div class="analytics-chat-portal">
    <input
      ref="fileInputRef"
      class="hidden-file-input"
      type="file"
      multiple
      accept=".csv,.xlsx,.xls,.txt,.tsv,.json,.md,.docx,.pdf"
      @change="handleFileSelection"
    />

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
          <div class="erumi-composer">
            <div class="erumi-project-row">
              <div class="erumi-custom-dropdown" ref="dropdownRef1" @click="isDropdownOpen1 = !isDropdownOpen1">
                <div class="erumi-project-trigger" :class="{ 'is-open': isDropdownOpen1 }">
                  <Folder :size="20" class="folder-icon" />
                  <span class="erumi-project-name">{{ selectedTargetLabel }}</span>
                  <ChevronDown :size="16" class="erumi-project-chevron" :class="{ 'rotate-180': isDropdownOpen1 }" />
                </div>
                <Transition name="dropdown-fade">
                  <div class="erumi-dropdown-menu" v-if="isDropdownOpen1">
                    <div class="erumi-dropdown-item" @click.stop="selectTarget('workspace', 1)" :class="{ active: selectedTarget === 'workspace' }">Tất cả dự án</div>
                    <div v-for="p in activeProjects" :key="p.id" class="erumi-dropdown-item" @click.stop="selectTarget(p.id, 1)" :class="{ active: selectedTarget === p.id }">
                      {{ p.name }}
                    </div>
                  </div>
                </Transition>
              </div>
              <span class="erumi-last-updated">{{ autoSyncLabel }}</span>
            </div>

            <div v-if="selectedFiles.length" class="attached-file-list">
              <button
                v-for="(file, index) in selectedFiles"
                :key="`${file.name}-${index}`"
                type="button"
                class="attached-file-chip"
                @click="removeSelectedFile(index)"
              >
                <span>{{ file.name }}</span>
                <small>{{ formatUploadSize(file.size) }} · bỏ chọn</small>
              </button>
            </div>

            <div class="erumi-input-shell">
              <button class="erumi-attach-btn" title="Đính kèm tệp" type="button" @click="openFilePicker">
                <Paperclip :size="25" />
              </button>
              <textarea
                ref="textareaRef"
                v-model="chatInput"
                class="erumi-message-input"
                placeholder="Nhập câu hỏi..."
                :disabled="isChatting"
                aria-label="Nhập câu hỏi"
                rows="1"
                @input="autoResize"
                @keydown="handleKeydown"
              />
              <button 
                class="erumi-send-btn"
                type="button"
                :disabled="!canSubmit"
                :class="{ 'is-ready': canSubmit }"
                @click="submitChat()"
              >
                <Send :size="24" v-if="!isChatting" />
                <Square :size="14" v-else />
              </button>
            </div>
          </div>
          <p class="composer-hint">Enter để gửi · Shift+Enter để xuống dòng</p>
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

              <div v-if="msg.role === 'user' && msg.attachments?.length" class="msg-attachment-list">
                <span v-for="file in msg.attachments" :key="file.name" class="msg-attachment-chip">
                  {{ file.name }} · {{ formatUploadSize(file.size) }}
                </span>
              </div>

              <div v-if="msg.role === 'assistant' && msg.metrics?.length" class="erumi-metrics-grid">
                <article v-for="metric in msg.metrics" :key="metric.label" class="erumi-metric" :class="`tone-${metric.tone || 'neutral'}`">
                  <span>{{ metric.label }}</span>
                  <strong>{{ metric.value }}</strong>
                  <small v-if="metric.hint">{{ metric.hint }}</small>
                </article>
              </div>

              <div v-if="msg.role === 'assistant' && msg.tables?.length" class="erumi-table-stack">
                <article v-for="table in msg.tables" :key="table.title" class="erumi-table-card">
                  <header>
                    <strong>{{ table.title }}</strong>
                    <span v-if="table.description">{{ table.description }}</span>
                  </header>
                  <div class="erumi-table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th
                            v-for="column in table.columns"
                            :key="column.key"
                            :class="`align-${column.align || 'left'}`"
                          >
                            {{ column.label }}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-if="!table.rows.length">
                          <td :colspan="table.columns.length" class="empty-cell">Không có dữ liệu phù hợp</td>
                        </tr>
                        <tr v-for="(row, rowIndex) in table.rows" :key="rowIndex">
                          <td
                            v-for="column in table.columns"
                            :key="column.key"
                            :class="`align-${column.align || 'left'}`"
                          >
                            {{ tableCell(row, column.key) }}
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
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
                <button
                  v-for="action in msg.actions"
                  :key="action.type"
                  type="button"
                  class="erumi-action-button"
                  @click="submitChat(action.label)"
                >
                  {{ action.label }}
                </button>
              </div>

              <div v-if="msg.role === 'assistant' && msg.files?.length" class="erumi-file-list">
                <a v-for="file in msg.files" :key="file.url" class="erumi-file-chip" :href="file.url">
                  <span>{{ file.label }}</span>
                  <small>{{ file.format.toUpperCase() }}</small>
                </a>
              </div>

              <div v-if="msg.role === 'assistant' && (msg.sources?.length || msg.latencyMs !== undefined)" class="erumi-response-meta">
                <span v-if="msg.latencyMs !== undefined">Phản hồi {{ msg.latencyMs }}ms</span>
                <span v-if="msg.confidence !== undefined">{{ confidenceLabel(msg.confidence) }}</span>
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
        <div class="erumi-composer">
          <div class="erumi-project-row">
            <div class="erumi-custom-dropdown" ref="dropdownRef2" @click="isDropdownOpen2 = !isDropdownOpen2">
              <div class="erumi-project-trigger" :class="{ 'is-open': isDropdownOpen2 }">
                <Folder :size="20" class="folder-icon" />
                <span class="erumi-project-name">{{ selectedTargetLabel }}</span>
                <ChevronDown :size="16" class="erumi-project-chevron" :class="{ 'rotate-180': isDropdownOpen2 }" />
              </div>
              <Transition name="dropdown-fade">
                <div class="erumi-dropdown-menu" v-if="isDropdownOpen2">
                  <div class="erumi-dropdown-item" @click.stop="selectTarget('workspace', 2)" :class="{ active: selectedTarget === 'workspace' }">Tất cả dự án</div>
                  <div v-for="p in activeProjects" :key="p.id" class="erumi-dropdown-item" @click.stop="selectTarget(p.id, 2)" :class="{ active: selectedTarget === p.id }">
                    {{ p.name }}
                  </div>
                </div>
              </Transition>
            </div>
            <span class="erumi-last-updated">{{ autoSyncLabel }}</span>
          </div>

          <div v-if="selectedFiles.length" class="attached-file-list">
            <button
              v-for="(file, index) in selectedFiles"
              :key="`${file.name}-${index}`"
              type="button"
              class="attached-file-chip"
              @click="removeSelectedFile(index)"
            >
              <span>{{ file.name }}</span>
              <small>{{ formatUploadSize(file.size) }} · bỏ chọn</small>
            </button>
          </div>

          <div class="erumi-input-shell">
            <button class="erumi-attach-btn" title="Đính kèm tệp" type="button" @click="openFilePicker">
              <Paperclip :size="25" />
            </button>
            <textarea
              ref="textareaRef"
              v-model="chatInput"
              class="erumi-message-input"
              placeholder="Nhập liệu..."
              :disabled="isChatting"
              aria-label="Nhập câu hỏi tiếp theo"
              rows="1"
              @input="autoResize"
              @keydown="handleKeydown"
            />
            <button 
              class="erumi-send-btn"
              type="button"
              :disabled="!canSubmit"
              :class="{ 'is-ready': canSubmit }"
              @click="submitChat()"
            >
              <Send :size="24" v-if="!isChatting" />
              <Square :size="14" v-else />
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
  background: #f7f9fc;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
}

.hidden-file-input {
  display: none;
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
  max-width: 680px;
  padding: 28px 22px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 18px;
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
  border-radius: 50%;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  display: inline-grid;
  place-items: center;
}

.avatar-holder :deep(.chatbot-avatar img) {
  width: 70% !important;
  height: 70% !important;
}

.welcome-text-block {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.welcome-heading {
  font-size: 30px;
  font-weight: 800;
  color: #0f172a;
  letter-spacing: -0.5px;
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
  max-width: 640px;
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
  border: none;
  border-radius: 24px;
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  background: #f1f5f9;
  cursor: pointer;
  transition: all 0.2s ease;
}

.suggestion-pill:hover {
  background: #e2e8f0;
  color: #0f172a;
}

/* ================================================
   ERUMI FLOATING COMPOSER
   ================================================ */
.erumi-composer {
  width: 100%;
  max-width: 640px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: transparent;
  color: #0f172a;
}

.erumi-project-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 0 4px;
  margin-bottom: 4px;
}

.erumi-custom-dropdown {
  position: relative;
}

.erumi-project-trigger {
  position: relative;
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #0f172a;
  cursor: pointer;
  padding: 8px 16px;
  border-radius: 20px;
  background: transparent;
  transition: all 0.25s ease;
  border: 1px solid transparent;
}

.erumi-project-trigger:hover,
.erumi-project-trigger.is-open {
  background: #f1f5f9;
  border-color: #e2e8f0;
}

.folder-icon {
  color: #1f80ff;
  fill: rgba(31, 128, 255, 0.15);
  stroke-width: 2;
}

.erumi-project-name {
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #0f172a;
  font-size: 16px;
  font-weight: 700;
}

.erumi-project-chevron {
  color: #64748b;
  transition: transform 0.3s ease;
}

.rotate-180 {
  transform: rotate(180deg);
}

.erumi-dropdown-menu {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  width: max-content;
  min-width: 220px;
  max-width: 340px;
  background: #ffffff;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 14px;
  box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.1), 0 8px 10px -6px rgba(15, 23, 42, 0.04);
  padding: 6px;
  z-index: 100;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.erumi-dropdown-item {
  padding: 10px 14px;
  border-radius: 10px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  color: #334155;
  transition: all 0.2s;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.erumi-dropdown-item:hover {
  background: #f8fafc;
  color: #0f172a;
}

.erumi-dropdown-item.active {
  background: rgba(31, 128, 255, 0.08);
  color: #1f80ff;
  font-weight: 600;
}

.dropdown-fade-enter-active,
.dropdown-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.dropdown-fade-enter-from,
.dropdown-fade-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

.erumi-last-updated {
  flex-shrink: 0;
  color: #8a8f98;
  font-size: 15px;
  font-weight: 500;
  white-space: nowrap;
}

.attached-file-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 0;
}

.attached-file-chip,
.msg-attachment-chip {
  display: inline-flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  max-width: 100%;
  border: 1px solid rgba(15, 82, 186, 0.14);
  border-radius: 10px;
  background: rgba(15, 82, 186, 0.04);
  color: #0f172a;
  padding: 7px 10px;
  font-size: 12px;
  font-weight: 700;
}

.attached-file-chip {
  cursor: pointer;
}

.attached-file-chip small,
.msg-attachment-chip small {
  color: #64748b;
  font-size: 10px;
  font-weight: 700;
}

 .erumi-input-shell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 64px;
  padding: 8px 14px;

  border: none;
  outline: none;
  border-radius: 32px;

  background: rgba(255, 255, 255, 0.92);
  box-shadow:
    0 16px 40px rgba(15, 23, 42, 0.08),
    0 4px 12px rgba(15, 23, 42, 0.04);

  transition:
    box-shadow 0.25s ease,
    transform 0.25s ease,
    background 0.25s ease;
}

.erumi-input-shell:focus-within {
  border: none;
  outline: none;

  background: #ffffff;
  box-shadow:
    0 18px 46px rgba(15, 23, 42, 0.1),
    0 0 0 3px rgba(31, 128, 255, 0.08);

  transform: translateY(-1px);
}

.erumi-message-input {
  flex: 1;
  width: 100%;
  min-width: 0;
  min-height: 30px;
  max-height: 120px;
  padding: 10px 0;

  border: none;
  outline: none;
  box-shadow: none;
  resize: none;

  background: transparent;
  color: #0f172a;
  font: inherit;
  font-size: 16px;
  font-weight: 500;
  line-height: 1.5;

  overflow-y: auto;
  scrollbar-width: thin;
}

.erumi-message-input:focus,
.erumi-message-input:focus-visible,
.erumi-message-input:active {
  border: none;
  outline: none;
  box-shadow: none;
}

.erumi-message-input::placeholder {
  color: #94a3b8;
  font-weight: 400;
}

.erumi-message-input:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

.erumi-attach-btn,
.erumi-send-btn {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
  display: grid;
  place-items: center;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.erumi-attach-btn:hover {
  background: #f1f5f9;
  color: #3b82f6;
  transform: scale(1.05);
}
.erumi-attach-btn:active {
  transform: scale(0.95);
}

.erumi-send-btn {
  cursor: not-allowed;
  color: #cbd5e1;
  background: #f8fafc;
}

.erumi-send-btn.is-ready {
  cursor: pointer;
  color: #ffffff;
  background: #2563eb;
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.25);
}

.erumi-send-btn.is-ready:hover {
  background: #1d4ed8;
  transform: translateY(-2px) scale(1.05);
  box-shadow: 0 6px 16px rgba(37, 99, 235, 0.35);
}

.erumi-send-btn.is-ready:active {
  transform: translateY(0) scale(0.95);
}

.erumi-send-btn:disabled {
  cursor: not-allowed;
}

/* ================================================
   ACTIVE CHAT THREAD
   ================================================ */
.chat-thread-container {
  flex: 1;
  overflow-y: auto;
  padding: 24px 16px 170px 16px;
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
  border-radius: 50%;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  display: inline-grid;
  place-items: center;
}

.msg-avatar-col :deep(.chatbot-avatar img) {
  width: 70% !important;
  height: 70% !important;
}

.msg-bubble-content {
  max-width: 80%;
  min-width: 0;
}

.msg-bubble-assistant {
  background: transparent;
  color: #334155;
  border: none;
  border-radius: 16px;
  padding: 8px 0;
  font-size: 14.5px;
  line-height: 1.6;
  box-shadow: none;
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

.erumi-table-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 12px;
}

.erumi-table-card {
  min-width: 0;
  overflow: hidden;
  border: 1px solid rgba(15, 82, 186, 0.1);
  border-radius: 14px;
  background: #ffffff;
  box-shadow: 0 2px 10px rgba(15, 82, 186, 0.05);
}

.erumi-table-card header {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: 12px 14px 8px;
  border-bottom: 1px solid rgba(15, 82, 186, 0.08);
}

.erumi-table-card header strong {
  color: #0f172a;
  font-size: 13px;
  line-height: 1.35;
}

.erumi-table-card header span {
  color: #64748b;
  font-size: 11px;
  line-height: 1.4;
}

.erumi-table-wrap {
  width: 100%;
  overflow-x: auto;
}

.erumi-table-wrap table {
  width: 100%;
  min-width: 560px;
  border-collapse: collapse;
  font-size: 12px;
}

.erumi-table-wrap th,
.erumi-table-wrap td {
  padding: 9px 12px;
  border-bottom: 1px solid rgba(15, 82, 186, 0.07);
  color: #334155;
  text-align: left;
  vertical-align: top;
  white-space: nowrap;
}

.erumi-table-wrap th {
  background: rgba(15, 82, 186, 0.04);
  color: #475569;
  font-weight: 800;
}

.erumi-table-wrap tr:last-child td {
  border-bottom: none;
}

.erumi-table-wrap .align-right {
  text-align: right;
}

.erumi-table-wrap .align-center {
  text-align: center;
}

.erumi-table-wrap .empty-cell {
  color: #94a3b8;
  text-align: center;
}

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

.erumi-file-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.erumi-file-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  max-width: 100%;
  border: 1px solid rgba(15, 82, 186, 0.16);
  border-radius: 999px;
  background: #ffffff;
  color: #0f52ba;
  font-size: 12px;
  font-weight: 800;
  line-height: 1.2;
  padding: 8px 12px;
  text-decoration: none;
  box-shadow: 0 2px 8px rgba(15, 82, 186, 0.04);
}

.erumi-file-chip:hover {
  background: rgba(15, 82, 186, 0.06);
  border-color: rgba(15, 82, 186, 0.28);
}

.erumi-file-chip small {
  color: #64748b;
  font-size: 10px;
  font-weight: 800;
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
  background: #1e293b;
  color: #ffffff;
  border-radius: 20px;
  border-top-right-radius: 4px;
  padding: 12px 18px;
  font-size: 14.5px;
  line-height: 1.5;
  box-shadow: none;
}

.msg-attachment-list {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 6px;
  margin-top: 8px;
}

.msg-attachment-chip {
  flex-direction: row;
  background: #ffffff;
  color: #0f52ba;
  border-color: rgba(15, 82, 186, 0.18);
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
  background: linear-gradient(to top, #f8fafc 58%, rgba(248, 250, 252, 0) 100%);
  display: flex;
  justify-content: center;
  z-index: 5;
  flex-shrink: 0;
}

.bottom-bar-width {
  width: 100%;
  max-width: 640px;
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
  border: none;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 500;
  color: #475569;
  background: #f1f5f9;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.suggestion-chip-small:hover {
  background: #e2e8f0;
  color: #0f172a;
}

.chat-disclaimer {
  font-size: 11px;
  color: #94a3b8;
  margin: 0;
  text-align: center;
}

@media (max-width: 760px) {
  .chat-empty-state {
    padding: 24px 14px 18px;
    gap: 16px;
  }

  .welcome-heading {
    font-size: 23px;
  }

  .composer-wrap-center,
  .bottom-bar-width {
    max-width: none;
  }

  .erumi-project-row {
    align-items: center;
    flex-direction: row;
    gap: 8px;
    padding: 0;
  }

  .erumi-project-name {
    max-width: calc(100vw - 230px);
    font-size: 15px;
  }

  .erumi-last-updated {
    max-width: 145px;
    overflow: hidden;
    text-align: right;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 12px;
  }

  .erumi-input-shell {
    min-height: 58px;
    gap: 8px;
    padding: 8px 10px;
  }

  .erumi-attach-btn,
  .erumi-send-btn {
    width: 38px;
    height: 38px;
  }

  .erumi-message-input {
    font-size: 15px;
  }

  .msg-bubble-content {
    max-width: 92%;
  }

  .chat-thread-container {
    padding-bottom: 210px;
  }

  .chat-bottom-bar {
    padding-inline: 10px;
  }
}

/* Scrollbar styling */
.no-scrollbar {
  scrollbar-width: none;
}
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
/* ================================================
   HARD RESET: remove textarea rectangle border
   Put this at the very end of <style scoped>
   ================================================ */

.erumi-input-shell,
.erumi-input-shell:hover,
.erumi-input-shell:focus,
.erumi-input-shell:focus-within {
  border: 0 !important;
  outline: 0 !important;
}

textarea.erumi-message-input,
textarea.erumi-message-input:hover,
textarea.erumi-message-input:focus,
textarea.erumi-message-input:focus-visible,
textarea.erumi-message-input:active,
textarea.erumi-message-input:disabled {
  display: block;
  flex: 1;
  width: 100%;
  min-width: 0;

  border: 0 !important;
  border-color: transparent !important;
  outline: 0 !important;
  box-shadow: none !important;

  background: transparent !important;
  appearance: none !important;
  -webkit-appearance: none !important;
}
</style>
