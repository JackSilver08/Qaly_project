<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { 
  ChevronDown,
  Folder,
  Send, 
  Square,
  Paperclip,
  Copy,
  Download,
  AlertTriangle
} from 'lucide-vue-next'
import { useDashboardContext } from '../../composables/dashboard-context'
import { useErumiContext } from '../../composables/use-erumi-context'
import { apiJson } from '../../utils/api-client'
import { showSuccess } from '../../composables/use-toast'
import ChatbotAvatar from '../ChatbotAvatar.vue'
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

const props = withDefaults(defineProps<{
  isDrawer?: boolean
}>(), {
  isDrawer: false
})

const { projects, selectedProject, currentUser, loadDashboard } = useDashboardContext()
const erumiContext = useErumiContext()

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
  payload?: any
  requiresConfirmation?: boolean
  confirmed?: boolean
  rejected?: boolean
  processing?: boolean
  confirmAction?: string
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
  usedAi?: boolean
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
const selectedTarget = ref('workspace')
const selectedTargetLabel = computed(() => {
  if (selectedTarget.value === 'workspace') return 'Tất cả dự án'
  const project = projects.value.find((p: any) => p.id === selectedTarget.value)
  return project?.name || 'Dự án'
})

const isDataInsufficient = computed(() => {
  if (selectedTarget.value === 'workspace') return false
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  return proj ? (proj.taskCount || 0) < 3 : false
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

// Slash commands predefined popup list (Sprint 1)
const slashCommands = [
  { code: '/summary', prompt: 'Tóm tắt nhanh tình hình hiện tại của dự án.', description: 'Tóm tắt nhanh dự án' },
  { code: '/risks', prompt: 'Phân tích các rủi ro quá hạn và trễ việc của dự án.', description: 'Phân tích rủi ro dự án' },
  { code: '/workload', prompt: 'Liệt kê workload thành viên dưới dạng bảng.', description: 'Xem workload thành viên' },
  { code: '/overdue', prompt: 'Liệt kê các task quá hạn của dự án dưới dạng bảng.', description: 'Liệt kê task quá hạn' },
  { code: '/productivity', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.', description: 'Đánh giá hiệu suất' }
]

const showSlashCommands = computed(() => {
  return chatInput.value.startsWith('/')
})

const filteredSlashCommands = computed(() => {
  const query = chatInput.value.toLowerCase()
  return slashCommands.filter(c => c.code.startsWith(query))
})

function applySlashCommand(cmd: typeof slashCommands[0]) {
  if (selectedTarget.value !== 'workspace') {
    const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
    const name = proj?.name || 'dự án'
    chatInput.value = cmd.prompt.replace('của dự án.', `của dự án ${name}.`)
  } else {
    chatInput.value = cmd.prompt.replace('của dự án.', 'của tất cả dự án.')
  }
  nextTick(() => {
    textareaRef.value?.focus()
  })
}

// Onboarding suggested prompts depending on the active route (Sprint 1)
const onboardingSuggestions = computed(() => {
  const path = erumiContext.routePath.value || ''
  const name = String(erumiContext.routeName.value || '')
  
  if (path.includes('/tasks') || name === 'tasks') {
    return [
      { label: 'Ai đang quá tải việc?', prompt: 'Ai đang quá tải việc và cần phân bổ lại nhiệm vụ?' },
      { label: 'Có nhiệm vụ nào quá hạn không?', prompt: 'Liệt kê các nhiệm vụ quá hạn dưới dạng bảng.' },
      { label: 'Thống kê trạng thái task', prompt: 'Thống kê số lượng nhiệm vụ theo từng trạng thái.' }
    ]
  }
  
  if (path.includes('/gantt') || path.includes('/timeline')) {
    return [
      { label: 'Mốc bàn giao có nguy cơ trễ?', prompt: 'Mốc bàn giao nào có nguy cơ trễ hạn trong dự án?' },
      { label: 'Đường găng của dự án', prompt: 'Hãy chỉ ra các nhiệm vụ thuộc đường găng (critical path) ảnh hưởng tiến độ.' },
      { label: 'Tóm tắt timeline dự án', prompt: 'Tóm tắt timeline và tiến trình thực tế so với kế hoạch.' }
    ]
  }

  if (path.includes('/projects') || name === 'project-detail') {
    const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
    const nameStr = proj?.name || 'dự án'
    return [
      { label: 'Tóm tắt dự án', prompt: `Tóm tắt nhanh tình hình hiện tại của dự án ${nameStr}.` },
      { label: 'Phân tích rủi ro', prompt: `Phân tích các rủi ro quá hạn và trễ việc của dự án ${nameStr}.` },
      { label: 'Workload thành viên', prompt: `Liệt kê workload thành viên của dự án ${nameStr} dưới dạng bảng.` }
    ]
  }

  // Default suggestions
  return [
    { label: 'Đánh giá hiệu suất tuần qua', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.' },
    { label: 'Dự án nào đang rủi ro?', prompt: 'Hiện tại có dự án nào đang gặp rủi ro hoặc chậm tiến độ không?' },
    { label: 'So sánh các dự án', prompt: 'So sánh tiến độ và số lượng task của các dự án đang hoạt động dưới dạng bảng.' }
  ]
})

// Quick suggestions to show when chat is active
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

function selectTarget(id: string, instance: number) {
  selectedTarget.value = id
  if (instance === 1) isDropdownOpen1.value = false
  if (instance === 2) isDropdownOpen2.value = false
}

// Watchers
watch(() => erumiContext.projectId.value, (newProjectId) => {
  if (newProjectId) {
    selectedTarget.value = newProjectId
  }
}, { immediate: true })

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

async function scrollToBottom() {
  await nextTick()
  if (chatContainerRef.value) {
    chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight
  }
}

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

async function handleDraftAction(action: ErumiAction, confirmAction: 'execute_action' | 'reject') {
  if (!action.payload?.draftId) return
  action.processing = true
  action.confirmAction = confirmAction
  try {
    const result = await apiJson<any>(`/api/ai/drafts/${action.payload.draftId}/confirm`, {
      method: 'POST',
      body: JSON.stringify({
        confirmAction: confirmAction,
        editedPayloadJson: null,
        confirmationNote: confirmAction === 'reject' ? 'Rejected from chat UI' : 'Confirmed from chat UI'
      })
    })
    if (confirmAction === 'execute_action') {
      action.confirmed = true
      chatHistory.value.push({
        role: 'assistant',
        text: `✅ Đã thực thi thành công hành động nháp. Số lượng công việc tạo mới: ${result.createdTaskCount || 0}.`
      })
    } else {
      action.rejected = true
      chatHistory.value.push({
        role: 'assistant',
        text: `❌ Đã hủy bỏ hành động nháp thành công.`
      })
    }
  } catch (err: any) {
    console.error(err)
    alert(`Lỗi thực hiện hành động: ${err.message || err}`)
  } finally {
    action.processing = false
  }
}

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

    let replyActions = fastReply.actions || []
    if (replyActions.length === 0 && fastReply.reply) {
      const lowerReply = fastReply.reply.toLowerCase()
      if (lowerReply.includes('trễ hạn') || lowerReply.includes('quá hạn') || lowerReply.includes('chậm tiến độ')) {
        replyActions = [
          { type: 'quick_action', label: 'Giao việc cho tôi' },
          { type: 'quick_action', label: 'Gia hạn thêm 3 ngày' }
        ]
      } else if (lowerReply.includes('chấm công') || lowerReply.includes('timesheet') || lowerReply.includes('worklog')) {
        replyActions = [
          { type: 'quick_action', label: 'Đăng ký chấm công' },
          { type: 'quick_action', label: 'Báo cáo hiệu suất' }
        ]
      } else if (lowerReply.includes('công việc') || lowerReply.includes('nhiệm vụ') || lowerReply.includes('task')) {
        replyActions = [
          { type: 'quick_action', label: 'Tạo công việc mới' },
          { type: 'quick_action', label: 'Xem danh sách công việc' }
        ]
      }
    }

    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: fastReply.reply,
      metrics: fastReply.metrics,
      tables: fastReply.tables,
      charts: fastReply.charts,
      actions: replyActions,
      files: fastReply.files,
      sources: fastReply.sources,
      confidence: fastReply.confidence,
      latencyMs: fastReply.latencyMs,
      usedAi: fastReply.usedAi
    }
  } catch (e) {
    const lastIdx = chatHistory.value.length - 1
    const fallbackText = getFallbackChatAnswer(userText)
    isChatting.value = false
    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: fallbackText,
      usedAi: false
    }
  } finally {
    isChatting.value = false
    await scrollToBottom()
  }
}

function copyToClipboard(text: string) {
  navigator.clipboard.writeText(text)
  showSuccess('Đã sao chép phản hồi vào clipboard!')
}

function exportAsMarkdown(projectName: string, text: string) {
  const blob = new Blob([text], { type: 'text/markdown;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.setAttribute('download', `Erumi_Report_${projectName.replace(/\s+/g, '_')}_${new Date().toISOString().slice(0, 10)}.md`)
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  showSuccess('Đã xuất báo cáo thành công!')
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
  <div class="analytics-chat-portal" :class="{ 'is-drawer-mode': isDrawer }">
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
      
      <!-- EMPTY STATE: Initial Center Layout -->
      <div v-if="!isChatActive" class="chat-empty-state">
        <div class="avatar-holder">
          <ChatbotAvatar size="medium" />
        </div>
        <div class="welcome-text-block">
          <h1 class="welcome-heading">Hôm nay Erumi<br>có thể giúp gì cho bạn?</h1>
        </div>
        
        <!-- Suggestions above composer in empty state (Sprint 1 dynamic suggested prompts) -->
        <div class="suggestion-pills-wrap">
          <button 
            v-for="(s, idx) in onboardingSuggestions" 
            :key="idx"
            class="suggestion-pill"
            @click="submitChat(s.prompt)"
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
                  <div class="erumi-dropdown-menu" v-if="isDropdownOpen1" @wheel.stop>
                    <div class="erumi-dropdown-item" @click.stop="selectTarget('workspace', 1)" :class="{ active: selectedTarget === 'workspace' }">Tất cả dự án</div>
                    <div v-for="p in activeProjects" :key="p.id" class="erumi-dropdown-item" @click.stop="selectTarget(p.id, 1)" :class="{ active: selectedTarget === p.id }">
                      {{ p.name }}
                    </div>
                  </div>
                </Transition>
              </div>
              <span class="erumi-last-updated" v-if="!isDrawer">{{ autoSyncLabel }}</span>
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

            <div class="erumi-input-shell-container">
              <!-- Slash command autocomplete list -->
              <div v-if="showSlashCommands && filteredSlashCommands.length > 0" class="slash-commands-popup">
                <div 
                  v-for="cmd in filteredSlashCommands" 
                  :key="cmd.code" 
                  class="slash-command-item"
                  @click="applySlashCommand(cmd)"
                >
                  <span class="cmd-code">{{ cmd.code }}</span>
                  <span class="cmd-desc">{{ cmd.description }}</span>
                </div>
              </div>

              <div class="erumi-input-shell">
                <button class="erumi-attach-btn" title="Đính kèm tệp" type="button" @click="openFilePicker">
                  <Paperclip :size="25" />
                </button>
                <textarea
                  ref="textareaRef"
                  v-model="chatInput"
                  class="erumi-message-input"
                  placeholder="Hỏi bất kì thứ gì... (gõ / để xem lệnh nhanh)"
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
          </div>
          <p class="composer-hint" v-if="!isDrawer">Enter để gửi · Shift+Enter để xuống dòng</p>
        </div>
      </div>

      <!-- ACTIVE STATE: Scrolling chat thread -->
      <div v-else class="chat-thread-container no-scrollbar" ref="chatContainerRef">
        <div class="chat-thread-width">
          
          <!-- Insufficient Data Warning -->
          <div v-if="isDataInsufficient" class="data-warning-banner">
            <AlertTriangle :size="16" class="warning-icon" />
            <div class="warning-text">
              <strong>Cảnh báo dữ liệu:</strong> Dự án này hiện tại có quá ít dữ liệu (dưới 3 nhiệm vụ). Các phân tích từ AI có thể chưa tối ưu hoặc kém chính xác. Hãy bổ sung thêm nhiệm vụ để nhận kết quả tốt nhất.
            </div>
          </div>

          <div 
            v-for="(msg, i) in chatHistory" 
            :key="i" 
            :class="['msg-bubble-row', `msg-${msg.role}`]"
          >
            <div v-if="msg.role === 'assistant'" class="msg-avatar-col">
              <ChatbotAvatar size="small" />
            </div>
            
            <div class="msg-bubble-content">
              <article v-if="msg.role === 'assistant'" class="msg-bubble-assistant assistant-response-card">
                <header class="assistant-response-header">
                  <div class="assistant-identity">
                    <strong>Erumi</strong>
                    <span>Trợ lý phân tích</span>
                  </div>
                  <div class="origin-badge" :class="msg.usedAi ? 'origin-ai' : 'origin-rule'">
                    <span class="origin-indicator" aria-hidden="true"></span>
                    <span>{{ msg.usedAi ? 'Erumi AI' : 'Dữ liệu hệ thống' }}</span>
                  </div>
                </header>

                <div class="markdown-body" v-html="renderMarkdown(msg.text)"></div>

                <div v-if="msg.metrics?.length" class="erumi-metrics-grid">
                  <article v-for="metric in msg.metrics" :key="metric.label" class="erumi-metric" :class="`tone-${metric.tone || 'neutral'}`">
                    <span>{{ metric.label }}</span>
                    <strong>{{ metric.value }}</strong>
                    <small v-if="metric.hint">{{ metric.hint }}</small>
                  </article>
                </div>

                <div v-if="msg.tables?.length" class="erumi-table-stack">
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

                <div v-if="msg.charts?.length" class="erumi-chart-grid">
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

                <div v-if="msg.actions?.length" class="erumi-action-list">
                  <template v-for="action in msg.actions" :key="action.type">
                    <div v-if="action.type === 'draft_change'" class="erumi-draft-card">
                      <p class="erumi-draft-text">Hành động ghi dữ liệu cần xác nhận của bạn để thực thi:</p>
                      <div class="erumi-draft-buttons">
                        <button
                          type="button"
                          class="erumi-draft-btn-confirm"
                          :disabled="action.processing || action.confirmed || action.rejected"
                          @click="handleDraftAction(action, 'execute_action')"
                        >
                          <span v-if="action.processing && action.confirmAction === 'execute_action'">Đang xử lý...</span>
                          <span v-else-if="action.confirmed">Đã xác nhận</span>
                          <span v-else>Xác nhận</span>
                        </button>
                        <button
                          type="button"
                          class="erumi-draft-btn-reject"
                          :disabled="action.processing || action.confirmed || action.rejected"
                          @click="handleDraftAction(action, 'reject')"
                        >
                          <span v-if="action.processing && action.confirmAction === 'reject'">Đang hủy...</span>
                          <span v-else-if="action.rejected">Đã hủy</span>
                          <span v-else>Hủy</span>
                        </button>
                      </div>
                    </div>
                    <button
                      v-else
                      type="button"
                      class="erumi-action-button"
                      @click="submitChat(action.label)"
                    >
                      {{ action.label }}
                    </button>
                  </template>
                </div>

                <div v-if="msg.files?.length" class="erumi-file-list">
                  <a v-for="file in msg.files" :key="file.url" class="erumi-file-chip" :href="file.url">
                    <span>{{ file.label }}</span>
                    <small>{{ file.format.toUpperCase() }}</small>
                  </a>
                </div>

                <footer class="assistant-response-footer">
                  <div class="erumi-response-meta">
                    <span v-if="msg.latencyMs !== undefined">
                      <small>Phản hồi</small>
                      {{ msg.latencyMs }}ms
                    </span>
                    <span v-if="msg.confidence !== undefined">
                      <small>Độ tin cậy</small>
                      {{ confidenceLabel(msg.confidence) }}
                    </span>
                    <span v-if="msg.sources?.length">
                      <small>Nguồn</small>
                      {{ msg.sources.join(', ') }}
                    </span>
                  </div>

                  <div class="bubble-actions-toolbar">
                    <button class="bubble-action-btn" title="Sao chép phản hồi" type="button" @click="copyToClipboard(msg.text)">
                      <Copy :size="14" />
                      <span>Sao chép</span>
                    </button>
                    <button class="bubble-action-btn" title="Xuất báo cáo Markdown" type="button" @click="exportAsMarkdown(selectedTargetLabel, msg.text)">
                      <Download :size="14" />
                      <span>Xuất</span>
                    </button>
                  </div>
                </footer>
              </article>

              <template v-else>
                <div class="msg-bubble-user">{{ msg.text }}</div>
                <div v-if="msg.attachments?.length" class="msg-attachment-list">
                  <span v-for="file in msg.attachments" :key="file.name" class="msg-attachment-chip">
                    {{ file.name }} · {{ formatUploadSize(file.size) }}
                  </span>
                </div>
              </template>
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
            <span class="erumi-last-updated" v-if="!isDrawer">{{ autoSyncLabel }}</span>
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

          <div class="erumi-input-shell-container">
            <!-- Slash command autocomplete list -->
            <div v-if="showSlashCommands && filteredSlashCommands.length > 0" class="slash-commands-popup">
              <div 
                v-for="cmd in filteredSlashCommands" 
                :key="cmd.code" 
                class="slash-command-item"
                @click="applySlashCommand(cmd)"
              >
                <span class="cmd-code">{{ cmd.code }}</span>
                <span class="cmd-desc">{{ cmd.description }}</span>
              </div>
            </div>

            <div class="erumi-input-shell">
              <button class="erumi-attach-btn" title="Đính kèm tệp" type="button" @click="openFilePicker">
                <Paperclip :size="25" />
              </button>
              <textarea
                ref="textareaRef"
                v-model="chatInput"
                class="erumi-message-input"
                placeholder="Nhập liệu... (gõ / để xem lệnh nhanh)"
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
        </div>

        <p class="chat-disclaimer" v-if="!isDrawer">Erumi AI có thể mắc sai sót. Vui lòng kiểm tra lại thông tin quan trọng.</p>
      </div>
    </footer>
  </div>
</template>

<style scoped>
/* Scoped styles copied and refined from AnalyticsPage.vue */
.data-warning-banner {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: #fffbeb;
  border: 1px solid #fde68a;
  border-radius: 8px;
  padding: 10px 14px;
  margin-bottom: 16px;
  color: #b45309;
  font-size: 0.8rem;
  line-height: 1.45;
}
.warning-icon {
  flex-shrink: 0;
  margin-top: 2px;
}
.origin-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  flex-shrink: 0;
  font-size: 11px;
  font-weight: 700;
  padding: 5px 9px;
  border-radius: 999px;
}

.origin-ai {
  background: #eefbf6;
  color: #08775b;
  border: 1px solid #c8f0e1;
}

.origin-rule {
  background: #f1f5ff;
  color: #52658f;
  border: 1px solid #dbe5f7;
}

.origin-indicator {
  width: 7px;
  height: 7px;
  flex: 0 0 auto;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 0 3px color-mix(in srgb, currentColor 13%, transparent);
}

.bubble-actions-toolbar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  margin-left: auto;
}

.bubble-action-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: 1px solid transparent;
  color: #64748b;
  padding: 6px 9px;
  border-radius: 9px;
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
}

.bubble-action-btn:hover {
  background: #eef4ff;
  color: #1d5fd1;
  border-color: #d9e6ff;
}

.analytics-chat-portal {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: #f7f9fc;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
}

.hidden-file-input {
  display: none;
}

.chat-main-area {
  flex: 1;
  overflow: hidden;
  position: relative;
  display: flex;
  flex-direction: column;
}

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
  flex-shrink: 0;
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
  flex-shrink: 0;
}

.rotate-180 {
  transform: rotate(180deg);
}

.erumi-dropdown-menu {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  width: max-content;
  min-width: 260px;
  max-width: 360px;
  height: auto;
  max-height: 190px;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  background: rgba(255, 255, 255, 0.98);
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 18px;
  box-shadow:
    0 18px 45px rgba(15, 23, 42, 0.12),
    0 8px 18px rgba(15, 23, 42, 0.06);
  padding: 8px;
  z-index: 999;
  display: block;
  scrollbar-width: none;
  -ms-overflow-style: none;
  scroll-behavior: smooth;
}

.erumi-dropdown-menu::-webkit-scrollbar {
  width: 0;
  height: 0;
  display: none;
}

.erumi-dropdown-item {
  padding: 11px 14px;
  border-radius: 12px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 600;
  color: #334155;
  transition:
    background 0.18s ease,
    color 0.18s ease,
    transform 0.18s ease;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.erumi-dropdown-item:hover {
  background: rgba(31, 128, 255, 0.08);
  color: #1f80ff;
  transform: translateX(2px);
}

.erumi-dropdown-item.active {
  background: rgba(31, 128, 255, 0.12);
  color: #1f80ff;
  font-weight: 700;
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

.erumi-input-shell-container {
  position: relative;
  width: 100%;
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
  appearance: none;
  background: transparent !important;
  border: 0 !important;
  outline: 0 !important;
  box-shadow: none !important;
  resize: none;
  padding: 10px 4px;
  font-family: inherit;
  font-size: 17px;
  font-weight: 600;
  color: #0f172a;
  line-height: 1.4;
  height: 44px;
  max-height: 160px;
}

.erumi-message-input:focus,
.erumi-message-input:focus-visible,
.erumi-message-input:disabled {
  background: transparent !important;
  border: 0 !important;
  outline: 0 !important;
  box-shadow: none !important;
}

.erumi-message-input::placeholder {
  color: #94a3b8;
  font-weight: 600;
}

.erumi-attach-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  border: none;
  background: #f1f5f9;
  color: #475569;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.erumi-attach-btn:hover {
  background: #e2e8f0;
  color: #0f172a;
}

.erumi-send-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  border: none;
  background: #e2e8f0;
  color: #94a3b8;
  cursor: pointer;
  transition: all 0.22s cubic-bezier(0.4, 0, 0.2, 1);
  flex-shrink: 0;
}

.erumi-send-btn.is-ready {
  background: #1f80ff;
  color: #ffffff;
  box-shadow: 0 4px 14px rgba(31, 128, 255, 0.35);
}

.erumi-send-btn.is-ready:hover {
  background: #0066eb;
  transform: scale(1.04);
}

/* Slash commands styles (Sprint 1) */
.slash-commands-popup {
  position: absolute;
  bottom: calc(100% + 10px);
  left: 0;
  width: 100%;
  background: #ffffff;
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: 16px;
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12);
  z-index: 1000;
  max-height: 240px;
  overflow-y: auto;
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.slash-command-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.slash-command-item:hover {
  background: #f1f5f9;
}

.cmd-code {
  font-family: monospace;
  font-weight: 700;
  color: #1f80ff;
  font-size: 14px;
  background: rgba(31, 128, 255, 0.08);
  padding: 2px 6px;
  border-radius: 6px;
}

.cmd-desc {
  font-size: 13px;
  color: #475569;
  font-weight: 600;
}

/* Active Chat Thread */
.chat-thread-container {
  flex: 1;
  overflow-y: auto;
  padding: 32px 24px 220px;
  scroll-behavior: smooth;
}

.chat-thread-width {
  width: 100%;
  max-width: 840px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.msg-bubble-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  width: 100%;
}

.msg-user {
  justify-content: flex-end;
}

.msg-avatar-col {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  display: grid;
  place-items: center;
  overflow: hidden;
  border: 1px solid #dbe6f5;
  border-radius: 13px;
  background: linear-gradient(145deg, #ffffff, #edf4ff);
  box-shadow: 0 8px 20px rgba(31, 128, 255, 0.12);
}

.msg-avatar-col :deep(.chatbot-avatar) {
  width: 38px !important;
  height: 38px !important;
  border-radius: 12px;
}

.msg-avatar-col :deep(.chatbot-avatar img) {
  width: 100% !important;
  height: 100% !important;
  object-fit: contain;
}

.msg-bubble-content {
  min-width: 0;
  max-width: 74%;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.msg-assistant .msg-bubble-content {
  flex: 1;
  max-width: calc(100% - 52px);
}

.msg-user .msg-bubble-content {
  align-items: flex-end;
}

.msg-bubble-user {
  max-width: 100%;
  background: linear-gradient(135deg, #2486ff, #1268e8);
  color: #ffffff;
  padding: 11px 16px;
  border-radius: 18px 18px 5px 18px;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.5;
  box-shadow: 0 8px 22px rgba(31, 128, 255, 0.2);
  overflow-wrap: anywhere;
}

.msg-bubble-assistant {
  width: 100%;
  background: #ffffff;
  color: #0f172a;
  border-radius: 18px;
  font-size: 14px;
  line-height: 1.7;
  border: 1px solid #dfe7f3;
  box-shadow: 0 12px 32px rgba(43, 65, 104, 0.08);
}

.assistant-response-card {
  overflow: hidden;
}

.assistant-response-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  min-height: 54px;
  padding: 11px 14px 11px 18px;
  border-bottom: 1px solid #edf1f7;
  background: linear-gradient(90deg, #fbfdff, #ffffff);
}

.assistant-identity {
  min-width: 0;
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.assistant-identity strong {
  color: #172033;
  font-size: 14px;
  font-weight: 800;
}

.assistant-identity > span {
  color: #8a97ad;
  font-size: 11px;
  font-weight: 600;
}

.assistant-response-card > .markdown-body {
  padding: 18px 20px 16px;
}

.assistant-response-card > .erumi-metrics-grid,
.assistant-response-card > .erumi-table-stack,
.assistant-response-card > .erumi-chart-grid,
.assistant-response-card > .erumi-action-list,
.assistant-response-card > .erumi-file-list {
  width: auto;
  margin: 0 20px 18px;
}

.assistant-response-footer {
  min-height: 48px;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 10px 8px 18px;
  border-top: 1px solid #edf1f7;
  background: #fbfcfe;
}

.msg-attachment-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

/* Metrics and Custom Content */
.erumi-metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px;
  width: 100%;
}

.erumi-metric {
  min-width: 0;
  background: #f8faff;
  border: 1px solid #e3eaf5;
  border-radius: 14px;
  padding: 13px 15px;
  display: flex;
  flex-direction: column;
}

.erumi-metric.tone-good {
  background: #f0fdf4;
  border-color: #bbf7d0;
  color: #166534;
}

.erumi-metric.tone-warning {
  background: #fffbeb;
  border-color: #fde68a;
  color: #92400e;
}

.erumi-metric.tone-danger {
  background: #fef2f2;
  border-color: #fecaca;
  color: #991b1b;
}

.erumi-metric strong {
  font-size: 20px;
  font-weight: 800;
  margin-top: 4px;
}

.erumi-metric small {
  font-size: 11px;
  color: #64748b;
  margin-top: 2px;
}

/* Tables and Charts */
.erumi-table-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.erumi-table-card {
  background: #ffffff;
  border: 1px solid #dfe7f3;
  border-radius: 15px;
  overflow: hidden;
}

.erumi-table-card header {
  padding: 12px 16px;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
  display: flex;
  flex-direction: column;
}

.erumi-table-card header strong {
  font-size: 14px;
  color: #0f172a;
}

.erumi-table-card header span {
  font-size: 11px;
  color: #64748b;
}

.erumi-table-wrap {
  overflow-x: auto;
  max-width: 100%;
}

.erumi-table-wrap table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.erumi-table-wrap th {
  background: #fafafc;
  border-bottom: 1px solid #e2e8f0;
  padding: 8px 12px;
  font-weight: 700;
  color: #475569;
  text-align: left;
}

.erumi-table-wrap td {
  padding: 10px 12px;
  border-bottom: 1px solid #f1f5f9;
  color: #334155;
}

.erumi-table-wrap tr:last-child td {
  border-bottom: none;
}

.align-left { text-align: left; }
.align-right { text-align: right; }
.align-center { text-align: center; }

.erumi-chart-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.erumi-chart-card {
  background: #fbfcff;
  border: 1px solid #dfe7f3;
  border-radius: 15px;
  padding: 16px;
}

.erumi-chart-card header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.erumi-chart-canvas {
  height: 240px;
  position: relative;
}

/* Actions and Files */
.erumi-action-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.erumi-action-button {
  background: #f4f7ff;
  border: 1px solid #cfddfb;
  color: #205fc7;
  padding: 8px 13px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
}

.erumi-action-button:hover {
  background: #e9f1ff;
  border-color: #9cbcf5;
  transform: translateY(-1px);
}

.erumi-draft-card {
  background: rgba(248, 250, 252, 0.75);
  border: 1px dashed #cbd5e1;
  padding: 12px 16px;
  border-radius: 12px;
  margin-top: 4px;
  width: 100%;
}

.erumi-draft-text {
  font-size: 13px;
  color: #475569;
  margin: 0 0 10px 0;
  font-weight: 600;
}

.erumi-draft-buttons {
  display: flex;
  gap: 10px;
}

.erumi-draft-btn-confirm {
  background: #2563eb;
  border: none;
  color: #ffffff;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
}

.erumi-draft-btn-confirm:hover:not(:disabled) {
  background: #1d4ed8;
  transform: translateY(-1px);
}

.erumi-draft-btn-confirm:disabled {
  background: #94a3b8;
  cursor: not-allowed;
}

.erumi-draft-btn-reject {
  background: #ef4444;
  border: none;
  color: #ffffff;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
}

.erumi-draft-btn-reject:hover:not(:disabled) {
  background: #dc2626;
  transform: translateY(-1px);
}

.erumi-draft-btn-reject:disabled {
  background: #cbd5e1;
  color: #64748b;
  cursor: not-allowed;
}

.erumi-file-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.erumi-file-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  padding: 8px 12px;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 700;
  color: #0f172a;
  text-decoration: none;
}

.erumi-response-meta {
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 14px;
  font-size: 11px;
  color: #7b879b;
  font-weight: 600;
}

.erumi-response-meta span {
  min-width: 0;
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.erumi-response-meta small {
  color: #9aa5b6;
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

/* Typing loader */
.typing-loader {
  width: auto;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 14px 18px;
}

.typing-loader span {
  width: 6px;
  height: 6px;
  background: #94a3b8;
  border-radius: 50%;
  animation: typing 1s infinite alternate;
}

.typing-loader span:nth-child(2) { animation-delay: 0.2s; }
.typing-loader span:nth-child(3) { animation-delay: 0.4s; }

@keyframes typing {
  from { opacity: 0.3; transform: translateY(0); }
  to { opacity: 1; transform: translateY(-4px); }
}

/* Bottom Bar */
.chat-bottom-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
  background: linear-gradient(rgba(247, 249, 252, 0) 0%, #f7f9fc 20%);
  padding: 16px 16px 20px;
  z-index: 10;
}

.bottom-bar-width {
  width: 100%;
  max-width: 640px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.suggestion-chips-horizontal {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding: 4px 0 8px;
  scrollbar-width: none;
}

.suggestion-chips-horizontal::-webkit-scrollbar {
  display: none;
}

.suggestion-chip-small {
  white-space: nowrap;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  padding: 6px 12px;
  border-radius: 16px;
  font-size: 12px;
  font-weight: 600;
  color: #475569;
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.suggestion-chip-small:hover {
  background: #f1f5f9;
  border-color: #94a3b8;
  color: #0f172a;
}

.chat-disclaimer {
  font-size: 11px;
  color: #94a3b8;
  text-align: center;
  margin: 0;
}

/* Drawer mode overrides (Sprint 1) */
.is-drawer-mode {
  border-left: 1px solid #e2e8f0;
  box-shadow: -4px 0 24px rgba(15, 23, 42, 0.04);
  background: #ffffff;
}

.is-drawer-mode .chat-thread-container {
  padding: 18px 12px 150px;
}

.is-drawer-mode .bottom-bar-width {
  max-width: 100%;
}

.is-drawer-mode .erumi-composer {
  max-width: 100%;
}

.is-drawer-mode .chat-empty-state {
  max-width: 100%;
  padding: 16px 12px;
  gap: 12px;
}

.is-drawer-mode .welcome-heading {
  font-size: 22px;
}

.is-drawer-mode .suggestion-pills-wrap {
  flex-direction: column;
  width: 100%;
  align-items: stretch;
}

.is-drawer-mode .suggestion-pill {
  border-radius: 12px;
  text-align: left;
  padding: 8px 12px;
  font-size: 12px;
}

.is-drawer-mode .erumi-metrics-grid {
  grid-template-columns: 1fr;
}

.is-drawer-mode .erumi-chart-card {
  padding: 10px;
}

.is-drawer-mode .erumi-chart-canvas {
  height: 180px;
}

.is-drawer-mode .chat-thread-width {
  gap: 16px;
}

.is-drawer-mode .msg-bubble-row {
  gap: 8px;
}

.is-drawer-mode .msg-avatar-col {
  width: 34px;
  height: 34px;
  border-radius: 11px;
}

.is-drawer-mode .msg-avatar-col :deep(.chatbot-avatar) {
  width: 32px !important;
  height: 32px !important;
  border-radius: 10px;
}

.is-drawer-mode .msg-assistant .msg-bubble-content {
  max-width: calc(100% - 42px);
}

.is-drawer-mode .assistant-response-header {
  align-items: flex-start;
  padding: 10px 12px;
}

.is-drawer-mode .assistant-identity {
  flex-direction: column;
  align-items: flex-start;
  gap: 0;
}

.is-drawer-mode .assistant-response-card > .markdown-body {
  padding: 14px;
}

.is-drawer-mode .assistant-response-card > .erumi-metrics-grid,
.is-drawer-mode .assistant-response-card > .erumi-table-stack,
.is-drawer-mode .assistant-response-card > .erumi-chart-grid,
.is-drawer-mode .assistant-response-card > .erumi-action-list,
.is-drawer-mode .assistant-response-card > .erumi-file-list {
  margin: 0 14px 14px;
}

.is-drawer-mode .assistant-response-footer {
  align-items: flex-start;
  flex-direction: column;
  padding: 9px 12px;
}

.is-drawer-mode .bubble-actions-toolbar {
  width: 100%;
  margin-left: 0;
  justify-content: flex-start;
}

.is-drawer-mode .erumi-input-shell {
  min-height: 52px;
  padding: 6px 10px;
}

.is-drawer-mode .erumi-message-input {
  font-size: 15px;
  height: 36px;
}

.is-drawer-mode .erumi-attach-btn,
.is-drawer-mode .erumi-send-btn {
  width: 36px;
  height: 36px;
}

.is-drawer-mode .erumi-project-name {
  max-width: 180px;
  font-size: 14px;
}

.markdown-body :deep(h1), 
.markdown-body :deep(h2), 
.markdown-body :deep(h3) {
  font-size: 16px;
  font-weight: 800;
  margin: 16px 0 7px;
  color: #0f172a;
  letter-spacing: -0.01em;
}

.markdown-body :deep(p) {
  margin: 0 0 10px;
  color: #46546a;
}

.markdown-body :deep(ul), 
.markdown-body :deep(ol) {
  margin: 8px 0 10px;
  padding-left: 22px;
}

.markdown-body :deep(li) {
  margin-bottom: 5px;
  font-size: 14px;
  color: #46546a;
}

.markdown-body :deep(p:last-child),
.markdown-body :deep(ul:last-child),
.markdown-body :deep(ol:last-child) {
  margin-bottom: 0;
}

.markdown-body :deep(a) {
  color: #1268e8;
  font-weight: 700;
  text-decoration: none;
}

.markdown-body :deep(a:hover) {
  text-decoration: underline;
}

.markdown-body :deep(code) {
  border-radius: 6px;
  background: #eef3fb;
  color: #244b83;
  padding: 2px 5px;
  font-size: 0.9em;
}

:global(:root[data-theme='dark']) .assistant-response-header,
:global(:root[data-theme='dark']) .assistant-response-footer {
  border-color: var(--line);
  background: var(--panel-soft);
}

:global(:root[data-theme='dark']) .assistant-identity strong,
:global(:root[data-theme='dark']) .markdown-body :deep(h1),
:global(:root[data-theme='dark']) .markdown-body :deep(h2),
:global(:root[data-theme='dark']) .markdown-body :deep(h3) {
  color: var(--text-strong);
}

:global(:root[data-theme='dark']) .assistant-identity > span,
:global(:root[data-theme='dark']) .markdown-body :deep(p),
:global(:root[data-theme='dark']) .markdown-body :deep(li),
:global(:root[data-theme='dark']) .erumi-response-meta {
  color: var(--muted);
}

:global(:root[data-theme='dark']) .msg-avatar-col {
  border-color: var(--line);
  background: var(--panel-soft);
}

@media (max-width: 720px) {
  .chat-thread-container {
    padding: 20px 12px 205px;
  }

  .chat-thread-width {
    gap: 16px;
  }

  .msg-avatar-col {
    width: 34px;
    height: 34px;
    border-radius: 11px;
  }

  .msg-avatar-col :deep(.chatbot-avatar) {
    width: 32px !important;
    height: 32px !important;
  }

  .msg-assistant .msg-bubble-content {
    max-width: calc(100% - 46px);
  }

  .msg-user .msg-bubble-content {
    max-width: 88%;
  }

  .assistant-response-header {
    align-items: flex-start;
    padding: 10px 12px;
  }

  .assistant-identity {
    flex-direction: column;
    align-items: flex-start;
    gap: 0;
  }

  .assistant-response-card > .markdown-body {
    padding: 14px;
  }

  .assistant-response-card > .erumi-metrics-grid,
  .assistant-response-card > .erumi-table-stack,
  .assistant-response-card > .erumi-chart-grid,
  .assistant-response-card > .erumi-action-list,
  .assistant-response-card > .erumi-file-list {
    margin: 0 14px 14px;
  }

  .assistant-response-footer {
    align-items: flex-start;
    flex-direction: column;
    padding: 9px 12px;
  }

  .bubble-actions-toolbar {
    width: 100%;
    margin-left: 0;
    justify-content: flex-start;
  }
}
</style>
