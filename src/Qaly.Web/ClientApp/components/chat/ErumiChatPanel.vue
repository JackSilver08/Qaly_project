<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import {
  Send,
  Square,
  AlertTriangle,
  Copy,
  Download,
  Database,
  Clock3,
  Settings2,
  MessageSquarePlus,
  X
} from 'lucide-vue-next'
import { useDashboardContext } from '../../composables/dashboard-context'
import { useErumiContext } from '../../composables/use-erumi-context'
import { apiJson } from '../../utils/api-client'
import { showError, showSuccess } from '../../composables/use-toast'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import AiModelSelector from '../analytics-ai/AiModelSelector.vue'
import AnalyticsSideDrawer from '../analytics-ai/AnalyticsSideDrawer.vue'
import ConversationHistoryDrawer from '../analytics-ai/ConversationHistoryDrawer.vue'
import SourceRefsDrawer from '../analytics-ai/SourceRefsDrawer.vue'
import ComposerPlusMenu from '../analytics-ai/ComposerPlusMenu.vue'
import OverflowMenu from '../analytics-ai/OverflowMenu.vue'
import {
  AI_MODEL_OPTIONS,
  aiModelCompactLabel,
  type AiModelOption,
  type AiToolbarAction,
  type AnalyticsMiniTab,
  type ConversationHistoryItem,
  type SourceRef
} from '../analytics-ai/types'
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
const route = useRoute()

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

type AiModelMetadata = {
  id?: string | null
  label?: string | null
  provider?: string | null
  status?: string | null
}

type ErumiChatResponse = {
  reply: string
  metrics: ErumiMetric[]
  tables: ErumiTable[]
  charts: ErumiChart[]
  actions: ErumiAction[]
  files: ErumiFile[]
  sources: string[]
  sourceRefs?: SourceRef[] | null
  confidence: number
  confidenceReason?: string | null
  freshness?: string | null
  usedAi: boolean
  intent: string
  latencyMs: number
  model?: AiModelMetadata | null
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
  sourceRefs?: SourceRef[] | null
  confidence?: number
  confidenceReason?: string | null
  freshness?: string | null
  latencyMs?: number
  usedAi?: boolean
  model?: AiModelMetadata | null
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

function applyRoutePrompt() {
  const rawPrompt = Array.isArray(route.query.prompt) ? route.query.prompt[0] : route.query.prompt
  if (typeof rawPrompt === 'string' && rawPrompt.trim()) {
    chatInput.value = rawPrompt.trim()
  }
  if (route.query.scope === 'workspace') {
    selectedTarget.value = 'workspace'
  }
  nextTick(() => textareaRef.value?.focus())
}

const selectedAiModel = ref(AI_MODEL_OPTIONS[0]?.id ?? 'auto')
const selectedProviderHint = computed(() => {
  switch (selectedAiModel.value) {
    case 'deepseek-v4-pro':
      return 'deepseek'
    case 'ollama-local':
      return 'local'
    default:
      return 'auto'
  }
})
const activeDrawerTab = ref<AnalyticsMiniTab>('sources')
const cockpitDrawerOpen = ref(false)
const selectedDrawerMessage = ref<ChatEntry | null>(null)
const isCompactViewport = ref(false)
const conversationHistory = ref<ConversationHistoryItem[]>([])
const ANALYTICS_HISTORY_KEY = 'qaly.analytics.erumi.history.v1'
const MAX_ANALYTICS_HISTORY_ITEMS = 20

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

// Suggested questions surfaced inside the "+" menu (progressive disclosure).
// Selecting one only fills the composer so the user can edit before sending.
const menuSuggestions = [
  { label: 'Đánh giá hiệu suất tuần qua', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.' },
  { label: 'Tìm dự án đang có rủi ro', prompt: 'Hiện tại có dự án nào đang gặp rủi ro hoặc chậm tiến độ không?' },
  { label: 'So sánh tiến độ các dự án', prompt: 'So sánh tiến độ và số lượng task của các dự án đang hoạt động dưới dạng bảng.' },
  { label: 'Tóm tắt vấn đề cần xử lý', prompt: 'Tóm tắt các vấn đề cần xử lý và ưu tiên hành động tiếp theo.' }
]

// Analysis tools surfaced inside the "+" menu. Prompts reuse the existing
// wording so the underlying chat behaviour is preserved.
const analysisTools: AiToolbarAction[] = [
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
  }
]

function handleSelectProject(id: string) {
  selectedTarget.value = id
}

function clearProjectContext() {
  selectedTarget.value = 'workspace'
}

// Watchers
watch(() => erumiContext.projectId.value, (newProjectId) => {
  if (newProjectId) {
    selectedTarget.value = newProjectId
  }
}, { immediate: true })

function buildWelcomeMessage(): ChatEntry {
  if (selectedTarget.value === 'workspace') {
    return {
      role: 'assistant',
      text: 'Xin chào! Mình là Erumi, trợ lý phân tích AI của Qaly. Hãy hỏi mình bất kỳ câu hỏi nào về các chỉ số hoặc dự án trong Workspace của bạn nhé.'
    }
  }
  const proj = projects.value.find((p: any) => p.id === selectedTarget.value)
  const name = proj?.name || 'Dự án'
  return {
    role: 'assistant',
    text: `Chào bạn! Mình đã nạp thành công dữ liệu dự án **${name}**. Hãy yêu cầu mình xuất các báo cáo nhanh hoặc hỏi bất cứ thông tin nào liên quan nhé.`
  }
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
const showSlashCommandsPopup = computed(() => showSlashCommands.value && filteredSlashCommands.value.length > 0)
const selectedAiModelOption = computed<AiModelOption | undefined>(() => {
  return AI_MODEL_OPTIONS.find(option => option.id === selectedAiModel.value) ?? AI_MODEL_OPTIONS[0]
})
const selectedAiModelCompactLabel = computed(() => aiModelCompactLabel(selectedAiModelOption.value))
const latestAssistantMessage = computed(() => {
  return chatHistory.value
    .slice()
    .reverse()
    .find(msg => msg.role === 'assistant' && (!!msg.text || !!msg.metrics?.length || !!msg.sources?.length))
})
const drawerMessage = computed(() => selectedDrawerMessage.value ?? latestAssistantMessage.value ?? null)
const drawerSources = computed(() => drawerMessage.value?.sources ?? [])
const drawerSourceRefs = computed(() => drawerMessage.value?.sourceRefs ?? [])
const drawerMetrics = computed(() => drawerMessage.value?.metrics ?? [])
const drawerTables = computed(() => drawerMessage.value?.tables ?? [])
const drawerActions = computed(() => drawerMessage.value?.actions ?? [])
const composerPlaceholder = computed(() => {
  return isCompactViewport.value ? 'Hỏi Erumi...' : 'Hỏi bất kỳ điều gì về dự án... (gõ / để xem lệnh nhanh)'
})
const freshnessLabel = computed(() => {
  if (backgroundRefreshing.value) return 'Đang cập nhật dữ liệu...'
  if (!lastRefreshedAt.value) return 'Dữ liệu mới'
  return `Dữ liệu cập nhật lúc ${lastRefreshedAt.value.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}`
})
const cockpitDrawerTitle = computed(() => {
  const titles: Record<AnalyticsMiniTab, string> = {
    insights: 'Insights',
    metrics: 'Metrics',
    risks: 'Risks',
    sources: 'Nguồn dữ liệu',
    report: 'Report',
    actions: 'Actions',
    model: 'Thiết lập model',
    history: 'Lịch sử trò chuyện',
    settings: 'Thiết lập'
  }
  return titles[activeDrawerTab.value]
})

// Kebab (•••) menus. Keep secondary actions out of the default view while
// staying keyboard + screen-reader accessible.
const headerMenuItems = computed(() => [
  { key: 'new', label: 'Cuộc trò chuyện mới', icon: MessageSquarePlus },
  { key: 'history', label: 'Lịch sử', icon: Clock3 },
  { key: 'sources', label: 'Nguồn dữ liệu', icon: Database },
  { key: 'export', label: 'Xuất báo cáo', icon: Download },
  { key: 'settings', label: 'Thiết lập', icon: Settings2 }
])

function messageMenuItems(msg: ChatEntry) {
  const items = [
    { key: 'copy', label: 'Sao chép', icon: Copy },
    { key: 'export', label: 'Xuất báo cáo', icon: Download }
  ]
  if (msg.sources?.length || msg.sourceRefs?.length) {
    items.push({ key: 'sources', label: 'Xem nguồn', icon: Database })
  }
  return items
}

function handleHeaderMenu(key: string) {
  switch (key) {
    case 'new':
      startNewConversation()
      break
    case 'history':
      openCockpitDrawer('history')
      break
    case 'sources':
      openCockpitDrawer('sources')
      break
    case 'export':
      exportLatestReport()
      break
    case 'settings':
      openCockpitDrawer('settings')
      break
  }
}

function handleMessageMenu(key: string, msg: ChatEntry) {
  switch (key) {
    case 'copy':
      copyToClipboard(msg.text)
      break
    case 'export':
      exportAsMarkdown(selectedTargetLabel.value, msg.text)
      break
    case 'sources':
      openSourcesForMessage(msg)
      break
  }
}

function startNewConversation() {
  chatHistory.value = [buildWelcomeMessage()]
  selectedDrawerMessage.value = null
  closeCockpitDrawer()
  focusComposer()
}

function exportLatestReport() {
  const message = latestAssistantMessage.value
  if (!message?.text) {
    showError('Chưa có phản hồi nào để xuất báo cáo.')
    return
  }
  exportAsMarkdown(selectedTargetLabel.value, message.text)
}

function isExecutingDraftAction(action: any) {
  return action.processing && action.confirmAction === 'execute_action'
}

function isRejectingDraftAction(action: any) {
  return action.processing && action.confirmAction === 'reject'
}
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

function formatUploadSize(size: number) {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${Math.round(size / 102.4) / 10} KB`
  return `${Math.round(size / 1024 / 102.4) / 10} MB`
}

function openFilePicker() {
  fileInputRef.value?.click()
}

function syncViewportFlag() {
  isCompactViewport.value = window.innerWidth <= 640
}

function focusComposer() {
  nextTick(() => {
    textareaRef.value?.focus()
    autoResize()
  })
}

function fillComposer(prompt: string) {
  chatInput.value = prompt
  focusComposer()
}

function openCockpitDrawer(tab: AnalyticsMiniTab, message?: ChatEntry) {
  activeDrawerTab.value = tab
  selectedDrawerMessage.value = message ?? null
  cockpitDrawerOpen.value = true
}

function closeCockpitDrawer() {
  cockpitDrawerOpen.value = false
}

function openSourcesForMessage(message: ChatEntry) {
  openCockpitDrawer('sources', message)
}

function askAboutSource(sourceLabel: string) {
  fillComposer(`Giải thích nguồn "${sourceLabel}" và dữ liệu nào đã được dùng để tạo nhận định này.`)
}

function loadConversationHistory() {
  try {
    const raw = window.localStorage.getItem(ANALYTICS_HISTORY_KEY)
    const parsed = raw ? JSON.parse(raw) : []
    conversationHistory.value = Array.isArray(parsed)
      ? parsed
          .filter((item: ConversationHistoryItem) => item?.prompt && item?.createdAt)
          .slice(0, MAX_ANALYTICS_HISTORY_ITEMS)
      : []
  } catch {
    conversationHistory.value = []
  }
}

function persistConversationHistory() {
  try {
    window.localStorage.setItem(ANALYTICS_HISTORY_KEY, JSON.stringify(conversationHistory.value.slice(0, MAX_ANALYTICS_HISTORY_ITEMS)))
  } catch {
    // Local history is a convenience only; storage failures should not block chat.
  }
}

function rememberConversationPrompt(prompt: string, attachmentCount: number) {
  if (!prompt.trim()) return
  const item: ConversationHistoryItem = {
    id: `analytics-history-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    prompt: prompt.trim(),
    projectId: selectedTarget.value === 'workspace' ? null : selectedTarget.value,
    projectLabel: selectedTargetLabel.value,
    createdAt: new Date().toISOString(),
    attachmentCount: attachmentCount || undefined
  }

  conversationHistory.value = [
    item,
    ...conversationHistory.value.filter(existing => existing.prompt.trim() !== item.prompt || existing.projectId !== item.projectId)
  ].slice(0, MAX_ANALYTICS_HISTORY_ITEMS)
  persistConversationHistory()
}

function updateLatestConversationSnippet(text: string) {
  const first = conversationHistory.value[0]
  if (!first || !text.trim()) return
  first.assistantSnippet = text.replace(/\s+/g, ' ').trim().slice(0, 160)
  persistConversationHistory()
}

function restoreHistoryItem(item: ConversationHistoryItem) {
  fillComposer(item.prompt)
  closeCockpitDrawer()
}

function deleteHistoryItem(id: string) {
  conversationHistory.value = conversationHistory.value.filter(item => item.id !== id)
  persistConversationHistory()
}

function clearConversationHistory() {
  conversationHistory.value = []
  persistConversationHistory()
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

watch(selectedTarget, () => {
  chatHistory.value = [buildWelcomeMessage()]
  selectedDrawerMessage.value = null
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
    return 'Bạn hãy chọn một dự án cụ thể ở menu ngữ cảnh, rồi hỏi lại về sếp hoặc thành viên trong dự án nhé.'
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
  return `Chào bạn! Mình là Erumi. Hiện tại mô hình AI cục bộ đang ở trạng thái ngoại tuyến.\n\nTuy nhiên, bạn có thể chọn các dự án cụ thể trong menu ngữ cảnh và dùng nút **+** để mở các câu hỏi gợi ý hay công cụ phân tích để mình trích xuất báo cáo thông minh trực tiếp từ dữ liệu hệ thống nhé!`
}

async function handleDraftAction(action: ErumiAction, confirmAction: 'execute_action' | 'reject') {
  if (!action.payload?.draftId) return
  action.processing = true
  action.confirmAction = confirmAction
  try {
    const approvalUrl = action.payload.runId
      ? `/api/ai/agent-runs/${action.payload.runId}/approve`
      : `/api/ai/drafts/${action.payload.draftId}/confirm`
    const result = await apiJson<any>(approvalUrl, {
      method: 'POST',
      body: JSON.stringify({
        ...(action.payload.runId ? { action: confirmAction } : { confirmAction: confirmAction }),
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
    showError(`Lỗi thực hiện hành động: ${err.message || err}`)
  } finally {
    action.processing = false
  }
}

async function submitChat(explicitText?: string, _action?: string) {
  const prompt = (explicitText ?? chatInput.value).trim()
  const filesToSend = selectedFiles.value.slice()
  if ((!prompt && filesToSend.length === 0) || isChatting.value) return

  const userText = prompt || 'Phân tích file đã đính kèm'
  rememberConversationPrompt(userText, filesToSend.length)
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
        mode: 'agent',
        providerHint: selectedProviderHint.value,
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
      sourceRefs: fastReply.sourceRefs,
      confidence: fastReply.confidence,
      confidenceReason: fastReply.confidenceReason,
      freshness: fastReply.freshness,
      latencyMs: fastReply.latencyMs,
      usedAi: fastReply.usedAi,
      model: fastReply.model
    }
    updateLatestConversationSnippet(fastReply.reply)
  } catch (e) {
    const lastIdx = chatHistory.value.length - 1
    const detail = e instanceof Error ? e.message : 'Nhà cung cấp AI không phản hồi.'
    const errorText = `Không thể hoàn tất phân tích bằng model đã chọn. ${detail}`
    isChatting.value = false
    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: errorText,
      usedAi: false
    }
    updateLatestConversationSnippet(errorText)
    showError('Model AI đã chọn chưa sẵn sàng.')
  } finally {
    isChatting.value = false
    await scrollToBottom()
  }
}

async function copyToClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    showSuccess('Đã sao chép phản hồi vào clipboard!')
  } catch {
    showError('Không thể sao chép phản hồi.')
  }
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

onMounted(() => {
  syncViewportFlag()
  loadConversationHistory()
  if (selectedProject.value) {
    selectedTarget.value = selectedProject.value.id
  }
  applyRoutePrompt()
  refreshAnalyticsContext()
  refreshTimer = window.setInterval(refreshAnalyticsContext, 30000)
  window.addEventListener('resize', syncViewportFlag)
  window.addEventListener('focus', refreshAnalyticsContext)
  scrollToBottom()
})

watch(() => [route.query.prompt, route.query.scope], applyRoutePrompt)

onBeforeUnmount(() => {
  if (refreshTimer) window.clearInterval(refreshTimer)
  window.removeEventListener('resize', syncViewportFlag)
  window.removeEventListener('focus', refreshAnalyticsContext)
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

    <!-- EMPTY STATE -->
    <div v-if="!isChatActive" class="chat-empty">
      <div class="empty-inner">
        <div class="empty-avatar">
          <ChatbotAvatar size="medium" />
        </div>
        <h1 class="empty-heading">Bạn muốn phân tích điều gì?</h1>

        <!-- Composer -->
        <div class="composer">
          <div class="composer-surface">
            <div v-if="showSlashCommandsPopup" class="slash-popup">
              <button
                v-for="cmd in filteredSlashCommands"
                :key="cmd.code"
                type="button"
                class="slash-item"
                @click="applySlashCommand(cmd)"
              >
                <span class="slash-code">{{ cmd.code }}</span>
                <span class="slash-desc">{{ cmd.description }}</span>
              </button>
            </div>

            <textarea
              ref="textareaRef"
              v-model="chatInput"
              class="composer-input"
              :placeholder="composerPlaceholder"
              :disabled="isChatting"
              aria-label="Nhập câu hỏi cho Erumi"
              rows="1"
              @input="autoResize"
              @keydown="handleKeydown"
            />

            <div v-if="selectedFiles.length" class="composer-files">
              <span
                v-for="(file, index) in selectedFiles"
                :key="`${file.name}-${index}`"
                class="file-chip"
              >
                <span class="file-name">{{ file.name }}</span>
                <small>{{ formatUploadSize(file.size) }}</small>
                <button
                  type="button"
                  class="file-remove"
                  :aria-label="`Bỏ tệp ${file.name}`"
                  @click="removeSelectedFile(index)"
                >
                  <X :size="13" aria-hidden="true" />
                </button>
              </span>
            </div>

            <div class="composer-footer">
              <div class="composer-context">
                <ComposerPlusMenu
                  :projects="activeProjects"
                  :selected-target="selectedTarget"
                  :suggestions="menuSuggestions"
                  :tools="analysisTools"
                  :is-compact="isCompactViewport"
                  :hide-system="isDrawer"
                  @attach="openFilePicker"
                  @select-project="handleSelectProject"
                  @fill-prompt="fillComposer"
                  @open-drawer="openCockpitDrawer"
                />

                <button
                  v-if="selectedTarget !== 'workspace'"
                  type="button"
                  class="ctx-chip"
                  @click="clearProjectContext"
                >
                  <span class="ctx-name">{{ selectedTargetLabel }}</span>
                  <span class="ctx-x" aria-label="Bỏ chọn dự án"><X :size="13" aria-hidden="true" /></span>
                </button>
                <span v-else class="ctx-label">{{ selectedTargetLabel }}</span>
                <AiModelSelector
                  v-model="selectedAiModel"
                  :options="AI_MODEL_OPTIONS"
                  :compact="isCompactViewport"
                  @open-settings="openCockpitDrawer('model')"
                />
              </div>

              <button
                class="send-btn"
                type="button"
                :disabled="!canSubmit"
                :class="{ 'is-ready': canSubmit }"
                :aria-label="isChatting ? 'Đang xử lý' : 'Gửi câu hỏi'"
                @click="submitChat()"
              >
                <Send :size="18" v-if="!isChatting" aria-hidden="true" />
                <Square :size="16" v-else aria-hidden="true" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ACTIVE STATE -->
    <template v-else>
      <header class="chat-header">
        <h1 class="chat-title">Phân tích dự án</h1>
        <OverflowMenu
          :items="headerMenuItems"
          aria-label="Tùy chọn cuộc trò chuyện"
          trigger-title="Tùy chọn"
          @select="handleHeaderMenu"
        />
      </header>

      <div class="chat-thread no-scrollbar" ref="chatContainerRef">
        <div class="thread-width">
          <div v-if="isDataInsufficient" class="data-warning">
            <AlertTriangle :size="16" class="warning-icon" aria-hidden="true" />
            <p>
              <strong>Cảnh báo dữ liệu:</strong> Dự án này hiện có quá ít dữ liệu (dưới 3 nhiệm vụ). Phân tích từ AI có thể chưa tối ưu. Hãy bổ sung thêm nhiệm vụ để nhận kết quả tốt nhất.
            </p>
          </div>

          <div
            v-for="(msg, i) in chatHistory"
            :key="i"
            :class="['msg-row', `msg-${msg.role}`]"
          >
            <div v-if="msg.role === 'assistant'" class="msg-avatar">
              <ChatbotAvatar size="small" />
            </div>

            <div class="msg-content">
              <template v-if="msg.role === 'assistant'">
                <div class="assistant-body">
                  <div v-if="msg.text" class="markdown-body" v-html="renderMarkdown(msg.text)"></div>

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
                        <p class="erumi-draft-text">
                          {{ action.payload?.draftId && isDrawer ? 'Hành động ghi dữ liệu cần xác nhận của bạn để thực thi:' : 'Erumi đã chuẩn bị gợi ý hành động, nhưng pass này không thực thi hành động ghi dữ liệu trực tiếp trên /analytics.' }}
                        </p>
                        <ol v-if="action.payload?.events?.length" class="erumi-run-progress" aria-label="Tiến trình agent">
                          <li v-for="event in action.payload.events" :key="`${event.type}-${event.at}`">
                            <span class="erumi-run-check" aria-hidden="true">✓</span>
                            <span>{{ event.message }}</span>
                          </li>
                        </ol>
                        <div v-if="action.payload?.draftId && isDrawer" class="erumi-draft-buttons">
                          <button
                            type="button"
                            class="erumi-draft-btn-confirm"
                            :disabled="action.processing || action.confirmed || action.rejected"
                            @click="handleDraftAction(action, 'execute_action')"
                          >
                            <span v-if="isExecutingDraftAction(action)">Đang xử lý...</span>
                            <span v-else-if="action.confirmed">Đã xác nhận</span>
                            <span v-else>Xác nhận</span>
                          </button>
                          <button
                            type="button"
                            class="erumi-draft-btn-reject"
                            :disabled="action.processing || action.confirmed || action.rejected"
                            @click="handleDraftAction(action, 'reject')"
                          >
                            <span v-if="isRejectingDraftAction(action)">Đang hủy...</span>
                            <span v-else-if="action.rejected">Đã hủy</span>
                            <span v-else>Hủy</span>
                          </button>
                        </div>
                        <p v-else class="erumi-draft-note">Bạn có thể hỏi Erumi tạo lại bản nháp chi tiết hơn hoặc copy nội dung này để xử lý thủ công.</p>
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
                </div>

                <footer v-if="msg.text" class="assistant-meta">
                  <span class="assistant-mode" :class="msg.usedAi ? 'is-ai' : 'is-rule'">
                    {{ msg.usedAi ? 'Erumi AI' : 'Dữ liệu hệ thống' }}
                  </span>
                  <span
                    v-if="msg.model"
                    class="assistant-model"
                    :class="`status-${msg.model.status || 'live'}`"
                  >
                    {{ msg.model.label || msg.model.id }}
                    <em v-if="msg.model.status === 'fallback'">Fallback</em>
                  </span>
                  <span v-if="typeof msg.confidence === 'number'" class="assistant-conf" :title="msg.confidenceReason || undefined">
                    Độ tin cậy {{ Math.round(msg.confidence * 100) }}%
                  </span>
                  <div class="assistant-actions">
                    <OverflowMenu
                      :items="messageMenuItems(msg)"
                      aria-label="Tùy chọn phản hồi"
                      trigger-title="Tùy chọn phản hồi"
                      @select="key => handleMessageMenu(key, msg)"
                    />
                  </div>
                </footer>
              </template>

              <template v-else>
                <div class="msg-user-bubble">{{ msg.text }}</div>
                <div v-if="msg.attachments?.length" class="msg-attach">
                  <span v-for="file in msg.attachments" :key="file.name" class="msg-attach-chip">
                    {{ file.name }} · {{ formatUploadSize(file.size) }}
                  </span>
                </div>
              </template>
            </div>
          </div>

          <div v-if="isChatting" class="msg-row msg-assistant">
            <div class="msg-avatar">
              <ChatbotAvatar size="small" />
            </div>
            <div class="msg-content">
              <div class="typing-loader">
                <span></span><span></span><span></span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer class="chat-composer-bar">
        <div class="composer">
          <div class="composer-surface">
            <div v-if="showSlashCommandsPopup" class="slash-popup">
              <button
                v-for="cmd in filteredSlashCommands"
                :key="cmd.code"
                type="button"
                class="slash-item"
                @click="applySlashCommand(cmd)"
              >
                <span class="slash-code">{{ cmd.code }}</span>
                <span class="slash-desc">{{ cmd.description }}</span>
              </button>
            </div>

            <textarea
              ref="textareaRef"
              v-model="chatInput"
              class="composer-input"
              :placeholder="composerPlaceholder"
              :disabled="isChatting"
              aria-label="Nhập câu hỏi tiếp theo"
              rows="1"
              @input="autoResize"
              @keydown="handleKeydown"
            />

            <div v-if="selectedFiles.length" class="composer-files">
              <span
                v-for="(file, index) in selectedFiles"
                :key="`${file.name}-${index}`"
                class="file-chip"
              >
                <span class="file-name">{{ file.name }}</span>
                <small>{{ formatUploadSize(file.size) }}</small>
                <button
                  type="button"
                  class="file-remove"
                  :aria-label="`Bỏ tệp ${file.name}`"
                  @click="removeSelectedFile(index)"
                >
                  <X :size="13" aria-hidden="true" />
                </button>
              </span>
            </div>

            <div class="composer-footer">
              <div class="composer-context">
                <ComposerPlusMenu
                  :projects="activeProjects"
                  :selected-target="selectedTarget"
                  :suggestions="menuSuggestions"
                  :tools="analysisTools"
                  :is-compact="isCompactViewport"
                  :hide-system="isDrawer"
                  @attach="openFilePicker"
                  @select-project="handleSelectProject"
                  @fill-prompt="fillComposer"
                  @open-drawer="openCockpitDrawer"
                />

                <button
                  v-if="selectedTarget !== 'workspace'"
                  type="button"
                  class="ctx-chip"
                  @click="clearProjectContext"
                >
                  <span class="ctx-name">{{ selectedTargetLabel }}</span>
                  <span class="ctx-x" aria-label="Bỏ chọn dự án"><X :size="13" aria-hidden="true" /></span>
                </button>
                <span v-else class="ctx-label">{{ selectedTargetLabel }}</span>
                <AiModelSelector
                  v-model="selectedAiModel"
                  :options="AI_MODEL_OPTIONS"
                  :compact="isCompactViewport"
                  @open-settings="openCockpitDrawer('model')"
                />
              </div>

              <button
                class="send-btn"
                type="button"
                :disabled="!canSubmit"
                :class="{ 'is-ready': canSubmit }"
                :aria-label="isChatting ? 'Đang xử lý' : 'Gửi câu hỏi'"
                @click="submitChat()"
              >
                <Send :size="18" v-if="!isChatting" aria-hidden="true" />
                <Square :size="16" v-else aria-hidden="true" />
              </button>
            </div>
          </div>
          <p class="composer-disclaimer" v-if="!isDrawer">Erumi AI có thể mắc sai sót. Vui lòng kiểm tra lại thông tin quan trọng.</p>
        </div>
      </footer>
    </template>

    <AnalyticsSideDrawer
      v-if="!isDrawer"
      :open="cockpitDrawerOpen"
      :title="cockpitDrawerTitle"
      :subtitle="selectedAiModelCompactLabel"
      @close="closeCockpitDrawer"
    >
      <div class="analytics-drawer-content">
        <template v-if="activeDrawerTab === 'sources'">
          <p class="analytics-drawer-freshness">{{ freshnessLabel }}</p>
          <SourceRefsDrawer
            :sources="drawerSources"
            :source-refs="drawerSourceRefs"
            @ask-source="askAboutSource"
          />
        </template>

        <ConversationHistoryDrawer
          v-else-if="activeDrawerTab === 'history'"
          :items="conversationHistory"
          @restore="restoreHistoryItem"
          @delete="deleteHistoryItem"
          @clear="clearConversationHistory"
        />

        <section v-else-if="activeDrawerTab === 'metrics'" class="analytics-drawer-section">
          <p class="analytics-drawer-muted">Metrics được lấy từ phản hồi Erumi gần nhất, không gọi backend mới.</p>
          <div v-if="drawerMetrics.length" class="analytics-drawer-metric-list">
            <article v-for="metric in drawerMetrics" :key="metric.label" class="analytics-drawer-metric" :class="`tone-${metric.tone || 'neutral'}`">
              <span>{{ metric.label }}</span>
              <strong>{{ metric.value }}</strong>
              <small v-if="metric.hint">{{ metric.hint }}</small>
            </article>
          </div>
          <div v-else class="analytics-drawer-empty">
            <strong>Chưa có metric</strong>
            <span>Hãy hỏi Erumi về tiến độ, workload hoặc rủi ro để tạo metric.</span>
          </div>
        </section>

        <section v-else-if="activeDrawerTab === 'model' || activeDrawerTab === 'settings'" class="analytics-drawer-section">
          <p class="analytics-drawer-muted">Model bạn chọn được gửi cùng request. Kết quả hiển thị provider và model thực sự đã trả lời.</p>
          <AiModelSelector
            v-model="selectedAiModel"
            :options="AI_MODEL_OPTIONS"
            @open-settings="openCockpitDrawer('model')"
          />
          <div class="analytics-model-registry">
            <article v-for="option in AI_MODEL_OPTIONS" :key="option.id">
              <span>{{ option.badge }}</span>
              <strong>{{ option.label }}</strong>
              <p>{{ option.description }}</p>
            </article>
          </div>
        </section>

        <section v-else-if="activeDrawerTab === 'actions'" class="analytics-drawer-section">
          <p class="analytics-drawer-muted">Actions ở pass này chỉ là gợi ý hoặc prompt nháp, chưa thực thi ghi dữ liệu trực tiếp.</p>
          <div v-if="drawerActions.length" class="analytics-action-list">
            <button
              v-for="action in drawerActions"
              :key="action.label"
              type="button"
              class="analytics-action-draft"
              @click="fillComposer(action.label)"
            >
              {{ action.label }}
            </button>
          </div>
          <div v-else class="analytics-drawer-empty">
            <strong>Chưa có action</strong>
            <span>Dùng công cụ phân tích trong nút + để điền prompt tạo đề xuất tiếp theo.</span>
          </div>
        </section>

        <section v-else class="analytics-drawer-section">
          <p class="analytics-drawer-muted">Khu vực này giữ vai trò cockpit phụ, không thay thế luồng chat chính.</p>
          <div v-if="drawerTables.length" class="analytics-drawer-table-note">
            <strong>{{ drawerTables.length }} bảng dữ liệu</strong>
            <span>Các bảng chi tiết vẫn hiển thị trong câu trả lời chat để tránh nhân đôi dashboard.</span>
          </div>
          <div v-else class="analytics-drawer-empty">
            <strong>Chưa có dữ liệu</strong>
            <span>Chọn prompt từ nút + hoặc hỏi Erumi trực tiếp trong composer.</span>
          </div>
        </section>
      </div>
    </AnalyticsSideDrawer>
  </div>
</template>

<style scoped>
.analytics-chat-portal {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  max-width: 100%;
  background: var(--bg);
  color: var(--text);
  position: relative;
  overflow: hidden;
}

.analytics-chat-portal,
.analytics-chat-portal * {
  box-sizing: border-box;
}

.hidden-file-input {
  display: none;
}

/* ============ EMPTY STATE ============ */
.chat-empty {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
}

.empty-inner {
  margin: auto;
  width: 100%;
  max-width: 680px;
  padding: 32px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 20px;
}

.empty-avatar {
  width: 64px;
  height: 64px;
  display: grid;
  place-items: center;
}

.empty-avatar :deep(.chatbot-avatar) {
  width: 64px !important;
  height: 64px !important;
  border-radius: 50%;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  display: inline-grid;
  place-items: center;
}

.empty-avatar :deep(.chatbot-avatar img) {
  width: 70% !important;
  height: 70% !important;
}

.empty-heading {
  margin: 0;
  font-size: 26px;
  font-weight: 700;
  color: var(--text-strong);
  letter-spacing: -0.4px;
  line-height: 1.25;
  text-align: center;
}

/* ============ ACTIVE STATE ============ */
.chat-header {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 20px;
  border-bottom: 1px solid var(--line);
  background: var(--bg);
}

.chat-title {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
  color: var(--text-strong);
}

.chat-thread {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 24px 20px;
  scroll-behavior: smooth;
}

.thread-width {
  width: 100%;
  max-width: 780px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 22px;
}

.data-warning {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: var(--warning-soft);
  border: 1px solid var(--warning);
  border-radius: 10px;
  padding: 10px 14px;
  color: var(--warning-dark);
  font-size: 0.8rem;
  line-height: 1.45;
}

.data-warning p {
  margin: 0;
}

.warning-icon {
  flex-shrink: 0;
  margin-top: 2px;
}

.msg-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  width: 100%;
}

.msg-user {
  justify-content: flex-end;
}

.msg-avatar {
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  display: grid;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--panel);
}

.msg-avatar :deep(.chatbot-avatar) {
  width: 32px !important;
  height: 32px !important;
  border-radius: 8px;
}

.msg-avatar :deep(.chatbot-avatar img) {
  width: 100% !important;
  height: 100% !important;
  object-fit: contain;
}

.msg-content {
  min-width: 0;
  max-width: 74%;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.msg-assistant .msg-content {
  flex: 1;
  max-width: calc(100% - 46px);
}

.msg-user .msg-content {
  align-items: flex-end;
  max-width: min(62%, 680px);
}

.msg-user-row {
  justify-content: flex-end;
}

/* User bubble: compact, content-sized, and visually distinct from its row */
.msg-user-bubble {
  max-width: 100%;
  background: var(--primary);
  color: #ffffff;
  padding: 10px 15px;
  border-radius: 10px 10px 3px 10px;
  font-size: 14px;
  font-weight: 500;
  line-height: 1.5;
  overflow-wrap: anywhere;
}

/* Assistant response: flat, no card wrapper */
.assistant-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
  color: var(--text);
  font-size: 14px;
  line-height: 1.7;
}

.assistant-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 30px;
}

.assistant-mode {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 11px;
  font-weight: 600;
  color: var(--muted);
}

.assistant-mode::before {
  content: '';
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.assistant-mode.is-ai {
  color: var(--success);
}

.assistant-mode.is-rule {
  color: var(--muted);
}

.assistant-conf {
  font-size: 11px;
  font-weight: 500;
  color: var(--muted);
}

.assistant-model {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
  font-size: 11px;
  font-weight: 600;
  color: var(--muted);
}

.assistant-model.status-live {
  color: #047857;
}

.assistant-model.status-fallback {
  color: #b45309;
}

.assistant-model em {
  border: 1px solid currentColor;
  border-radius: 6px;
  padding: 1px 5px;
  font-size: 9px;
  font-style: normal;
  text-transform: uppercase;
}

.assistant-actions {
  margin-left: auto;
  opacity: 0;
  transition: opacity 0.14s ease;
}

.msg-row:hover .assistant-actions,
.assistant-actions:focus-within {
  opacity: 1;
}

.msg-attach {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.msg-attach-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel-soft);
  color: var(--muted);
  padding: 5px 9px;
  font-size: 11px;
  font-weight: 500;
}

/* Metrics / tables / charts keep their own surface */
.erumi-metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 10px;
  width: 100%;
}

.erumi-metric {
  min-width: 0;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 10px;
  padding: 13px 15px;
  display: flex;
  flex-direction: column;
}

.erumi-metric span {
  font-size: 12px;
  color: var(--muted);
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
  font-weight: 700;
  margin-top: 4px;
  color: var(--text-strong);
}

.erumi-metric.tone-good strong,
.erumi-metric.tone-warning strong,
.erumi-metric.tone-danger strong {
  color: inherit;
}

.erumi-metric small {
  font-size: 11px;
  color: var(--muted);
  margin-top: 2px;
}

.erumi-table-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.erumi-table-card {
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 10px;
  overflow: hidden;
}

.erumi-table-card header {
  padding: 12px 16px;
  background: var(--panel-soft);
  border-bottom: 1px solid var(--line);
  display: flex;
  flex-direction: column;
}

.erumi-table-card header strong {
  font-size: 14px;
  color: var(--text-strong);
}

.erumi-table-card header span {
  font-size: 11px;
  color: var(--muted);
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
  background: var(--panel-soft);
  border-bottom: 1px solid var(--line);
  padding: 8px 12px;
  font-weight: 600;
  color: var(--muted);
  text-align: left;
}

.erumi-table-wrap td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--line-light);
  color: var(--text);
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
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 10px;
  padding: 16px;
}

.erumi-chart-card header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.erumi-chart-card header strong {
  font-size: 14px;
  color: var(--text-strong);
}

.erumi-chart-card header span {
  font-size: 11px;
  color: var(--muted);
}

.erumi-chart-canvas {
  height: 240px;
  position: relative;
}

.erumi-action-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.erumi-action-button {
  background: var(--primary-soft);
  border: 1px solid transparent;
  color: var(--primary-strong);
  padding: 8px 13px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.16s ease, border-color 0.16s ease;
}

.erumi-action-button:hover,
.erumi-action-button:focus-visible {
  border-color: var(--primary);
  outline: none;
}

.erumi-draft-card {
  background: var(--panel-soft);
  border: 1px dashed var(--line);
  padding: 12px 16px;
  border-radius: 10px;
  width: 100%;
}

.erumi-draft-text {
  font-size: 13px;
  color: var(--muted);
  margin: 0 0 10px 0;
  font-weight: 500;
}

.erumi-run-progress {
  display: grid;
  gap: 7px;
  margin: 0 0 12px;
  padding: 0;
  list-style: none;
  color: var(--text);
  font-size: 12px;
}

.erumi-run-progress li {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}

.erumi-run-check {
  display: inline-grid;
  flex: 0 0 18px;
  width: 18px;
  height: 18px;
  place-items: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--success) 14%, transparent);
  color: var(--success);
  font-size: 11px;
  font-weight: 700;
}

.erumi-draft-buttons {
  display: flex;
  gap: 10px;
}

.erumi-draft-btn-confirm,
.erumi-draft-btn-reject {
  border: none;
  color: #ffffff;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.16s ease;
}

.erumi-draft-btn-confirm {
  background: var(--primary);
}

.erumi-draft-btn-confirm:hover:not(:disabled) {
  background: var(--primary-hover);
}

.erumi-draft-btn-confirm:disabled {
  background: var(--border-strong, #94a3b8);
  cursor: not-allowed;
}

.erumi-draft-btn-reject {
  background: var(--danger);
}

.erumi-draft-btn-reject:disabled {
  background: var(--line);
  color: var(--muted);
  cursor: not-allowed;
}

.erumi-draft-note {
  margin: 8px 0 0;
  color: var(--muted);
  font-size: 12px;
  line-height: 1.45;
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
  background: var(--panel-soft);
  border: 1px solid var(--line);
  padding: 8px 12px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
  text-decoration: none;
}

/* Typing loader */
.typing-loader {
  width: auto;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 10px 2px;
}

.typing-loader span {
  width: 6px;
  height: 6px;
  background: var(--muted);
  border-radius: 50%;
  animation: erumi-typing 1s infinite alternate;
}

.typing-loader span:nth-child(2) { animation-delay: 0.2s; }
.typing-loader span:nth-child(3) { animation-delay: 0.4s; }

@keyframes erumi-typing {
  from { opacity: 0.3; transform: translateY(0); }
  to { opacity: 1; transform: translateY(-4px); }
}

/* ============ COMPOSER ============ */
.chat-composer-bar {
  flex: 0 0 auto;
  padding: 8px 16px 14px;
  background: var(--bg);
  border-top: 1px solid var(--line);
}

.composer {
  width: 100%;
  max-width: 680px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.composer-surface {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px 18px;
  border: none;
  border-radius: 26px;
  background: var(--panel);
  box-shadow: var(--qaly-shadow-md);
}

.composer-input {
  width: 100%;
  min-width: 0;
  appearance: none;
  background: transparent !important;
  border: 0 !important;
  outline: 0 !important;
  box-shadow: none !important;
  resize: none;
  padding: 2px;
  font-family: inherit;
  font-size: 15px;
  font-weight: 400;
  color: var(--text-strong);
  line-height: 1.5;
  height: 28px;
  max-height: 160px;
}

.composer-input:hover,
.composer-input:focus,
.composer-input:focus-visible,
.composer-input:active,
.composer-input:disabled {
  background: transparent !important;
  border: 0 !important;
  outline: 0 !important;
  box-shadow: none !important;
}

.composer-input::placeholder {
  color: var(--text-muted);
}

.composer-files {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.file-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 100%;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel-soft);
  color: var(--text);
  padding: 5px 6px 5px 10px;
  font-size: 12px;
  font-weight: 500;
}

.file-chip .file-name {
  min-width: 0;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-chip small {
  color: var(--muted);
  font-size: 10px;
}

.file-remove {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  transition: background-color 0.14s ease, color 0.14s ease;
}

.file-remove:hover,
.file-remove:focus-visible {
  background: var(--danger-soft);
  color: var(--danger);
  outline: none;
}

.composer-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.composer-context {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.ctx-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  max-width: 220px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel-soft);
  color: var(--text);
  padding: 5px 6px 5px 10px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: border-color 0.14s ease;
}

.ctx-chip:hover,
.ctx-chip:focus-visible {
  border-color: var(--primary);
  outline: none;
}

.ctx-name {
  min-width: 0;
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ctx-x {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border-radius: 5px;
  color: var(--muted);
}

.ctx-label {
  min-width: 0;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--muted);
  font-size: 12px;
  font-weight: 500;
}

.send-btn {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  border-radius: 8px;
  border: none;
  background: var(--panel-soft);
  color: var(--text-muted);
  cursor: pointer;
  transition: background-color 0.16s ease, color 0.16s ease;
}

.send-btn.is-ready {
  background: var(--primary);
  color: #ffffff;
}

.send-btn.is-ready:hover {
  background: var(--primary-hover);
}

.send-btn:disabled {
  cursor: default;
}

.composer-disclaimer {
  margin: 0;
  font-size: 11px;
  color: var(--text-muted);
  text-align: center;
}

/* Slash command popup */
.slash-popup {
  position: absolute;
  bottom: calc(100% + 10px);
  left: 0;
  width: 100%;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 12px;
  box-shadow: var(--qaly-shadow-md);
  z-index: 1000;
  max-height: 240px;
  overflow-y: auto;
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.slash-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 9px 12px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  cursor: pointer;
  text-align: left;
  transition: background-color 0.14s ease;
}

.slash-item:hover,
.slash-item:focus-visible {
  background: var(--panel-soft);
  outline: none;
}

.slash-code {
  font-family: ui-monospace, monospace;
  font-weight: 600;
  color: var(--primary-strong);
  font-size: 13px;
  background: var(--primary-soft);
  padding: 2px 6px;
  border-radius: 6px;
}

.slash-desc {
  font-size: 13px;
  color: var(--text);
  font-weight: 500;
}

/* ============ MARKDOWN ============ */
.markdown-body :deep(h1),
.markdown-body :deep(h2),
.markdown-body :deep(h3) {
  font-size: 16px;
  font-weight: 700;
  margin: 14px 0 7px;
  color: var(--text-strong);
  letter-spacing: -0.01em;
}

.markdown-body :deep(h1:first-child),
.markdown-body :deep(h2:first-child),
.markdown-body :deep(h3:first-child),
.markdown-body :deep(p:first-child) {
  margin-top: 0;
}

.markdown-body :deep(p) {
  margin: 0 0 10px;
  color: var(--text);
}

.markdown-body :deep(ul),
.markdown-body :deep(ol) {
  margin: 8px 0 10px;
  padding-left: 22px;
}

.markdown-body :deep(li) {
  margin-bottom: 5px;
  font-size: 14px;
  color: var(--text);
}

.markdown-body :deep(p:last-child),
.markdown-body :deep(ul:last-child),
.markdown-body :deep(ol:last-child) {
  margin-bottom: 0;
}

.markdown-body :deep(a) {
  color: var(--primary-strong);
  font-weight: 600;
  text-decoration: none;
}

.markdown-body :deep(a:hover) {
  text-decoration: underline;
}

.markdown-body :deep(code) {
  border-radius: 6px;
  background: var(--primary-soft);
  color: var(--primary-strong);
  padding: 2px 5px;
  font-size: 0.9em;
}

/* ============ DRAWER CONTENT ============ */
.analytics-drawer-content,
.analytics-drawer-section {
  min-width: 0;
  display: grid;
  gap: 12px;
}

.analytics-drawer-freshness {
  margin: 0;
  padding: 8px 10px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel-soft);
  color: var(--muted);
  font-size: 12px;
  font-weight: 500;
}

.analytics-drawer-muted,
.analytics-drawer-empty span,
.analytics-drawer-table-note span,
.analytics-model-registry p {
  margin: 0;
  color: var(--muted);
  font-size: 12px;
  line-height: 1.45;
}

.analytics-drawer-metric-list,
.analytics-model-registry,
.analytics-action-list {
  min-width: 0;
  display: grid;
  gap: 8px;
}

.analytics-drawer-metric,
.analytics-model-registry article,
.analytics-drawer-empty,
.analytics-drawer-table-note,
.analytics-action-draft {
  min-width: 0;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--panel-soft);
  padding: 12px;
}

.analytics-drawer-metric {
  display: grid;
  gap: 4px;
}

.analytics-drawer-metric span,
.analytics-drawer-metric small,
.analytics-model-registry span {
  color: var(--muted);
  font-size: 11px;
  font-weight: 600;
}

.analytics-drawer-metric strong,
.analytics-drawer-empty strong,
.analytics-drawer-table-note strong,
.analytics-model-registry strong {
  min-width: 0;
  overflow: hidden;
  color: var(--text-strong);
  font-size: 14px;
  font-weight: 700;
  text-overflow: ellipsis;
}

.analytics-drawer-metric.tone-good {
  border-color: #bbf7d0;
  background: #f0fdf4;
}

.analytics-drawer-metric.tone-warning {
  border-color: #fde68a;
  background: #fffbeb;
}

.analytics-drawer-metric.tone-danger {
  border-color: #fecaca;
  background: #fef2f2;
}

.analytics-model-registry article,
.analytics-drawer-empty,
.analytics-drawer-table-note {
  display: grid;
  gap: 5px;
}

.analytics-model-registry span {
  width: max-content;
  border-radius: 999px;
  background: var(--primary-soft);
  color: var(--primary-strong);
  padding: 2px 7px;
}

.analytics-action-draft {
  width: 100%;
  color: var(--primary-strong);
  text-align: left;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

.analytics-action-draft:hover,
.analytics-action-draft:focus-visible {
  border-color: var(--primary);
  outline: none;
}

/* ============ DRAWER MODE (FloatingChatbot) ============ */
.is-drawer-mode {
  border-left: 1px solid var(--line);
  background: var(--panel);
}

.is-drawer-mode .composer,
.is-drawer-mode .thread-width {
  max-width: 100%;
}

.is-drawer-mode .empty-inner {
  max-width: 100%;
  padding: 18px 12px;
}

.is-drawer-mode .empty-heading {
  font-size: 20px;
}

.is-drawer-mode .chat-thread {
  padding: 18px 12px;
}

.is-drawer-mode .erumi-metrics-grid {
  grid-template-columns: 1fr;
}

.is-drawer-mode .erumi-chart-canvas {
  height: 180px;
}

.is-drawer-mode .msg-assistant .msg-content {
  max-width: calc(100% - 42px);
}

/* Dark-mode metric tone tweaks */
:global(:root[data-theme='dark']) .erumi-metric.tone-good,
:global(:root[data-theme='dark']) .analytics-drawer-metric.tone-good {
  background: rgba(34, 197, 94, 0.12);
  border-color: rgba(34, 197, 94, 0.4);
  color: #86efac;
}

:global(:root[data-theme='dark']) .erumi-metric.tone-warning,
:global(:root[data-theme='dark']) .analytics-drawer-metric.tone-warning {
  background: rgba(245, 158, 11, 0.12);
  border-color: rgba(245, 158, 11, 0.4);
  color: #fcd34d;
}

:global(:root[data-theme='dark']) .erumi-metric.tone-danger,
:global(:root[data-theme='dark']) .analytics-drawer-metric.tone-danger {
  background: rgba(248, 113, 113, 0.12);
  border-color: rgba(248, 113, 113, 0.4);
  color: #fca5a5;
}

/* Keyboard-only navigation should never be revealed only on hover */
@media (hover: none) {
  .assistant-actions {
    opacity: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .composer-surface,
  .send-btn,
  .erumi-action-button,
  .slash-item,
  .file-remove,
  .ctx-chip,
  .assistant-actions {
    transition: none;
  }

  .typing-loader span {
    animation: none;
  }

  .chat-thread {
    scroll-behavior: auto;
  }
}

@media (max-width: 720px) {
  .empty-inner {
    padding: 20px 14px;
    gap: 16px;
  }

  .empty-heading {
    font-size: 22px;
  }

  .chat-header {
    padding: 10px 14px;
  }

  .chat-thread {
    padding: 18px 12px;
  }

  .chat-composer-bar {
    padding: 8px 12px 12px;
  }

  .msg-content {
    max-width: 84%;
  }

  .msg-user .msg-content {
    max-width: 84%;
  }

  .msg-assistant .msg-content {
    max-width: calc(100% - 46px);
  }

  /* Always expose per-message actions on touch/mobile */
  .assistant-actions {
    opacity: 1;
  }
}
</style>
