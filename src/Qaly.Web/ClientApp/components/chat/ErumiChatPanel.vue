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
import { showError, showSuccess } from '../../composables/use-toast'
import ChatbotAvatar from '../ChatbotAvatar.vue'
import AiAnswerMetaChips from '../analytics-ai/AiAnswerMetaChips.vue'
import AiModelSelector from '../analytics-ai/AiModelSelector.vue'
import AiQuickToolbar from '../analytics-ai/AiQuickToolbar.vue'
import AnalyticsSideDrawer from '../analytics-ai/AnalyticsSideDrawer.vue'
import ConversationHistoryDrawer from '../analytics-ai/ConversationHistoryDrawer.vue'
import SourceRefsDrawer from '../analytics-ai/SourceRefsDrawer.vue'
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

const isDropdownOpen1 = ref(false)
const isDropdownOpen2 = ref(false)
const dropdownRef1 = ref<HTMLElement | null>(null)
const dropdownRef2 = ref<HTMLElement | null>(null)
const selectedAiModel = ref(AI_MODEL_OPTIONS[0]?.id ?? 'fast-current')
const activeDrawerTab = ref<AnalyticsMiniTab>('sources')
const cockpitDrawerOpen = ref(false)
const selectedDrawerMessage = ref<ChatEntry | null>(null)
const isCompactViewport = ref(false)
const toolPaletteOpen = ref(false)
const conversationHistory = ref<ConversationHistoryItem[]>([])
const dropdownPlacement1 = ref<'up' | 'down'>('down')
const dropdownPlacement2 = ref<'up' | 'down'>('down')
const dropdownMaxHeight1 = ref('320px')
const dropdownMaxHeight2 = ref('320px')
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

const visibleOnboardingSuggestions = computed(() => onboardingSuggestions.value.slice(0, 3))

// Quick suggestions to show when chat is active. Keep a larger internal pool,
// then render only three chips plus a lightweight "More" affordance.
const currentSuggestions = computed(() => {
  const latest = chatHistory.value
    .slice()
    .reverse()
    .find(msg => msg.role === 'assistant' && !!msg.text)
  const latestText = latest?.text.toLowerCase() || ''

  if (latest?.actions?.length || latestText.includes('rủi ro') || latestText.includes('quá hạn') || latestText.includes('chậm')) {
    return [
      { label: 'Task cần chú ý', prompt: 'Liệt kê các task cần chú ý nhất và lý do rủi ro.' },
      { label: 'Kế hoạch xử lý', prompt: 'Tạo kế hoạch xử lý nháp cho các rủi ro vừa nêu, chưa thực thi.' },
      { label: 'Nguồn chứng minh', prompt: 'Nguồn nào chứng minh các nhận định rủi ro này?' },
      { label: 'Báo cáo standup', prompt: 'Rút gọn phần rủi ro thành báo cáo standup.' }
    ]
  }

  if (latest?.metrics?.length || latest?.charts?.length || latest?.tables?.length) {
    return [
      { label: 'Giải thích số này', prompt: 'Giải thích các số liệu quan trọng nhất trong phản hồi vừa rồi.' },
      { label: 'So sánh tuần trước', prompt: 'So sánh các số liệu này với tuần trước nếu có dữ liệu.' },
      { label: 'Xuất báo cáo', prompt: 'Chuyển các số liệu này thành bản báo cáo markdown ngắn.' },
      { label: 'Tìm bất thường', prompt: 'Chỉ ra các điểm bất thường trong số liệu vừa phân tích.' }
    ]
  }

  if (latest?.files?.length || latestText.includes('báo cáo') || latestText.includes('standup')) {
    return [
      { label: 'Rút gọn standup', prompt: 'Rút gọn nội dung này thành bản standup 5 gạch đầu dòng.' },
      { label: 'Copy markdown', prompt: 'Định dạng lại câu trả lời vừa rồi thành markdown sạch để copy.' },
      { label: 'Checklist tiếp theo', prompt: 'Tạo checklist việc cần làm tiếp theo từ báo cáo vừa rồi.' },
      { label: 'Nguồn báo cáo', prompt: 'Nguồn nào được dùng để tạo báo cáo vừa rồi?' }
    ]
  }

  if (latest?.sources?.length || latest?.sourceRefs?.length) {
    return [
      { label: 'Nguồn chứng minh', prompt: 'Giải thích các nguồn đã dùng trong câu trả lời vừa rồi.' },
      { label: 'Điểm chưa chắc', prompt: 'Nhận định nào trong câu trả lời cần kiểm chứng thêm?' },
      { label: 'Tạo báo cáo', prompt: 'Tạo báo cáo ngắn kèm nguồn tham chiếu.' },
      { label: 'Hỏi tiếp', prompt: 'Đặt câu hỏi follow-up tốt nhất dựa trên các nguồn này.' }
    ]
  }

  if (selectedTarget.value === 'workspace') {
    return [
      { label: 'Hiệu suất tuần qua', prompt: 'Hãy đánh giá hiệu suất làm việc của toàn bộ các dự án trong tuần qua.' },
      { label: 'Dự án rủi ro?', prompt: 'Hiện tại có dự án nào đang gặp rủi ro hoặc chậm tiến độ không?' },
      { label: 'So sánh dự án', prompt: 'So sánh các dự án đang hoạt động dưới dạng bảng.' },
      { label: 'Báo cáo standup', prompt: 'Tạo báo cáo standup ngắn cho toàn workspace.' }
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

const visibleCurrentSuggestions = computed(() => currentSuggestions.value.slice(0, 3))
const hiddenCurrentSuggestionCount = computed(() => Math.max(0, currentSuggestions.value.length - visibleCurrentSuggestions.value.length))

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
const showSlashCommandsPopup = computed(() => showSlashCommands.value && filteredSlashCommands.value.length > 0)
const selectedAiModelOption = computed<AiModelOption | undefined>(() => {
  return AI_MODEL_OPTIONS.find(option => option.id === selectedAiModel.value) ?? AI_MODEL_OPTIONS[0]
})
const activeToolbarTab = computed<AnalyticsMiniTab | undefined>(() => {
  return cockpitDrawerOpen.value ? activeDrawerTab.value : undefined
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
const primaryComposerPlaceholder = computed(() => {
  return isCompactViewport.value ? 'Hỏi Erumi...' : 'Hỏi bất kỳ thứ gì... (gõ / để xem lệnh nhanh)'
})
const secondaryComposerPlaceholder = computed(() => {
  return isCompactViewport.value ? 'Hỏi Erumi...' : 'Nhập liệu... (gõ / để xem lệnh nhanh)'
})
const freshnessChipLabel = computed(() => {
  if (backgroundRefreshing.value) return 'Đang cập nhật'
  if (!lastRefreshedAt.value) return 'Fresh'
  return `Fresh ${lastRefreshedAt.value.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}`
})
const cockpitDrawerTitle = computed(() => {
  const titles: Record<AnalyticsMiniTab, string> = {
    insights: 'Insights',
    metrics: 'Metrics',
    risks: 'Risks',
    sources: 'Nguồn tham chiếu',
    report: 'Report',
    actions: 'Actions',
    model: 'Model registry',
    history: 'Lịch sử trò chuyện',
    settings: 'Cài đặt AI'
  }
  return titles[activeDrawerTab.value]
})

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
  if (isDropdownOpen1.value) setDropdownPlacement(1)
  if (isDropdownOpen2.value) setDropdownPlacement(2)
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

function handleQuickToolbarSelect(action: AiToolbarAction) {
  if (action.behavior === 'open-drawer') {
    openCockpitDrawer(action.tab)
    return
  }

  if (action.prompt) {
    fillComposer(action.prompt)
  }
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

function handleToolPaletteOpen(open: boolean) {
  toolPaletteOpen.value = open
}

function setDropdownPlacement(instance: number) {
  const root = instance === 1 ? dropdownRef1.value : dropdownRef2.value
  if (!root) return

  const rect = root.getBoundingClientRect()
  const viewportHeight = window.innerHeight || document.documentElement.clientHeight
  const spaceBelow = viewportHeight - rect.bottom
  const spaceAbove = rect.top
  const shouldOpenUp = spaceBelow < 240 && spaceAbove > spaceBelow
  const availableSpace = Math.max(160, Math.min(360, (shouldOpenUp ? spaceAbove : spaceBelow) - 18))

  if (instance === 1) {
    dropdownPlacement1.value = shouldOpenUp ? 'up' : 'down'
    dropdownMaxHeight1.value = `${availableSpace}px`
  } else {
    dropdownPlacement2.value = shouldOpenUp ? 'up' : 'down'
    dropdownMaxHeight2.value = `${availableSpace}px`
  }
}

function toggleProjectDropdown(instance: number) {
  const willOpen = instance === 1 ? !isDropdownOpen1.value : !isDropdownOpen2.value

  if (instance === 1) {
    isDropdownOpen1.value = willOpen
    isDropdownOpen2.value = false
  } else {
    isDropdownOpen2.value = willOpen
    isDropdownOpen1.value = false
  }

  if (willOpen) {
    nextTick(() => setDropdownPlacement(instance))
  }
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
    const fallbackText = getFallbackChatAnswer(userText)
    isChatting.value = false
    chatHistory.value[lastIdx] = {
      role: 'assistant',
      text: fallbackText,
      usedAi: false
    }
    updateLatestConversationSnippet(fallbackText)
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

function handleDocumentClick(e: MouseEvent) {
  if (dropdownRef1.value && !dropdownRef1.value.contains(e.target as Node)) {
    isDropdownOpen1.value = false
  }
  if (dropdownRef2.value && !dropdownRef2.value.contains(e.target as Node)) {
    isDropdownOpen2.value = false
  }
}

onMounted(() => {
  syncViewportFlag()
  loadConversationHistory()
  if (selectedProject.value) {
    selectedTarget.value = selectedProject.value.id
  }
  refreshAnalyticsContext()
  refreshTimer = window.setInterval(refreshAnalyticsContext, 30000)
  window.addEventListener('resize', syncViewportFlag)
  window.addEventListener('focus', refreshAnalyticsContext)
  window.addEventListener('click', handleDocumentClick)
  scrollToBottom()
})

onBeforeUnmount(() => {
  if (refreshTimer) window.clearInterval(refreshTimer)
  window.removeEventListener('resize', syncViewportFlag)
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
        <div v-if="!toolPaletteOpen" class="suggestion-pills-wrap">
          <button 
            v-for="(s, idx) in visibleOnboardingSuggestions"
            :key="idx"
            class="suggestion-pill"
            @click="submitChat(s.prompt)"
          >
            {{ s.label }}
          </button>
        </div>

        <AiQuickToolbar
          v-if="!isDrawer"
          :active-tab="activeToolbarTab"
          :history-count="conversationHistory.length"
          @select="handleQuickToolbarSelect"
          @palette-open-change="handleToolPaletteOpen"
        />

        <!-- Centered Composer -->
        <div class="composer-wrap-center">
          <div class="erumi-composer">
            <div class="erumi-project-row">
              <div class="erumi-context-controls">
                <div class="erumi-custom-dropdown" :class="{ 'is-open': isDropdownOpen1 }" ref="dropdownRef1" @click.stop="toggleProjectDropdown(1)">
                  <div class="erumi-project-trigger" :class="{ 'is-open': isDropdownOpen1 }">
                    <Folder :size="20" class="folder-icon" />
                    <span class="erumi-project-name">{{ selectedTargetLabel }}</span>
                    <ChevronDown :size="16" class="erumi-project-chevron" :class="{ 'rotate-180': isDropdownOpen1 }" />
                  </div>
                  <Transition name="dropdown-fade">
                    <div
                      class="erumi-dropdown-menu"
                      :class="{ 'opens-up': dropdownPlacement1 === 'up' }"
                      :style="{ maxHeight: dropdownMaxHeight1 }"
                      v-if="isDropdownOpen1"
                      @wheel.stop
                    >
                      <div class="erumi-dropdown-item" @click.stop="selectTarget('workspace', 1)" :class="{ active: selectedTarget === 'workspace' }">Tất cả dự án</div>
                      <div v-for="p in activeProjects" :key="p.id" class="erumi-dropdown-item" @click.stop="selectTarget(p.id, 1)" :class="{ active: selectedTarget === p.id }">
                        {{ p.name }}
                      </div>
                    </div>
                  </Transition>
                </div>
                <AiModelSelector
                  v-if="!isDrawer"
                  v-model="selectedAiModel"
                  :options="AI_MODEL_OPTIONS"
                  compact
                  @open-settings="openCockpitDrawer('model')"
                />
              </div>
              <span class="erumi-last-updated erumi-freshness-chip" v-if="!isDrawer">{{ freshnessChipLabel }}</span>
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
              <div v-if="showSlashCommandsPopup" class="slash-commands-popup">
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
                  :placeholder="primaryComposerPlaceholder"
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
                      <p class="erumi-draft-text">
                        {{ action.payload?.draftId && isDrawer ? 'Hành động ghi dữ liệu cần xác nhận của bạn để thực thi:' : 'Erumi đã chuẩn bị gợi ý hành động, nhưng pass này không thực thi hành động ghi dữ liệu trực tiếp trên /analytics.' }}
                      </p>
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

                <footer class="assistant-response-footer">
                  <AiAnswerMetaChips
                    :latency-ms="msg.latencyMs"
                    :confidence="msg.confidence"
                    :confidence-reason="msg.confidenceReason"
                    :freshness="msg.freshness"
                    :sources="msg.sources"
                    :used-ai="msg.usedAi"
                    :model-label="msg.model?.label"
                    @open-sources="openSourcesForMessage(msg)"
                  />

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
        <div v-if="!toolPaletteOpen" class="suggestion-chips-horizontal no-scrollbar">
          <button 
            v-for="(s, idx) in visibleCurrentSuggestions"
            :key="idx"
            class="suggestion-chip-small"
            @click="submitChat(s.prompt, (s as any).action)"
          >
            {{ s.label }}
          </button>
          <button
            v-if="hiddenCurrentSuggestionCount"
            type="button"
            class="suggestion-chip-small suggestion-chip-more"
            @click="fillComposer('/')"
          >
            Xem thêm
          </button>
        </div>

        <AiQuickToolbar
          v-if="!isDrawer"
          :active-tab="activeToolbarTab"
          :history-count="conversationHistory.length"
          @select="handleQuickToolbarSelect"
          @palette-open-change="handleToolPaletteOpen"
        />

        <!-- Premium Composer -->
        <div class="erumi-composer">
          <div class="erumi-project-row">
            <div class="erumi-context-controls">
              <div class="erumi-custom-dropdown" :class="{ 'is-open': isDropdownOpen2 }" ref="dropdownRef2" @click.stop="toggleProjectDropdown(2)">
                <div class="erumi-project-trigger" :class="{ 'is-open': isDropdownOpen2 }">
                  <Folder :size="20" class="folder-icon" />
                  <span class="erumi-project-name">{{ selectedTargetLabel }}</span>
                  <ChevronDown :size="16" class="erumi-project-chevron" :class="{ 'rotate-180': isDropdownOpen2 }" />
                </div>
                <Transition name="dropdown-fade">
                  <div
                    class="erumi-dropdown-menu"
                    :class="{ 'opens-up': dropdownPlacement2 === 'up' }"
                    :style="{ maxHeight: dropdownMaxHeight2 }"
                    v-if="isDropdownOpen2"
                  >
                    <div class="erumi-dropdown-item" @click.stop="selectTarget('workspace', 2)" :class="{ active: selectedTarget === 'workspace' }">Tất cả dự án</div>
                    <div v-for="p in activeProjects" :key="p.id" class="erumi-dropdown-item" @click.stop="selectTarget(p.id, 2)" :class="{ active: selectedTarget === p.id }">
                      {{ p.name }}
                    </div>
                  </div>
                </Transition>
              </div>
              <AiModelSelector
                v-if="!isDrawer"
                v-model="selectedAiModel"
                :options="AI_MODEL_OPTIONS"
                compact
                @open-settings="openCockpitDrawer('model')"
              />
            </div>
            <span class="erumi-last-updated erumi-freshness-chip" v-if="!isDrawer">{{ freshnessChipLabel }}</span>
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
            <div v-if="showSlashCommandsPopup" class="slash-commands-popup">
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
                :placeholder="secondaryComposerPlaceholder"
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

    <AnalyticsSideDrawer
      v-if="!isDrawer"
      :open="cockpitDrawerOpen"
      :title="cockpitDrawerTitle"
      :subtitle="selectedAiModelCompactLabel"
      @close="closeCockpitDrawer"
    >
      <div class="analytics-drawer-content">
        <SourceRefsDrawer
          v-if="activeDrawerTab === 'sources'"
          :sources="drawerSources"
          :source-refs="drawerSourceRefs"
          @ask-source="askAboutSource"
        />

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
          <p class="analytics-drawer-muted">Registry này chỉ điều khiển UI. Provider planned chưa được kích hoạt live.</p>
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
            <span>Dùng toolbar Actions để điền prompt tạo đề xuất tiếp theo.</span>
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
            <span>Chọn prompt từ toolbar hoặc hỏi Erumi trực tiếp trong composer.</span>
          </div>
        </section>
      </div>
    </AnalyticsSideDrawer>
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  max-width: 100%;
  background: #f7f9fc;
  position: relative;
  overflow: hidden;
  font-family: 'Inter', sans-serif;
}

.analytics-chat-portal,
.analytics-chat-portal * {
  box-sizing: border-box;
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
  min-width: 0;
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
  min-width: 0;
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
  border-radius: var(--qaly-radius-lg);
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
  position: relative;
  z-index: 20;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  min-width: 0;
  padding: 0 4px;
  margin-bottom: 2px;
}

.erumi-context-controls {
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.erumi-custom-dropdown {
  position: relative;
  z-index: 1;
  flex: 1 1 auto;
  min-width: 0;
  max-width: min(340px, 48vw);
}

.erumi-custom-dropdown.is-open {
  z-index: 1600;
}

.erumi-project-trigger {
  position: relative;
  min-width: 0;
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  color: #0f172a;
  cursor: pointer;
  padding: 0 10px;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  transition: all 0.25s ease;
  border: 1px solid #dbeafe;
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
  max-width: 100%;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #0f172a;
  font-size: 13px;
  font-weight: 850;
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
  width: min(360px, calc(100vw - 32px));
  min-width: 260px;
  max-width: calc(100vw - 32px);
  height: auto;
  max-height: 320px;
  overflow-y: auto;
  overflow-x: hidden;
  overscroll-behavior: contain;
  background: rgba(255, 255, 255, 0.98);
  border: 1px solid rgba(15, 23, 42, 0.08);
  border-radius: var(--qaly-radius-lg);
  box-shadow: var(--qaly-shadow-md);
  padding: 8px;
  z-index: 1601;
  display: block;
  scrollbar-width: none;
  -ms-overflow-style: none;
  scroll-behavior: smooth;
}

.erumi-dropdown-menu.opens-up {
  top: auto;
  bottom: calc(100% + 8px);
}

.erumi-dropdown-menu::-webkit-scrollbar {
  width: 0;
  height: 0;
  display: none;
}

.erumi-dropdown-item {
  padding: 11px 14px;
  border-radius: var(--qaly-radius-lg);
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
  min-width: 0;
  max-width: 128px;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 0 0 auto;
  color: #8a8f98;
  font-size: 12px;
  font-weight: 800;
  white-space: nowrap;
}

.erumi-freshness-chip {
  height: 30px;
  display: inline-flex;
  align-items: center;
  border: 1px solid #dbeafe;
  border-radius: var(--qaly-radius-lg);
  background: #ffffff;
  color: #335274;
  padding: 0 9px;
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
  border-radius: var(--qaly-radius-lg);
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
  box-shadow: var(--qaly-shadow-md);
  transition:
    box-shadow 0.25s ease,
    transform 0.25s ease,
    background 0.25s ease;
}

.erumi-input-shell:focus-within {
  border: none;
  outline: none;
  background: #ffffff;
  box-shadow: var(--qaly-shadow-md);
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
  box-shadow: var(--qaly-shadow-md);
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
  border-radius: var(--qaly-radius-lg);
  box-shadow: var(--qaly-shadow-md);
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
  border-radius: var(--qaly-radius-lg);
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
  padding: 32px 24px 190px;
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
  border-radius: var(--qaly-radius-lg);
  background: linear-gradient(145deg, #ffffff, #edf4ff);
  box-shadow: var(--qaly-shadow-md);
}

.msg-avatar-col :deep(.chatbot-avatar) {
  width: 38px !important;
  height: 38px !important;
  border-radius: var(--qaly-radius-lg);
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
  border-radius: 6px 6px 2px 6px;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.5;
  box-shadow: var(--qaly-shadow-md);
  overflow-wrap: anywhere;
}

.msg-bubble-assistant {
  width: 100%;
  background: #ffffff;
  color: #0f172a;
  border-radius: var(--qaly-radius-lg);
  font-size: 14px;
  line-height: 1.7;
  border: 1px solid #dfe7f3;
  box-shadow: var(--qaly-shadow-md);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
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
  padding: 10px 16px 16px;
  z-index: 10;
}

.bottom-bar-width {
  width: 100%;
  max-width: 640px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.suggestion-chips-horizontal {
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding: 0 0 4px;
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
  border-radius: var(--qaly-radius-lg);
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

.suggestion-chip-more {
  border-color: #b9d5ff;
  color: #185fb8;
  background: #f5f9ff;
}

.chat-disclaimer {
  font-size: 11px;
  color: #94a3b8;
  text-align: center;
  margin: 0;
}

.analytics-drawer-content,
.analytics-drawer-section {
  min-width: 0;
  display: grid;
  gap: 12px;
}

.analytics-drawer-muted,
.analytics-drawer-empty span,
.analytics-drawer-table-note span,
.analytics-model-registry p {
  margin: 0;
  color: #64748b;
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
  border: 1px solid #e2e8f0;
  border-radius: var(--qaly-radius-lg);
  background: #f8fafc;
  padding: 12px;
}

.analytics-drawer-metric {
  display: grid;
  gap: 4px;
}

.analytics-drawer-metric span,
.analytics-drawer-metric small,
.analytics-model-registry span {
  color: #64748b;
  font-size: 11px;
  font-weight: 800;
}

.analytics-drawer-metric strong,
.analytics-drawer-empty strong,
.analytics-drawer-table-note strong,
.analytics-model-registry strong {
  min-width: 0;
  overflow: hidden;
  color: #0f172a;
  font-size: 14px;
  font-weight: 900;
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
  background: #eef4ff;
  color: #1d4ed8;
  padding: 2px 7px;
}

.analytics-action-draft {
  width: 100%;
  color: #1d4ed8;
  text-align: left;
  font-size: 13px;
  font-weight: 850;
  cursor: pointer;
}

.analytics-action-draft:hover,
.analytics-action-draft:focus-visible {
  border-color: #bfdbfe;
  background: #eff6ff;
  outline: none;
}

.erumi-draft-note {
  margin: 8px 0 0;
  color: #64748b;
  font-size: 12px;
  line-height: 1.45;
}

/* Drawer mode overrides (Sprint 1) */
.is-drawer-mode {
  border-left: 1px solid #e2e8f0;
  box-shadow: var(--qaly-shadow-md);
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
  border-radius: var(--qaly-radius-lg);
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
  border-radius: var(--qaly-radius-lg);
}

.is-drawer-mode .msg-avatar-col :deep(.chatbot-avatar) {
  width: 32px !important;
  height: 32px !important;
  border-radius: var(--qaly-radius-lg);
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

:global(:root[data-theme='dark']) .analytics-drawer-metric,
:global(:root[data-theme='dark']) .analytics-model-registry article,
:global(:root[data-theme='dark']) .analytics-drawer-empty,
:global(:root[data-theme='dark']) .analytics-drawer-table-note,
:global(:root[data-theme='dark']) .analytics-action-draft {
  border-color: var(--line) !important;
  background: var(--panel-soft) !important;
  color: var(--text) !important;
}

:global(:root[data-theme='dark']) .analytics-drawer-metric strong,
:global(:root[data-theme='dark']) .analytics-drawer-empty strong,
:global(:root[data-theme='dark']) .analytics-drawer-table-note strong,
:global(:root[data-theme='dark']) .analytics-model-registry strong {
  color: var(--text-strong) !important;
}

:global(:root[data-theme='dark']) .analytics-drawer-muted,
:global(:root[data-theme='dark']) .analytics-drawer-metric span,
:global(:root[data-theme='dark']) .analytics-drawer-metric small,
:global(:root[data-theme='dark']) .analytics-drawer-empty span,
:global(:root[data-theme='dark']) .analytics-drawer-table-note span,
:global(:root[data-theme='dark']) .analytics-model-registry p,
:global(:root[data-theme='dark']) .erumi-draft-note {
  color: var(--muted) !important;
}

@media (max-width: 720px) {
  .chat-empty-state {
    max-width: 100%;
    padding: 18px 12px 16px;
    gap: 14px;
  }

  .welcome-heading {
    font-size: 24px;
  }

  .suggestion-pills-wrap {
    max-width: 100%;
    align-self: stretch;
    flex-wrap: nowrap;
    justify-content: flex-start;
    overflow-x: auto;
    padding-bottom: 2px;
    scrollbar-width: none;
  }

  .suggestion-pills-wrap::-webkit-scrollbar {
    display: none;
  }

  .suggestion-pill {
    flex: 0 0 auto;
    padding: 9px 13px;
  }

  .erumi-project-row {
    align-items: flex-start;
    gap: 8px;
    flex-wrap: wrap;
  }

  .erumi-context-controls {
    width: 100%;
    flex-wrap: wrap;
  }

  .erumi-custom-dropdown {
    flex: 1 1 190px;
    max-width: 100%;
  }

  .erumi-project-trigger {
    width: 100%;
    padding: 8px 10px;
  }

  .erumi-project-name {
    max-width: 100%;
    font-size: 14px;
  }

  .erumi-last-updated {
    max-width: 100%;
    font-size: 12px;
  }

  .erumi-freshness-chip {
    max-width: 100%;
  }

  .chat-thread-container {
    padding: 20px 12px 220px;
  }

  .chat-thread-width {
    gap: 16px;
  }

  .msg-avatar-col {
    width: 34px;
    height: 34px;
    border-radius: var(--qaly-radius-lg);
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
