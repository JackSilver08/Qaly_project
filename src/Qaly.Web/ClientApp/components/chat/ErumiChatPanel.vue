<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
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
  X,
  ArrowRight
} from 'lucide-vue-next'
import { useDashboardContext } from '../../composables/dashboard-context'
import { useErumiContext } from '../../composables/use-erumi-context'
import { apiJson, apiResult } from '../../utils/api-client'
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
  externalPrompt?: string
  externalPromptToken?: number
  externalHistoryToken?: number
  externalProjectId?: string | null
}>(), {
  isDrawer: false,
  externalPrompt: '',
  externalPromptToken: 0,
  externalHistoryToken: 0,
  externalProjectId: null,
})

const emit = defineEmits<{
  composeAction: [payload: { message: string; projectId: string }]
}>()

const { projects, selectedProject, currentUser, loadDashboard } = useDashboardContext()
const erumiContext = useErumiContext()
const route = useRoute()
const router = useRouter()

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

type AiAssistantProcessEvent = {
  sequence: number
  stage: string
  status: 'queued' | 'running' | 'completed' | 'failed' | string
  publicLabel: string
  startedAt: string
  completedAt?: string | null
  durationMs?: number | null
  retryable?: boolean
  safeErrorCode?: string | null
  stepId?: string | null
  attempt?: number | null
  sourceRefs?: string[] | null
  actualProvider?: string | null
  actualModel?: string | null
}

type AiAssistantQuickReply = { value: string; label: string; description?: string | null }
type AiAssistantConversationQuestion = {
  id: string
  text: string
  blocking: boolean
  reason: string
  quickReplies: AiAssistantQuickReply[]
  allowFreeText: boolean
}
type AiAssistantProgressiveReply = { questionId: string; value: string; label?: string | null }
type AiAssistantClarificationDraft = {
  originTurnId: string
  originalMessage: string
  requestedCapabilityId?: string | null
  questions: AiAssistantConversationQuestion[]
  answers: AiAssistantProgressiveReply[]
  updatedAt: string
}
type AiAssistantManualGuidance = {
  schemaId: 'assistant_manual_guidance.v1'
  temporary: boolean
  summary: string
  steps: Array<{ sequence: number; label: string; route: string; requiredPermission: string }>
}
type AiAssistantConversationTurn = {
  schemaId: 'assistant_conversation_turn.v2'
  conversationDisposition: string
  actionDisposition: string
  answer: string
  questions: AiAssistantConversationQuestion[]
  guidance?: AiAssistantManualGuidance | null
  capabilityGap?: { capabilityId: string; executionUnavailable: boolean; userMessage: string; internalReasonCode: string } | null
  proposedActions: string[]
  sources: string[]
  confidence: number
  actualProvider: string
  actualModel: string
  promptVersion: string
}
type ProjectLaunchBrief = {
  briefId: string
  schemaId: 'project_launch_brief.v1'
  revision: number
  state: string
  organizationId: string
  organizationName: string
  objective: string
  proposedProjectName: string
  scope: string[]
  exclusions: string[]
  successMeasures: string[]
  facts: string[]
  assumptions: string[]
  unknowns: string[]
  questions: AiAssistantConversationQuestion[]
  rulebookStatus: string
  ruleSetId?: string | null
  ruleSetVersion?: number | null
  ruleDecisions: Array<{ ruleKey: string; result: string; severity: string; explanation: string }>
  sourceRefs: string[]
  actualProvider: string
  actualModel: string
  promptVersion: string
  createdAt: string
}
type OrganizationWorkRuleSet = {
  ruleSetId: string
  organizationId: string
  version: number
  status: string
  revision: number
}

type ProjectStaffingMember = {
  userId: string
  displayName: string
  proposedRole: string
  proposedHours: number
  coveredSkills: string[]
  missingSkills: string[]
  loadAfterPercent: number
  decisionReasons: string[]
}

type ProjectStaffingScenario = {
  scenarioId: string
  title: string
  description: string
  feasible: boolean
  score: number
  managerUserId?: string | null
  managerName?: string | null
  members: ProjectStaffingMember[]
  managerCandidates: Array<{ userId: string; displayName: string; hardRejects: string[]; loadAfterPercent: number; capacityState: string }>
  missingSkills: string[]
  blockingReasons: string[]
  risks: string[]
  assumptions: string[]
  ruleDecisions: Array<{ ruleKey: string; result: string; severity: string; explanation: string }>
  sourceRefs: string[]
  scoringVersion: string
}

type ProjectLaunchTaskPlan = {
  clientId: string
  title: string
  priority: string
  estimatedHours: number
  proposedAssigneeId?: string | null
  requiredSkillNames: string[]
  dependencyClientIds: string[]
  selected: boolean
}

type ProjectLaunchDeliveryPlan = {
  proposedProjectName: string
  proposedProjectCode: string
  objective: string
  startDate: string
  endDate: string
  scope: string[]
  skillGaps: string[]
  scheduleRisks: string[]
  externalDeferred: string[]
  sprints: Array<{ clientId: string; name: string; objective: string; startDate: string; endDate: string; selected: boolean; tasks: ProjectLaunchTaskPlan[] }>
}

type ProjectLaunchExecutionReceipt = {
  receiptId: string
  state: string
  projectId: string
  commands: Array<{ commandId: string; adapterId: string; status: string; summary: string; deepLink?: string | null }>
  createdEntityLinks: string[]
  deferredExternalActions: string[]
  readBackVerified: boolean
  internalTransactionCommitted: boolean
  rollbackAvailable: boolean
  rollbackBlockReason?: string | null
  executedAt: string
  revision: number
}

type ProjectReplanProposal = {
  proposalId: string
  revision: number
  state: string
  changes: Array<{ changeType: string; severity: string; summary: string; baselineValue: string; currentValue: string; suggestedAction: string }>
  triggerCodes: string[]
  requiresConfirmation: boolean
  createdAt: string
}

type ProjectLaunchPlan = {
  planId: string
  schemaId: 'project_launch_plan.v1'
  revision: number
  state: string
  organizationId: string
  organizationName: string
  ruleSetVersion?: number | null
  scoringVersion: string
  staffingScenarios: ProjectStaffingScenario[]
  selectedScenarioId?: string | null
  deliveryPlan: ProjectLaunchDeliveryPlan
  blockingReasons: string[]
  warnings: string[]
  actualProvider: string
  actualModel: string
  promptVersion: string
  rowRevision: number
  executionReceipt?: ProjectLaunchExecutionReceipt | null
  latestReplanProposal?: ProjectReplanProposal | null
}

type AiAssistantCapabilityDescriptor = {
  capabilityId: string
  kind: string
  inputSchemaId: string
  outputSchemaId: string
  requiredScopes: string[]
  contextSources: string[]
  riskClass: string
  confirmationPolicy: string
  modelProfile: string
  rendererId: string
  featureFlag: string
  version?: string
  title?: string
  description?: string
  userJobs?: string[] | null
  entityTypes?: string[] | null
}

type AiAssistantGoalScope = {
  scopeType: string
  projectId?: string | null
  entityType?: string | null
  entityId?: string | null
  label: string
  confidence: number
  reason: string
}

type AiAssistantGoalAnalysis = {
  schemaId: 'assistant_goal_analysis.v1'
  objective: string
  userJob: string
  intentFacets: string[]
  scopes: AiAssistantGoalScope[]
  selectedSkills: Array<{ skillId: string; title: string; fitReason: string; confidence: number; riskClass: string; confirmationPolicy: string }>
  missingSkills: Array<{ skillId: string; title: string; reason: string; suggestedPath: string }>
  disposition: 'answerable' | 'plannable' | 'clarification_required' | 'unsupported_but_analyzed' | 'policy_blocked'
  confidence: number
  warnings: string[]
  actualProvider: string
  actualModel: string
  usedFallback: boolean
}

type AiAssistantWorkPlan = {
  schemaId: 'assistant_work_plan.v1'
  objective: string
  scope: AiAssistantGoalScope
  selectedSkillIds: string[]
  steps: Array<{ stepId: string; kind: string; publicLabel: string; skillId?: string | null; dependencyIds: string[]; verificationIds: string[]; mutationClass: string; state: string }>
  requiresPlanApproval: boolean
}

type AiAssistantSourceDisclosure = {
  sourceId: string
  status: 'read' | 'skipped' | 'denied' | string
  label: string
  sourceRef?: string | null
  reasonCode?: string | null
}

type AiSafeTestRunEvent = {
  sequence: number
  suiteId: string
  status: string
  publicLabel: string
  startedAt: string
  completedAt?: string | null
  exitCode?: number | null
  safeErrorCode?: string | null
}
type AiSafeTestRunPreview = {
  schemaId: 'safe_test_run_preview.v1'
  runId: string
  manifestId: string
  title: string
  status: string
  requiresConfirmation: boolean
  estimatedSeconds: number
  estimatedExternalCost: number
  suites: Array<{ id: string; label: string; project: string; scope: string; estimatedSeconds: number }>
  revision: number
}
type AiSafeTestRunReport = {
  schemaId: 'safe_test_run_report.v1'
  runId: string
  manifestId: string
  status: string
  events: AiSafeTestRunEvent[]
  passedSuites: number
  failedSuites: number
  startedAt?: string | null
  completedAt?: string | null
  safeSummary?: string | null
  safeErrorCode?: string | null
  revision: number
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
  processEvents?: AiAssistantProcessEvent[] | null
  capabilities?: AiAssistantCapabilityDescriptor[] | null
  sourceDisclosures?: AiAssistantSourceDisclosure[] | null
  researchPlan?: AiAssistantResearchPlan | null
  goalAnalysis?: AiAssistantGoalAnalysis | null
  workPlan?: AiAssistantWorkPlan | null
  conversation?: AiAssistantConversationTurn | null
  projectLaunchBrief?: ProjectLaunchBrief | null
  projectLaunchPlan?: ProjectLaunchPlan | null
  safeTestRunPreview?: AiSafeTestRunPreview | null
  safeTestRunReport?: AiSafeTestRunReport | null
}

type AiAssistantChoice = {
  id: string
  label: string
  description?: string | null
}

type AiAssistantClarification = {
  questionId: string
  field: string
  prompt: string
  choices: AiAssistantChoice[]
  allowFreeText: boolean
  turn: number
  maxTurns: number
}

type AiAssistantArtifact = {
  kind: 'task_action_plan' | string
  schemaId: string
  message: string
  projectId: string
}

type AiAssistantResearchFinding = {
  findingId: string
  statement: string
  severity: 'info' | 'low' | 'medium' | 'high' | 'critical'
  confidence: number
  sourceRefs: string[]
}

type AiAssistantResearchUnknown = {
  unknownId: string
  question: string
  blocking: boolean
}

type AiAssistantResearchOption = {
  optionId: string
  title: string
  outcome: string
  tradeOffs: string[]
  estimatedEffort: string
  risk: string
}

type AiAssistantResearchAction = {
  actionId: string
  capabilityId: string
  title: string
  dependencyIds: string[]
  draftInput: Record<string, unknown>
  sourceRefs: string[]
  executionEligible: boolean
  eligibilityReason: string
}

type AiAssistantResearchPlan = {
  schemaId: 'assistant_research_plan.v1'
  promptId: string
  promptVersion: string
  objective: string
  scope: { scopeType: string; projectId?: string | null; label: string; sourceRefs: string[] }
  findings: AiAssistantResearchFinding[]
  unknowns: AiAssistantResearchUnknown[]
  assumptions: string[]
  options: AiAssistantResearchOption[]
  recommendedOptionId: string
  recommendationRationale: string
  proposedActions: AiAssistantResearchAction[]
  warnings: string[]
  privacyNotes: string[]
  freshnessAt: string
  generatedAt: string
  actualProvider: string
  actualModel: string
}

type AiAssistantTurnResponse = {
  schemaId: 'assistant_turn.v1'
  disposition: 'grounded_answer' | 'guided_answer' | 'research_plan' | 'project_launch_brief' | 'registered_action' | 'clarification_required' | 'unsupported' | 'unsupported_but_analyzed' | 'policy_blocked' | 'draft_ready'
  intent: string
  executionPolicy: string
  assistantMessage: string
  confidence: number
  clarification?: AiAssistantClarification | null
  artifact?: AiAssistantArtifact | null
  sourceRefs: string[]
  answer?: ErumiChatResponse | null
  sessionId?: string | null
  turnId?: string | null
  sequence?: number | null
  sessionVersion?: number | null
  clientTurnId?: string | null
  turnStatus?: string
  correlationId?: string | null
  replayed?: boolean
  modelProfile?: string
  actualProvider?: string
  actualModel?: string
  processEvents?: AiAssistantProcessEvent[] | null
  capabilities?: AiAssistantCapabilityDescriptor[] | null
  sourceDisclosures?: AiAssistantSourceDisclosure[] | null
  researchPlan?: AiAssistantResearchPlan | null
  goalAnalysis?: AiAssistantGoalAnalysis | null
  workPlan?: AiAssistantWorkPlan | null
  conversation?: AiAssistantConversationTurn | null
  projectLaunchBrief?: ProjectLaunchBrief | null
  projectLaunchPlan?: ProjectLaunchPlan | null
  safeTestRunPreview?: AiSafeTestRunPreview | null
  safeTestRunReport?: AiSafeTestRunReport | null
}

type AiAssistantStoredTurn = {
  turnId: string
  sequence: number
  clientTurnId: string
  userMessage: string
  status: string
  correlationId: string
  createdAt: string
  completedAt?: string | null
  response?: AiAssistantTurnResponse | null
  processEvents: AiAssistantProcessEvent[]
}

type AiAssistantSession = {
  sessionId: string
  title: string
  status: string
  version: number
  projectId?: string | null
  createdAt: string
  updatedAt?: string | null
  turns: AiAssistantStoredTurn[]
  clarificationDraft?: AiAssistantClarificationDraft | null
}
type AiAssistantSessionSummary = {
  sessionId: string
  title: string
  status: string
  version: number
  projectId?: string | null
  createdAt: string
  updatedAt?: string | null
  archivedAt?: string | null
  turnCount: number
  lastMessage?: string | null
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
  processEvents?: AiAssistantProcessEvent[] | null
  capabilities?: AiAssistantCapabilityDescriptor[] | null
  sourceDisclosures?: AiAssistantSourceDisclosure[] | null
  researchPlan?: AiAssistantResearchPlan | null
  goalAnalysis?: AiAssistantGoalAnalysis | null
  workPlan?: AiAssistantWorkPlan | null
  conversation?: AiAssistantConversationTurn | null
  projectLaunchBrief?: ProjectLaunchBrief | null
  projectLaunchPlan?: ProjectLaunchPlan | null
  safeTestRunPreview?: AiSafeTestRunPreview | null
  safeTestRunReport?: AiSafeTestRunReport | null
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
const launchActionBusy = ref<string | null>(null)
const safeTestActionBusy = ref<string | null>(null)
const selectedLaunchScenarios = ref<Record<string, string>>({})
const rulebookDrafts = ref<Record<string, OrganizationWorkRuleSet>>({})
const activeAssistantTurnId = ref<string | null>(null)
const activeAssistantClientTurnId = ref<string | null>(null)
const progressiveDraft = ref<AiAssistantClarificationDraft | null>(null)
const clarificationDraftSaving = ref(false)
const selectedFiles = ref<File[]>([])
const backgroundRefreshing = ref(false)
const lastRefreshedAt = ref<Date | null>(null)
const chatContainerRef = ref<HTMLElement | null>(null)
const textareaRef = ref<HTMLTextAreaElement | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
let refreshTimer: number | undefined
let keepConversationForNextTargetChange = false

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

const AI_MODEL_STORAGE_KEY = 'qaly.ai-native.model.v1'
const AI_ACTIVE_SESSION_STORAGE_KEY = 'qaly.ai-native.active-session.v1'
const storedAiModel = window.localStorage.getItem(AI_MODEL_STORAGE_KEY)
const selectedAiModel = ref(AI_MODEL_OPTIONS.some(option => option.id === storedAiModel && !option.disabled)
  ? storedAiModel!
  : 'deepseek-chat')
const selectedProviderHint = computed(() => {
  switch (selectedAiModel.value) {
    case 'deepseek-chat':
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
const conversationHistoryLoading = ref(false)
const assistantSessionId = ref<string | null>(null)
const assistantSessionVersion = ref(0)
const assistantSessionLoading = ref(false)
const assistantSessionLoadAttempted = ref(false)

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
// Default target is always workspace (Tất cả dự án)
selectedTarget.value = 'workspace'

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
  return isCompactViewport.value
    ? 'Bạn muốn Qaly giúp gì?'
    : 'Mô tả điều bạn muốn phân tích hoặc thực hiện... (gõ / để xem lệnh nhanh)'
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
    history: 'Lịch sử phiên Trợ lý AI',
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
      void openSessionHistory()
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

async function startNewConversation() {
  chatHistory.value = [buildWelcomeMessage()]
  selectedDrawerMessage.value = null
  closeCockpitDrawer()
  assistantSessionId.value = null
  assistantSessionVersion.value = 0
  assistantSessionLoadAttempted.value = true
  try {
    await createAssistantSession()
    await loadConversationHistory()
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể tạo phiên Trợ lý AI mới.')
  }
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

function openComposerAction(action: ErumiAction) {
  const message = String(action.payload?.message || '').trim()
  const projectId = String(action.payload?.projectId || '').trim()
  if (!message || !projectId) return
  emit('composeAction', { message, projectId })
}

function openResearchAction(action: AiAssistantResearchAction) {
  if (!action.executionEligible || action.capabilityId !== 'task.create.v1') return
  const message = String(action.draftInput?.message || '').trim()
  const projectId = String(action.draftInput?.projectId || '').trim()
  if (!message || !projectId) {
    showError('Action này thiếu project hoặc nội dung bản nháp nên chưa thể mở Task Composer.')
    return
  }
  emit('composeAction', { message, projectId })
}

function compactSourceRef(sourceRef: string) {
  const clean = String(sourceRef || '')
  return clean.length > 54 ? `${clean.slice(0, 28)}…${clean.slice(-20)}` : clean
}

function goalDispositionLabel(disposition: AiAssistantGoalAnalysis['disposition']) {
  if (disposition === 'unsupported_but_analyzed') return 'Có thể tư vấn · chưa thể tự thao tác'
  if (disposition === 'policy_blocked') return 'Có thể tư vấn · thao tác bị giới hạn quyền'
  if (disposition === 'clarification_required') return 'Cần làm rõ'
  if (disposition === 'answerable') return 'Có thể trả lời'
  return 'Có thể lập phương án'
}

async function answerProjectClarification(choice: AiAssistantChoice, action: ErumiAction) {
  const originalMessage = String(action.payload?.originalMessage || '').trim()
  if (!originalMessage || !choice.id) return
  keepConversationForNextTargetChange = true
  selectedTarget.value = choice.id
  await nextTick()
  await submitChat(originalMessage, undefined, choice.label)
}

let clarificationSaveChain: Promise<void> = Promise.resolve()
let clarificationInputSaveTimer: number | null = null

function ensureProgressiveDraft(action: ErumiAction) {
  const originTurnId = String(action.payload?.originTurnId || '')
  if (!originTurnId) return null
  if (progressiveDraft.value?.originTurnId === originTurnId) return progressiveDraft.value
  progressiveDraft.value = {
    originTurnId,
    originalMessage: String(action.payload?.originalMessage || ''),
    requestedCapabilityId: String(action.payload?.requestedCapabilityId || '') || null,
    questions: (action.payload?.questions || []) as AiAssistantConversationQuestion[],
    answers: [],
    updatedAt: new Date().toISOString()
  }
  return progressiveDraft.value
}

function progressiveAnswer(questionId: string, action: ErumiAction) {
  const draft = progressiveDraft.value?.originTurnId === String(action.payload?.originTurnId || '')
    ? progressiveDraft.value
    : null
  return draft?.answers.find(answer => answer.questionId === questionId) ?? null
}

function hasAllBlockingAnswers(action: ErumiAction) {
  const draft = progressiveDraft.value
  if (!draft || draft.originTurnId !== String(action.payload?.originTurnId || '')) return false
  const answered = new Set(draft.answers.filter(answer => answer.value.trim()).map(answer => answer.questionId))
  return draft.questions.filter(question => question.blocking).every(question => answered.has(question.id))
}

function isQuestionAnsweredInHistory(questionId: string): boolean {
  return chatHistory.value.some(msg => {
    const text = msg.text || ''
    if (questionId === 'launch.deadline' && (text.includes('tuần') || text.includes('ngày') || text.includes('tháng') || text.includes('6_weeks') || text.includes('8_weeks') || text.includes('12_weeks'))) return true
    if (questionId === 'launch.audience' && (text.includes('nội bộ') || text.includes('khách hàng') || text.includes('người dùng') || text.includes('Nội bộ') || text.includes('Khách hàng'))) return true
    if (questionId === 'launch.scope' && (text.includes('trang chủ') || text.includes('thanh toán') || text.includes('quản lý') || text.includes('chức năng'))) return true
    return false
  })
}

function filteredProgressiveQuestions(questions: AiAssistantConversationQuestion[] = [], action: ErumiAction): AiAssistantConversationQuestion[] {
  return questions.filter(question => !isQuestionAnsweredInHistory(question.id))
}

function pendingProgressiveQuestions(questions: AiAssistantConversationQuestion[] = [], action: ErumiAction) {
  return filteredProgressiveQuestions(questions, action)
    .filter(question => !progressiveAnswer(question.id, action)?.value.trim())
}

function queueClarificationDraftSave() {
  clarificationSaveChain = clarificationSaveChain.catch(() => undefined).then(async () => {
    const draft = progressiveDraft.value
    if (!draft || !assistantSessionId.value) return
    const sentUpdatedAt = draft.updatedAt
    const sentAnswersJson = JSON.stringify(draft.answers)
    clarificationDraftSaving.value = true
    const session = await apiJson<AiAssistantSession>(
      `/api/ai/assistant/sessions/${assistantSessionId.value}/clarification-draft`,
      {
        method: 'PUT',
        body: JSON.stringify({
          expectedVersion: assistantSessionVersion.value,
          originTurnId: draft.originTurnId,
          originalMessage: draft.originalMessage,
          requestedCapabilityId: draft.requestedCapabilityId,
          questions: draft.questions,
          answers: draft.answers
        })
      }
    )
    assistantSessionVersion.value = session.version
    const latestLocal = progressiveDraft.value
    const serverDraft = session.clarificationDraft ?? draft
    progressiveDraft.value = latestLocal?.originTurnId === draft.originTurnId &&
      (latestLocal.updatedAt !== sentUpdatedAt || JSON.stringify(latestLocal.answers) !== sentAnswersJson)
      ? {
          ...serverDraft,
          questions: latestLocal.questions,
          answers: latestLocal.answers,
          updatedAt: latestLocal.updatedAt,
        }
      : serverDraft
  }).catch(error => {
    showError(error instanceof Error ? error.message : 'Không thể lưu câu trả lời nháp.')
  }).finally(() => {
    clarificationDraftSaving.value = false
  })
}

function setProgressiveAnswer(
  question: AiAssistantConversationQuestion,
  value: string,
  label: string | null,
  action: ErumiAction,
  persist = true
) {
  const draft = ensureProgressiveDraft(action)
  if (!draft) return
  const normalized = persist ? value.trim() : value
  draft.answers = draft.answers.filter(answer => answer.questionId !== question.id)
  if (normalized.trim()) draft.answers.push({
    questionId: question.id,
    value: normalized,
    label: label || normalized.trim()
  })
  draft.updatedAt = new Date().toISOString()
  if (persist) queueClarificationDraftSave()
}

function answerProgressiveQuestion(
  question: AiAssistantConversationQuestion,
  reply: AiAssistantQuickReply,
  action: ErumiAction
) {
  setProgressiveAnswer(question, reply.value, reply.label, action)
}

function answerProgressiveWithComposer(question: AiAssistantConversationQuestion, action: ErumiAction) {
  ensureProgressiveDraft(action)
  nextTick(() => document.getElementById(`clarification-${question.id}`)?.focus())
}

function updateProgressiveFreeText(
  question: AiAssistantConversationQuestion,
  action: ErumiAction,
  event: Event,
  persist: boolean
) {
  setProgressiveAnswer(question, (event.target as HTMLInputElement).value, null, action, persist)
  if (persist && clarificationInputSaveTimer != null) {
    window.clearTimeout(clarificationInputSaveTimer)
    clarificationInputSaveTimer = null
  } else if (!persist) {
    if (clarificationInputSaveTimer != null) window.clearTimeout(clarificationInputSaveTimer)
    clarificationInputSaveTimer = window.setTimeout(() => {
      clarificationInputSaveTimer = null
      queueClarificationDraftSave()
    }, 400)
  }
}

async function clearProgressiveDraft() {
  if (!assistantSessionId.value || !progressiveDraft.value) return
  await clarificationSaveChain.catch(() => undefined)
  try {
    const session = await apiJson<AiAssistantSession>(
      `/api/ai/assistant/sessions/${assistantSessionId.value}/clarification-draft?expectedVersion=${assistantSessionVersion.value}`,
      { method: 'DELETE' }
    )
    assistantSessionVersion.value = session.version
    progressiveDraft.value = null
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể xóa câu trả lời nháp.')
  }
}

async function submitProgressiveDraft(action: ErumiAction) {
  const draft = ensureProgressiveDraft(action)
  if (!draft || !hasAllBlockingAnswers(action)) return
  await clarificationSaveChain.catch(() => undefined)
  const answerSummary = draft.answers.map(answer => answer.label || answer.value).join(' · ')
  const completed = await submitChat(
    draft.originalMessage,
    undefined,
    answerSummary,
    undefined,
    draft.requestedCapabilityId || undefined,
    undefined,
    draft.answers
  )
  if (completed) await clearProgressiveDraft()
}

function openGuidanceRoute(routePath: string) {
  const allowed = ['/dashboard', '/projects', '/tasks', '/teams', '/groups', '/analytics', '/organizations', '/settings']
  if (!allowed.includes(routePath)) return
  router.push(routePath)
}

function openAssistantNavigation(action: ErumiAction) {
  const routePath = String(action.payload?.route || '').trim()
  openGuidanceRoute(routePath)
}

function mapAssistantTurn(turn: AiAssistantTurnResponse, originalMessage?: string): ErumiChatResponse {
  const answer = turn.answer
  const actions = [...(answer?.actions ?? [])]

  if (turn.disposition === 'registered_action' && turn.artifact) {
    actions.push({
      type: 'compose_task_plan',
      label: 'Đang soạn phương án task',
      requiresConfirmation: false,
      payload: {
        message: turn.artifact.message,
        projectId: turn.artifact.projectId,
        schemaId: turn.schemaId,
        intent: turn.intent,
        disposition: turn.disposition,
        executionPolicy: turn.executionPolicy,
      },
    })
  } else if (turn.disposition === 'clarification_required' && turn.clarification) {
    actions.push({
      type: 'assistant_clarification',
      label: turn.clarification.prompt,
      requiresConfirmation: false,
      payload: {
        ...turn.clarification,
        originalMessage: chatHistory.value.slice().reverse().find(item => item.role === 'user')?.text ?? '',
      },
    })
  }
  if (turn.conversation && (turn.conversation.questions.length || turn.conversation.guidance || turn.conversation.capabilityGap)) {
    actions.push({
      type: 'assistant_progressive_questions',
      label: 'Mình cần biết thêm',
      requiresConfirmation: false,
      payload: {
        ...turn.conversation,
        requestedCapabilityId: turn.intent === 'project.launch.analyze.v1' ? turn.intent : null,
        originTurnId: turn.turnId,
        originalMessage: originalMessage ?? chatHistory.value.slice().reverse().find(item => item.role === 'user')?.text ?? '',
      },
    })
  }

  return {
    reply: turn.assistantMessage,
    metrics: answer?.metrics ?? [],
    tables: answer?.tables ?? [],
    charts: answer?.charts ?? [],
    actions,
    files: answer?.files ?? [],
    sources: turn.sourceRefs?.length ? turn.sourceRefs : (answer?.sources ?? []),
    sourceRefs: answer?.sourceRefs ?? null,
    confidence: turn.confidence,
    confidenceReason: answer?.confidenceReason ?? null,
    freshness: answer?.freshness ?? null,
    usedAi: answer?.usedAi ?? false,
    intent: turn.intent,
    latencyMs: answer?.latencyMs ?? 0,
    model: answer?.model ?? null,
    processEvents: turn.processEvents ?? null,
    capabilities: turn.capabilities ?? null,
    sourceDisclosures: turn.sourceDisclosures ?? null,
    researchPlan: turn.researchPlan ?? null,
    goalAnalysis: turn.goalAnalysis ?? null,
    workPlan: turn.workPlan ?? null,
    conversation: turn.conversation ?? null,
    projectLaunchBrief: turn.projectLaunchBrief ?? null,
    projectLaunchPlan: turn.projectLaunchPlan ?? null,
    safeTestRunPreview: turn.safeTestRunPreview ?? null,
    safeTestRunReport: turn.safeTestRunReport ?? null,
  }
}

function mapStoredAssistantTurn(turn: AiAssistantStoredTurn): ChatEntry[] {
  const entries: ChatEntry[] = [{ role: 'user', text: turn.userMessage }]
  if (turn.response) {
    const response = mapAssistantTurn({
      ...turn.response,
      processEvents: turn.processEvents?.length ? turn.processEvents : turn.response.processEvents
    }, turn.userMessage)
    entries.push({
      role: 'assistant',
      text: response.reply,
      metrics: response.metrics,
      tables: response.tables,
      charts: response.charts,
      actions: response.actions,
      files: response.files,
      sources: response.sources,
      sourceRefs: response.sourceRefs,
      confidence: response.confidence,
      confidenceReason: response.confidenceReason,
      latencyMs: response.latencyMs,
      usedAi: response.usedAi,
      model: response.model,
      processEvents: response.processEvents,
      capabilities: response.capabilities,
      sourceDisclosures: response.sourceDisclosures,
      researchPlan: response.researchPlan,
      goalAnalysis: response.goalAnalysis,
      workPlan: response.workPlan,
      conversation: response.conversation,
      projectLaunchBrief: response.projectLaunchBrief,
      projectLaunchPlan: response.projectLaunchPlan,
      safeTestRunPreview: response.safeTestRunPreview,
      safeTestRunReport: response.safeTestRunReport
    })
  } else {
    entries.push({
      role: 'assistant',
      text: turn.status === 'failed'
        ? 'Lượt này chưa hoàn tất. Yêu cầu đã được lưu trên máy chủ; bạn có thể kiểm tra và thử lại.'
        : 'Yêu cầu đang được xử lý trên máy chủ. Tải lại cuộc trò chuyện để cập nhật trạng thái.',
      processEvents: turn.processEvents,
      actions: turn.status === 'failed' || turn.status === 'canceled'
        ? [{ type: 'assistant_resume_turn', label: 'Tiếp tục lượt này', payload: { turnId: turn.turnId } }]
        : []
    })
  }
  return entries
}

function applyAssistantSession(session: AiAssistantSession) {
  const restoredTarget = session.projectId && projects.value.some((project: { id: string }) => project.id === session.projectId)
    ? session.projectId
    : 'workspace'
  if (selectedTarget.value !== restoredTarget) {
    keepConversationForNextTargetChange = true
    selectedTarget.value = restoredTarget
  }
  assistantSessionId.value = session.sessionId
  window.localStorage.setItem(AI_ACTIVE_SESSION_STORAGE_KEY, session.sessionId)
  assistantSessionVersion.value = session.version
  progressiveDraft.value = session.clarificationDraft ?? null
  const runningTurn = session.turns?.find(turn => turn.status === 'running')
  activeAssistantTurnId.value = runningTurn?.turnId ?? null
  activeAssistantClientTurnId.value = runningTurn?.clientTurnId ?? null
  const restored = (session.turns ?? [])
    .slice()
    .sort((left, right) => left.sequence - right.sequence)
    .flatMap(mapStoredAssistantTurn)
  chatHistory.value = restored.length ? [buildWelcomeMessage(), ...restored] : [buildWelcomeMessage()]
}

async function createAssistantSession() {
  const projectId = selectedTarget.value === 'workspace' ? null : selectedTarget.value
  const session = await apiJson<AiAssistantSession>('/api/ai/assistant/sessions', {
    method: 'POST',
    body: JSON.stringify({
      context: {
        route: window.location.pathname,
        module: projectId ? 'project' : 'workspace',
        projectId,
        entityType: projectId ? 'project' : null,
        entityId: projectId,
        selectionIds: []
      },
      title: `Cuộc trò chuyện Trợ lý AI · ${selectedTargetLabel.value}`
    })
  })
  applyAssistantSession(session)
  return session
}

async function restoreAssistantSession() {
  if (assistantSessionLoading.value) return
  assistantSessionLoading.value = true
  try {
    let session: AiAssistantSession | null = null
    const preferredSessionId = window.localStorage.getItem(AI_ACTIVE_SESSION_STORAGE_KEY)
    if (preferredSessionId) {
      try {
        session = await apiJson<AiAssistantSession>(`/api/ai/assistant/sessions/${preferredSessionId}`)
      } catch {
        window.localStorage.removeItem(AI_ACTIVE_SESSION_STORAGE_KEY)
      }
    }
    if (!session) session = await apiJson<AiAssistantSession | null>('/api/ai/assistant/sessions/recent')
    if (session) applyAssistantSession(session)
    else await createAssistantSession()
  } catch (error) {
    showError(error instanceof Error
      ? `Không thể khôi phục cuộc trò chuyện: ${error.message}`
      : 'Không thể khôi phục cuộc trò chuyện từ máy chủ.')
  } finally {
    assistantSessionLoadAttempted.value = true
    assistantSessionLoading.value = false
  }
}

async function ensureAssistantSession() {
  if (assistantSessionId.value) return
  if (!assistantSessionLoadAttempted.value) await restoreAssistantSession()
  if (!assistantSessionId.value) await createAssistantSession()
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

async function loadConversationHistory() {
  conversationHistoryLoading.value = true
  try {
    const sessions = await apiJson<AiAssistantSessionSummary[]>('/api/ai/assistant/sessions?includeArchived=true')
    conversationHistory.value = sessions.map(session => ({
      id: session.sessionId,
      sessionId: session.sessionId,
      title: session.title,
      status: session.status,
      version: session.version,
      prompt: session.title,
      projectId: session.projectId ?? null,
      projectLabel: session.projectId
        ? projects.value.find((project: { id: string; name?: string }) => project.id === session.projectId)?.name || 'Dự án'
        : 'Tất cả dự án',
      createdAt: session.createdAt,
      updatedAt: session.updatedAt,
      assistantSnippet: session.lastMessage,
      turnCount: session.turnCount,
      archivedAt: session.archivedAt
    }))
  } catch (error) {
    conversationHistory.value = []
    showError(error instanceof Error ? error.message : 'Không thể tải lịch sử trò chuyện.')
  } finally {
    conversationHistoryLoading.value = false
  }
}

async function openSessionHistory() {
  openCockpitDrawer('history')
  await loadConversationHistory()
}

function rememberConversationPrompt(_prompt: string, _attachmentCount: number) {
  // Durable AssistantSession/AssistantTurn is the only source of truth in every surface.
}

function updateLatestConversationSnippet(_text: string) {
  void loadConversationHistory()
}

async function restoreHistoryItem(item: ConversationHistoryItem) {
  try {
    const session = await apiJson<AiAssistantSession>(`/api/ai/assistant/sessions/${item.sessionId}`)
    applyAssistantSession(session)
    await loadConversationHistory()
    closeCockpitDrawer()
    await scrollToBottom()
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể mở cuộc trò chuyện.')
  }
}

async function renameHistoryItem(item: ConversationHistoryItem) {
  const title = window.prompt('Tên mới cho cuộc trò chuyện', item.title)?.trim()
  if (!title || title === item.title) return
  try {
    const updated = await apiJson<AiAssistantSession>(`/api/ai/assistant/sessions/${item.sessionId}`, {
      method: 'PATCH',
      body: JSON.stringify({ expectedVersion: item.version, title })
    })
    if (assistantSessionId.value === item.sessionId) applyAssistantSession(updated)
    await loadConversationHistory()
    showSuccess('Đã đổi tên cuộc trò chuyện.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể đổi tên cuộc trò chuyện.')
  }
}

async function archiveHistoryItem(item: ConversationHistoryItem) {
  if (!window.confirm(`Lưu trữ cuộc trò chuyện “${item.title}”?`)) return
  try {
    await apiJson<AiAssistantSession>(`/api/ai/assistant/sessions/${item.sessionId}/archive`, {
      method: 'POST',
      body: JSON.stringify({ expectedVersion: item.version })
    })
    if (assistantSessionId.value === item.sessionId) await startNewConversation()
    await loadConversationHistory()
    showSuccess('Đã lưu trữ cuộc trò chuyện.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể lưu trữ cuộc trò chuyện.')
  }
}

async function deleteHistoryItem(item: ConversationHistoryItem) {
  if (!window.confirm(`Xóa cuộc trò chuyện “${item.title}”? Thao tác này sẽ ẩn cuộc trò chuyện khỏi lịch sử.`)) return
  try {
    await apiJson<void>(`/api/ai/assistant/sessions/${item.sessionId}?expectedVersion=${item.version}`, { method: 'DELETE' })
    if (assistantSessionId.value === item.sessionId) await startNewConversation()
    await loadConversationHistory()
    showSuccess('Đã xóa cuộc trò chuyện.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể xóa cuộc trò chuyện.')
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

watch(selectedTarget, async () => {
  if (keepConversationForNextTargetChange) {
    keepConversationForNextTargetChange = false
    return
  }
  chatHistory.value = [buildWelcomeMessage()]
  selectedDrawerMessage.value = null
  assistantSessionId.value = null
  assistantSessionVersion.value = 0
  assistantSessionLoadAttempted.value = false
  try {
    await createAssistantSession()
  } catch {
    // Keep the composer available; the next send will retry durable session creation.
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
    return `### Đánh giá hiệu suất làm việc tuần qua\n\n- **Tiến độ**: Các dự án trong Workspace hoạt động đúng tiến độ đạt **75%**. Tổng số nhiệm vụ đã hoàn tất trong tuần là **8 nhiệm vụ**.\n- **Thời gian**: Toàn nhóm đã ghi nhận **32 giờ chấm công thực tế**.\n- **Nhận xét**: Năng suất duy trì ở mức ổn định. Điểm sáng là sự tập trung cao độ ở các task thuộc luồng quan trọng.`
  }
  if (p.includes('rủi ro') || p.includes('chậm') || p.includes('risk') || p.includes('quá hạn')) {
    return `### Đánh giá rủi ro toàn Workspace\n\n- **Nhiệm vụ trễ hạn**: Phát hiện dự án đang có **1 nhiệm vụ quá hạn** cần xử lý.\n- **Dự án chịu ảnh hưởng**: Dự án DATN đang có tỉ lệ quá hạn nhẹ.\n- **Giải pháp**: Nhắc nhở người thực hiện trực tiếp hoặc phân bổ thêm thành viên hỗ trợ để tháo gỡ điểm nghẽn.`
  }
  return `Chào bạn! Mình là Erumi. Hiện tại mô hình AI cục bộ đang ở trạng thái ngoại tuyến.\n\nTuy nhiên, bạn có thể chọn các dự án cụ thể trong menu ngữ cảnh và dùng nút **+** để mở các câu hỏi gợi ý hay công cụ phân tích để mình trích xuất báo cáo thông minh trực tiếp từ dữ liệu hệ thống nhé!`
}

async function pollAssistantTurn(clientTurnId: string, placeholderIndex: number, shouldStop: () => boolean) {
  if (!assistantSessionId.value) return
  const sessionId = assistantSessionId.value
  let observedTurn = false
  let streamedAnswer = ''
  const streamCompleted = await new Promise<boolean>((resolve) => {
    const stream = new EventSource(
      `/api/ai/assistant/sessions/${sessionId}/stream?clientTurnId=${encodeURIComponent(clientTurnId)}`)
    const stopTimer = window.setInterval(() => {
      if (shouldStop() && !observedTurn) {
        window.clearInterval(stopTimer)
        stream.close()
        resolve(false)
      }
    }, 400)
    const close = (completed: boolean) => {
      window.clearInterval(stopTimer)
      stream.close()
      resolve(completed)
    }
    stream.addEventListener('progress', async (event) => {
      observedTurn = true
      try {
        const progress = JSON.parse((event as MessageEvent).data) as AiAssistantProcessEvent
        const current = chatHistory.value[placeholderIndex]
        if (current?.role === 'assistant') {
          const existing = current.processEvents ?? []
          const next = [...existing.filter(item => item.sequence !== progress.sequence), progress]
            .sort((left, right) => left.sequence - right.sequence)
          chatHistory.value[placeholderIndex] = { ...current, processEvents: next }
          await scrollToBottom()
        }
      } catch {
        // The durable POST response remains authoritative.
      }
    })
    stream.addEventListener('answer_delta', async (event) => {
      observedTurn = true
      try {
        const payload = JSON.parse((event as MessageEvent).data) as { delta: string }
        streamedAnswer += payload.delta
        const current = chatHistory.value[placeholderIndex]
        if (current?.role === 'assistant') {
          chatHistory.value[placeholderIndex] = { ...current, text: streamedAnswer }
          await scrollToBottom()
        }
      } catch {
        // Invalid stream chunks are ignored; read-back supplies the canonical answer.
      }
    })
    stream.addEventListener('done', (event) => {
      try {
        const payload = JSON.parse((event as MessageEvent).data) as { version?: number }
        if (payload.version != null) assistantSessionVersion.value = payload.version
      } finally {
        close(true)
      }
    })
    stream.onerror = () => close(false)
  })
  if (streamCompleted || shouldStop()) return

  // Compatibility fallback for proxies that do not forward SSE.
  while (!shouldStop()) {
    await new Promise(resolve => window.setTimeout(resolve, 650))
    if (shouldStop()) break
    try {
      const session = await apiJson<AiAssistantSession>(`/api/ai/assistant/sessions/${sessionId}`)
      assistantSessionVersion.value = session.version
      const stored = session.turns?.find(turn => turn.clientTurnId === clientTurnId)
      if (!stored) continue
      activeAssistantTurnId.value = stored.turnId
      const current = chatHistory.value[placeholderIndex]
      if (current?.role === 'assistant') {
        chatHistory.value[placeholderIndex] = { ...current, processEvents: stored.processEvents }
        await scrollToBottom()
      }
    } catch {
      // The POST and durable reload remain authoritative.
    }
  }
}

async function cancelActiveAssistantTurn() {
  if (!activeAssistantTurnId.value || !assistantSessionId.value) return
  try {
    await apiJson<AiAssistantSession>(`/api/ai/assistant/turns/${activeAssistantTurnId.value}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ expectedVersion: assistantSessionVersion.value })
    })
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể hủy lượt này.')
  }
}

async function resumeAssistantTurn(action: ErumiAction) {
  const turnId = String(action.payload?.turnId || '')
  if (!turnId || isChatting.value) return
  isChatting.value = true
  try {
    const clientTurnId = crypto.randomUUID()
    await apiJson<AiAssistantTurnResponse>(`/api/ai/assistant/turns/${turnId}/resume`, {
      method: 'POST',
      headers: {
        'Idempotency-Key': `assistant:resume:${turnId}:${clientTurnId}`,
        'X-Request-Id': clientTurnId
      },
      body: JSON.stringify({ expectedVersion: assistantSessionVersion.value, clientTurnId })
    })
    await restoreAssistantSession()
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể tiếp tục lượt này.')
  } finally {
    isChatting.value = false
  }
}

async function confirmSafeTestRun(entry: ChatEntry, preview: AiSafeTestRunPreview) {
  if (safeTestActionBusy.value || !window.confirm(
    `Chạy manifest cố định ${preview.manifestId} gồm ${preview.suites.length} suite trong Development/Test?`)) return
  safeTestActionBusy.value = preview.runId
  try {
    const report = await apiJson<AiSafeTestRunReport>(
      `/api/ai/assistant/test-runs/${preview.runId}/confirm`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': `safe-test:${preview.runId}:${preview.revision}` },
        body: JSON.stringify({ expectedRevision: preview.revision })
      })
    entry.safeTestRunReport = report
    entry.safeTestRunPreview = { ...preview, status: report.status, requiresConfirmation: false, revision: report.revision }
    showSuccess(report.status === 'passed' ? 'Acceptance manifest đã PASS.' : 'Acceptance manifest đã hoàn tất; có suite cần xử lý.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể chạy acceptance manifest.')
  } finally {
    safeTestActionBusy.value = null
  }
}

function selectedLaunchScenarioId(plan: ProjectLaunchPlan) {
  return selectedLaunchScenarios.value[plan.planId]
    || plan.selectedScenarioId
    || plan.staffingScenarios.find(item => item.feasible)?.scenarioId
    || ''
}

function selectLaunchScenario(planId: string, scenarioId: string) {
  selectedLaunchScenarios.value = { ...selectedLaunchScenarios.value, [planId]: scenarioId }
}

async function createRecommendedRulebookDraft(brief: ProjectLaunchBrief) {
  launchActionBusy.value = `rulebook:${brief.organizationId}`
  try {
    const result = await apiResult<OrganizationWorkRuleSet>(
      `/api/organizations/${brief.organizationId}/work-rulebook`,
      {
        method: 'POST',
        body: JSON.stringify({
          effectiveFrom: new Date().toISOString(),
          rules: [
            {
              ruleKey: 'active_membership_required',
              category: 'governance',
              enforcement: 'block',
              description: 'Người yêu cầu phải là thành viên đang hoạt động của tổ chức.'
            },
            {
              ruleKey: 'max_active_projects',
              category: 'portfolio_capacity',
              enforcement: 'block',
              description: 'Giới hạn số dự án đang hoạt động để bảo vệ năng lực tổ chức.',
              numericValue: 20,
              unit: 'projects'
            },
            {
              ruleKey: 'capacity_evidence_required',
              category: 'staffing',
              enforcement: 'block',
              description: 'Mọi phân công phải có dữ liệu capacity và availability còn hiệu lực.'
            }
          ]
        })
      }
    )
    rulebookDrafts.value = { ...rulebookDrafts.value, [brief.organizationId]: result }
    showSuccess('Đã tạo bản nháp Rulebook. Chưa có policy nào được kích hoạt.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể tạo bản nháp Rulebook.')
  } finally {
    launchActionBusy.value = null
  }
}

async function activateRulebookAndResume(brief: ProjectLaunchBrief) {
  const draft = rulebookDrafts.value[brief.organizationId]
  if (!draft || !window.confirm(`Kích hoạt Organization Rulebook v${draft.version} và tiếp tục Project Launch?`)) return
  launchActionBusy.value = `rulebook:${brief.organizationId}`
  try {
    await apiResult<OrganizationWorkRuleSet>(
      `/api/organizations/${brief.organizationId}/work-rulebook/${draft.ruleSetId}/activate`,
      { method: 'POST', body: JSON.stringify({ revision: draft.revision }) }
    )
    rulebookDrafts.value = { ...rulebookDrafts.value, [brief.organizationId]: { ...draft, status: 'active' } }
    showSuccess('Rulebook đã được kích hoạt. Trợ lý đang tiếp tục Launch Brief bằng policy mới.')
    await submitChat(
      brief.objective,
      undefined,
      'Tiếp tục sau khi kích hoạt Rulebook',
      undefined,
      'project.launch.analyze.v1',
      brief.organizationId
    )
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể kích hoạt Rulebook.')
  } finally {
    launchActionBusy.value = null
  }
}

async function createLaunchPlan(brief: ProjectLaunchBrief) {
  await submitChat(
    `Create staffing scenarios and a delivery plan for ${brief.proposedProjectName}. Use the reviewed Launch Brief and current Qaly facts.`,
    undefined,
    'Lập staffing + delivery plan',
    undefined,
    'project.staffing.plan.v1',
    brief.organizationId
  )
}

async function confirmLaunchPlan(entry: ChatEntry, plan: ProjectLaunchPlan) {
  const scenarioId = selectedLaunchScenarioId(plan)
  const scenario = plan.staffingScenarios.find(item => item.scenarioId === scenarioId)
  if (!scenario?.feasible || scenario.blockingReasons.length || plan.blockingReasons.length) {
    showError('Hãy chọn một phương án khả thi và xử lý toàn bộ blocking decision trước khi xác nhận.')
    return
  }
  if (!window.confirm(`Xác nhận tạo Project "${plan.deliveryPlan.proposedProjectName}" cùng members, Sprints, Tasks và dependencies đã hiển thị?`)) return
  launchActionBusy.value = plan.planId
  try {
    const updated = await apiResult<ProjectLaunchPlan>(`/api/ai/project-launch/plans/${plan.planId}/confirm`, {
      method: 'POST',
      headers: { 'Idempotency-Key': `project-launch:${plan.planId}:${plan.rowRevision}:${scenarioId}` },
      body: JSON.stringify({ confirmed: true, expectedRevision: plan.rowRevision, selectedScenarioId: scenarioId })
    })
    entry.projectLaunchPlan = updated
    showSuccess('Đã tạo Project và kiểm tra lại dữ liệu thành công.')
    await loadDashboard()
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể xác nhận Project launch.')
  } finally {
    launchActionBusy.value = null
  }
}

async function rollbackLaunch(entry: ChatEntry, plan: ProjectLaunchPlan) {
  const receipt = plan.executionReceipt
  if (!receipt?.rollbackAvailable) return
  const reason = window.prompt('Nhập lý do rollback (bắt buộc, tối thiểu 5 ký tự):')?.trim()
  if (!reason || reason.length < 5) return
  if (!window.confirm('Rollback chỉ được thực hiện khi Project chưa phát sinh công việc người dùng. Tiếp tục?')) return
  launchActionBusy.value = receipt.receiptId
  try {
    const updated = await apiResult<ProjectLaunchPlan>(`/api/ai/project-launch/executions/${receipt.receiptId}/rollback`, {
      method: 'POST',
      headers: { 'Idempotency-Key': `project-launch-rollback:${receipt.receiptId}:${receipt.revision}` },
      body: JSON.stringify({ confirmed: true, expectedRevision: receipt.revision, reason })
    })
    entry.projectLaunchPlan = updated
    showSuccess('Đã rollback launch; audit receipt vẫn được giữ lại.')
    await loadDashboard()
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể rollback Project launch.')
  } finally {
    launchActionBusy.value = null
  }
}

async function monitorLaunch(entry: ChatEntry, plan: ProjectLaunchPlan) {
  const receipt = plan.executionReceipt
  if (!receipt) return
  launchActionBusy.value = receipt.receiptId
  try {
    const updated = await apiResult<ProjectLaunchPlan>(`/api/ai/project-launch/executions/${receipt.receiptId}/monitor`, {
      method: 'POST',
      body: JSON.stringify({ expectedRevision: receipt.revision })
    })
    entry.projectLaunchPlan = updated
    showSuccess(updated.latestReplanProposal ? 'Đã tạo replan proposal để review.' : 'Không phát hiện drift đáng kể.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể monitor Project launch.')
  } finally {
    launchActionBusy.value = null
  }
}

function openLaunchLink(path: string) {
  if (!/^\/projects\/[0-9a-f-]{36}(?:\?tab=(?:tasks|members|roadmap))?$/i.test(path)) return
  void router.push(path)
}

async function submitChat(
  explicitText?: string,
  _action?: string,
  displayText?: string,
  progressiveReply?: { questionId: string; value: string; label?: string },
  requestedCapabilityId?: string,
  requestedOrganizationId?: string,
  progressiveReplies?: AiAssistantProgressiveReply[]
) {
  const prompt = (explicitText ?? chatInput.value).trim()
  const filesToSend = selectedFiles.value.slice()
  if ((!prompt && filesToSend.length === 0) || isChatting.value) return false

  const userText = displayText || prompt || 'Phân tích file đã đính kèm'
  rememberConversationPrompt(userText, filesToSend.length)
  const effectiveProgressiveReplies = progressiveReplies ?? (progressiveReply ? [progressiveReply] : undefined)
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
    await ensureAssistantSession()
    chatHistory.value.push({ role: 'assistant', text: '' })
    const lastIdx = chatHistory.value.length - 1
    const attachedFileContexts = filesToSend.length ? await parseAttachedFiles(filesToSend) : []
    const projectId = selectedTarget.value === 'workspace' ? null : selectedTarget.value
    const clientTurnId = crypto.randomUUID()
    activeAssistantClientTurnId.value = clientTurnId
    let stopPolling = false
    const polling = pollAssistantTurn(clientTurnId, lastIdx, () => stopPolling)
    let turn: AiAssistantTurnResponse
    try {
      turn = await apiJson<AiAssistantTurnResponse>('/api/ai/assistant/turns', {
        method: 'POST',
        headers: {
          'Idempotency-Key': `assistant:${assistantSessionId.value}:${clientTurnId}`,
          'X-Request-Id': clientTurnId
        },
        body: JSON.stringify({
          message: prompt || userText,
          context: {
            route: window.location.pathname,
            module: projectId ? 'project' : 'workspace',
            projectId,
            entityType: projectId ? 'project' : null,
            entityId: projectId,
            organizationId: requestedOrganizationId || null,
            selectionIds: [],
          },
          mode: 'agent',
          language: 'vi',
          providerHint: selectedProviderHint.value,
          files: attachedFileContexts,
          sessionId: assistantSessionId.value,
          expectedVersion: assistantSessionVersion.value,
          clientTurnId,
          progressiveReply: effectiveProgressiveReplies?.length === 1 ? effectiveProgressiveReplies[0] : null,
          progressiveReplies: effectiveProgressiveReplies,
          requestedCapabilityId,
        })
      })
    } finally {
      stopPolling = true
      await polling
    }
    activeAssistantTurnId.value = null
    activeAssistantClientTurnId.value = null
    assistantSessionVersion.value = turn.sessionVersion ?? assistantSessionVersion.value
    const fastReply: ErumiChatResponse = mapAssistantTurn(turn)

    const replyActions = fastReply.actions || []

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
      model: fastReply.model,
      processEvents: fastReply.processEvents,
      capabilities: fastReply.capabilities,
      sourceDisclosures: fastReply.sourceDisclosures,
      researchPlan: fastReply.researchPlan,
      goalAnalysis: fastReply.goalAnalysis,
      workPlan: fastReply.workPlan,
      conversation: fastReply.conversation,
      projectLaunchBrief: fastReply.projectLaunchBrief,
      projectLaunchPlan: fastReply.projectLaunchPlan,
      safeTestRunPreview: fastReply.safeTestRunPreview,
      safeTestRunReport: fastReply.safeTestRunReport
    }
    updateLatestConversationSnippet(fastReply.reply)

    const composerAction = replyActions.find(action => action.type === 'compose_task_plan')
    if (composerAction) openComposerAction(composerAction)
    return true
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
    await restoreAssistantSession()
    return false
  } finally {
    isChatting.value = false
    activeAssistantTurnId.value = null
    activeAssistantClientTurnId.value = null
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

onMounted(async () => {
  syncViewportFlag()
  await restoreAssistantSession()
  await loadConversationHistory()
  selectedTarget.value = 'workspace'
  applyRoutePrompt()
  refreshAnalyticsContext()
  refreshTimer = window.setInterval(refreshAnalyticsContext, 30000)
  window.addEventListener('resize', syncViewportFlag)
  window.addEventListener('focus', refreshAnalyticsContext)
  scrollToBottom()
})

watch(() => [route.query.prompt, route.query.scope], applyRoutePrompt)

watch(selectedAiModel, value => {
  window.localStorage.setItem(AI_MODEL_STORAGE_KEY, value)
})

watch(
  () => props.externalPromptToken,
  () => {
    if (props.externalPrompt.trim()) fillComposer(props.externalPrompt.trim())
    else nextTick(() => textareaRef.value?.focus())
  },
)

watch(
  () => props.externalHistoryToken,
  (value, previous) => {
    if (value !== previous && value > 0) void openSessionHistory()
  },
)

onBeforeUnmount(() => {
  if (refreshTimer) window.clearInterval(refreshTimer)
  if (clarificationInputSaveTimer != null) window.clearTimeout(clarificationInputSaveTimer)
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
      <button type="button" class="empty-session-history" data-testid="assistant-session-history" @click="openSessionHistory">
        <Clock3 :size="16" aria-hidden="true" /> Phiên
      </button>
      <div class="empty-inner">
        <div class="empty-avatar">
          <ChatbotAvatar size="medium" />
        </div>
        <h1 class="empty-heading">Bạn muốn Qaly giúp gì?</h1>

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
              aria-label="Nhập yêu cầu cho Trợ lý AI"
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
        <div class="chat-title-wrap">
          <h1 class="chat-title">Phân tích dự án</h1>
          <span v-if="assistantSessionId" class="chat-session-state">Phiên được lưu tự động</span>
        </div>
        <div class="chat-header-actions">
          <button type="button" class="session-history-btn" data-testid="assistant-session-history" @click="openSessionHistory">
            <Clock3 :size="15" aria-hidden="true" /> Phiên
          </button>
          <OverflowMenu
            :items="headerMenuItems"
            aria-label="Tùy chọn cuộc trò chuyện"
            trigger-title="Tùy chọn"
            @select="handleHeaderMenu"
          />
        </div>
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
                  <div v-if="msg.text" class="markdown-body assistant-primary-answer" v-html="renderMarkdown(msg.text)"></div>
                  <details v-if="msg.processEvents?.length" class="assistant-process-disclosure">
                    <summary>Các bước Trợ lý AI đã thực hiện ({{ msg.processEvents.length }})</summary>
                    <ol class="assistant-process" aria-label="Các bước Trợ lý AI đã thực hiện">
                      <li
                        v-for="event in msg.processEvents"
                        :key="`${event.sequence}-${event.stage}`"
                        :class="`status-${event.status}`"
                      >
                        <span class="assistant-process-dot" aria-hidden="true"></span>
                        <span>{{ event.publicLabel }}</span>
                        <small v-if="typeof event.durationMs === 'number' && event.durationMs > 0">
                          {{ Math.max(1, Math.round(event.durationMs / 1000)) }}s
                        </small>
                      </li>
                    </ol>
                  </details>
                  <details
                    v-if="msg.capabilities?.length || msg.sourceDisclosures?.length"
                    class="assistant-context-disclosure"
                  >
                    <summary>
                      Ngữ cảnh đã kiểm tra · {{ msg.sourceDisclosures?.filter(source => source.status === 'read').length || 0 }} nguồn
                    </summary>
                    <div v-if="msg.capabilities?.length" class="assistant-capability-list" aria-label="Khả năng AI được cấp quyền">
                      <span v-for="capability in msg.capabilities" :key="capability.capabilityId">
                        {{ capability.capabilityId }}
                      </span>
                    </div>
                    <ul v-if="msg.sourceDisclosures?.length" class="assistant-source-list">
                      <li
                        v-for="source in msg.sourceDisclosures"
                        :key="`${source.sourceId}-${source.status}`"
                        :class="`source-${source.status}`"
                      >
                        <strong>{{ source.sourceId }}</strong>
                        <span>{{ source.label }}</span>
                      </li>
                    </ul>
                  </details>
                  <details
                    v-if="msg.goalAnalysis && msg.workPlan"
                    class="assistant-work-plan-details"
                  >
                    <summary>Chi tiết lập kế hoạch AI ({{ Math.round(msg.goalAnalysis.confidence * 100) }}%)</summary>
                    <article
                      class="assistant-work-plan-card"
                      data-testid="assistant-work-plan"
                    >
                      <header class="assistant-work-plan-header">
                        <div>
                          <span>AI hiểu yêu cầu</span>
                          <h3>{{ msg.goalAnalysis.objective }}</h3>
                          <p>{{ msg.goalAnalysis.userJob }}</p>
                        </div>
                        <strong>{{ Math.round(msg.goalAnalysis.confidence * 100) }}%</strong>
                      </header>
                      <div class="assistant-work-plan-meta">
                        <span>Phạm vi: {{ msg.workPlan.scope.label }}</span>
                        <span :class="`disposition-${msg.goalAnalysis.disposition}`">{{ goalDispositionLabel(msg.goalAnalysis.disposition) }}</span>
                        <span v-if="msg.goalAnalysis.usedFallback">Fallback giới hạn</span>
                        <span v-else-if="msg.model">{{ msg.model.label }}</span>
                        <span v-else>{{ msg.goalAnalysis.actualProvider }} / {{ msg.goalAnalysis.actualModel }}</span>
                      </div>
                      <section v-if="msg.goalAnalysis.selectedSkills.length" class="assistant-selected-skill">
                        <span>Skill được chọn</span>
                        <strong>{{ msg.goalAnalysis.selectedSkills[0].title }}</strong>
                        <code>{{ msg.goalAnalysis.selectedSkills[0].skillId }}</code>
                        <p>{{ msg.goalAnalysis.selectedSkills[0].fitReason }}</p>
                      </section>
                      <details v-else-if="msg.goalAnalysis.missingSkills.length" class="assistant-missing-skill">
                        <summary>Chưa thể tự thực hiện trực tiếp</summary>
                        <p>{{ msg.goalAnalysis.missingSkills[0].reason }}</p>
                      </details>
                      <ol class="assistant-work-plan-steps" aria-label="Kế hoạch thực hiện của Trợ lý AI">
                        <li v-for="step in msg.workPlan.steps" :key="step.stepId" :class="`step-${step.state}`">
                          <span>{{ step.stepId }}</span>
                          <div>
                            <strong>{{ step.publicLabel }}</strong>
                            <small v-if="step.verificationIds.length">Kiểm tra: {{ step.verificationIds.join(', ') }}</small>
                          </div>
                          <em>{{ step.state }}</em>
                        </li>
                      </ol>
                      <details v-if="msg.goalAnalysis.warnings.length" class="assistant-work-plan-warnings">
                        <summary>Giới hạn và cảnh báo ({{ msg.goalAnalysis.warnings.length }})</summary>
                        <ul><li v-for="warning in msg.goalAnalysis.warnings" :key="warning">{{ warning }}</li></ul>
                      </details>
                    </article>
                  </details>

                  <article
                    v-if="msg.safeTestRunPreview"
                    class="project-launch-brief-card safe-test-run-card"
                    data-testid="safe-test-run-preview"
                  >
                    <header class="project-launch-brief-header">
                      <div>
                        <span>Dev/Test only · manifest cố định</span>
                        <h3>{{ msg.safeTestRunPreview.title }}</h3>
                        <p>{{ msg.safeTestRunPreview.manifestId }} · không nhận command từ nội dung chat</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ msg.safeTestRunReport?.status || msg.safeTestRunPreview.status }}</strong>
                        <small>~{{ Math.max(1, Math.round(msg.safeTestRunPreview.estimatedSeconds / 60)) }} phút</small>
                        <small>External API cost: {{ msg.safeTestRunPreview.estimatedExternalCost }}</small>
                      </div>
                    </header>
                    <ol class="safe-test-suite-list">
                      <li v-for="suite in msg.safeTestRunPreview.suites" :key="suite.id">
                        <div><strong>{{ suite.label }}</strong><small>{{ suite.project }}</small></div>
                        <span>{{ suite.scope }}</span>
                      </li>
                    </ol>
                    <ol v-if="msg.safeTestRunReport?.events.length" class="safe-test-suite-list safe-test-events">
                      <li v-for="event in msg.safeTestRunReport.events" :key="event.sequence" :class="`status-${event.status}`">
                        <strong>{{ event.publicLabel }}</strong><span>{{ event.status }}</span>
                      </li>
                    </ol>
                    <p v-if="msg.safeTestRunReport?.safeSummary" class="safe-test-summary">{{ msg.safeTestRunReport.safeSummary }}</p>
                    <button
                      v-if="msg.safeTestRunPreview.requiresConfirmation && !msg.safeTestRunReport"
                      type="button"
                      class="launch-primary-action safe-test-confirm"
                      :disabled="Boolean(safeTestActionBusy)"
                      data-testid="safe-test-run-confirm"
                      @click="confirmSafeTestRun(msg, msg.safeTestRunPreview)"
                    >{{ safeTestActionBusy === msg.safeTestRunPreview.runId ? 'Đang chạy manifest…' : 'Xác nhận và chạy kiểm thử' }}</button>
                  </article>

                  <article
                    v-if="msg.projectLaunchBrief"
                    class="project-launch-brief-card"
                    data-testid="project-launch-brief"
                  >
                    <header class="project-launch-brief-header">
                      <div>
                        <span>Project Launch Brief · chỉ xem lại</span>
                        <h3>{{ msg.projectLaunchBrief.proposedProjectName }}</h3>
                        <p>{{ msg.projectLaunchBrief.objective }}</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ msg.projectLaunchBrief.state }}</strong>
                        <small>Revision {{ msg.projectLaunchBrief.revision }}</small>
                        <small>{{ msg.projectLaunchBrief.organizationName }}</small>
                      </div>
                    </header>
                    <div class="project-launch-rulebook" :class="`status-${msg.projectLaunchBrief.rulebookStatus}`">
                      <strong>Organization Rulebook</strong>
                      <span v-if="msg.projectLaunchBrief.ruleSetVersion">v{{ msg.projectLaunchBrief.ruleSetVersion }} hiệu lực</span>
                      <span v-else>Chưa có phiên bản hiệu lực · policy_missing</span>
                      <button
                        v-if="msg.projectLaunchBrief.rulebookStatus === 'policy_missing' && !rulebookDrafts[msg.projectLaunchBrief.organizationId]"
                        type="button"
                        :disabled="launchActionBusy === `rulebook:${msg.projectLaunchBrief.organizationId}`"
                        @click="createRecommendedRulebookDraft(msg.projectLaunchBrief)"
                      >Tạo bản nháp Rulebook đề xuất</button>
                      <button
                        v-else-if="rulebookDrafts[msg.projectLaunchBrief.organizationId]?.status === 'draft'"
                        type="button"
                        :disabled="launchActionBusy === `rulebook:${msg.projectLaunchBrief.organizationId}`"
                        @click="activateRulebookAndResume(msg.projectLaunchBrief)"
                      >Review và kích hoạt v{{ rulebookDrafts[msg.projectLaunchBrief.organizationId].version }}</button>
                    </div>
                    <div class="project-launch-columns">
                      <section>
                        <h4>Phạm vi đề xuất</h4>
                        <ul><li v-for="item in msg.projectLaunchBrief.scope" :key="item">{{ item }}</li></ul>
                      </section>
                      <section>
                        <h4>Thước đo thành công</h4>
                        <ul><li v-for="item in msg.projectLaunchBrief.successMeasures" :key="item">{{ item }}</li></ul>
                      </section>
                      <section>
                        <h4>Giả định</h4>
                        <p v-if="!msg.projectLaunchBrief.assumptions.length">Không có giả định được ghi nhận.</p>
                        <ul v-else><li v-for="item in msg.projectLaunchBrief.assumptions" :key="item">{{ item }}</li></ul>
                      </section>
                      <section>
                        <h4>Unknowns</h4>
                        <p v-if="!msg.projectLaunchBrief.unknowns.length">Không có unknown từ model.</p>
                        <ul v-else><li v-for="item in msg.projectLaunchBrief.unknowns" :key="item">{{ item }}</li></ul>
                      </section>
                    </div>
                    <details class="project-launch-decisions">
                      <summary>Quyết định Rulebook ({{ msg.projectLaunchBrief.ruleDecisions.length }})</summary>
                      <ul>
                        <li
                          v-for="decision in msg.projectLaunchBrief.ruleDecisions"
                          :key="decision.ruleKey"
                          :class="`decision-${decision.result}`"
                        >
                          <strong>{{ decision.ruleKey }}</strong>
                          <span>{{ decision.result }}</span>
                          <p>{{ decision.explanation }}</p>
                        </li>
                      </ul>
                    </details>
                    <footer>
                      <span>{{ msg.projectLaunchBrief.actualProvider }} / {{ msg.projectLaunchBrief.actualModel }}</span>
                      <span>{{ msg.projectLaunchBrief.promptVersion }}</span>
                      <strong>Không tạo Project · không phân công</strong>
                      <button
                        class="launch-primary-action"
                        type="button"
                        :disabled="isChatting || msg.projectLaunchBrief.rulebookStatus !== 'effective' || msg.projectLaunchBrief.questions.some(question => question.blocking)"
                        data-testid="project-launch-plan-start"
                        @click="createLaunchPlan(msg.projectLaunchBrief)"
                      >Lập staffing + delivery plan</button>
                      <small v-if="msg.projectLaunchBrief.rulebookStatus !== 'effective'">Cần kích hoạt Rulebook trước khi staffing.</small>
                      <small v-else-if="msg.projectLaunchBrief.questions.some(question => question.blocking)">Cần gửi đủ câu trả lời còn thiếu trước khi staffing.</small>
                    </footer>
                  </article>

                  <article
                    v-if="msg.projectLaunchPlan"
                    class="project-launch-plan-card"
                    data-testid="project-launch-plan"
                  >
                    <header class="project-launch-plan-header">
                      <div>
                        <span>AI-native Project launch · {{ msg.projectLaunchPlan.schemaId }}</span>
                        <h3>{{ msg.projectLaunchPlan.deliveryPlan.proposedProjectName }}</h3>
                        <p>{{ msg.projectLaunchPlan.deliveryPlan.objective }}</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ msg.projectLaunchPlan.state }}</strong>
                        <small>Plan rev {{ msg.projectLaunchPlan.rowRevision }}</small>
                        <small>Rulebook v{{ msg.projectLaunchPlan.ruleSetVersion || 'missing' }}</small>
                      </div>
                    </header>

                    <div v-if="msg.projectLaunchPlan.blockingReasons.length" class="launch-blocking-list">
                      <strong>Chưa thể xác nhận</strong>
                      <ul><li v-for="item in msg.projectLaunchPlan.blockingReasons" :key="item">{{ item }}</li></ul>
                    </div>
                    <div v-if="msg.projectLaunchPlan.warnings.length" class="launch-warning-list">
                      <strong>Cảnh báo</strong>
                      <ul><li v-for="item in msg.projectLaunchPlan.warnings" :key="item">{{ item }}</li></ul>
                    </div>

                    <section class="launch-plan-section">
                      <h4>1. Chọn staffing scenario</h4>
                      <div class="launch-scenario-list">
                        <article
                          v-for="scenario in msg.projectLaunchPlan.staffingScenarios"
                          :key="scenario.scenarioId"
                          class="launch-scenario"
                          :class="{ feasible: scenario.feasible, selected: selectedLaunchScenarioId(msg.projectLaunchPlan) === scenario.scenarioId }"
                        >
                          <label>
                            <input
                              type="radio"
                              :name="`launch-scenario-${msg.projectLaunchPlan.planId}`"
                              :value="scenario.scenarioId"
                              :checked="selectedLaunchScenarioId(msg.projectLaunchPlan) === scenario.scenarioId"
                              :disabled="!scenario.feasible || Boolean(msg.projectLaunchPlan.executionReceipt)"
                              @change="selectLaunchScenario(msg.projectLaunchPlan.planId, scenario.scenarioId)"
                            >
                            <span><strong>{{ scenario.title }}</strong><small>Score {{ scenario.score.toFixed(1) }} · {{ scenario.feasible ? 'khả thi' : 'blocked' }}</small></span>
                          </label>
                          <p>{{ scenario.description }}</p>
                          <p><strong>Manager:</strong> {{ scenario.managerName || 'chưa đủ điều kiện' }}</p>
                          <ul class="launch-member-list">
                            <li v-for="member in scenario.members" :key="member.userId">
                              <strong>{{ member.displayName }}</strong>
                              <span>{{ member.proposedRole }} · {{ member.proposedHours }}h · load {{ member.loadAfterPercent.toFixed(0) }}%</span>
                              <small>Skills: {{ member.coveredSkills.join(', ') || 'chưa có evidence' }}</small>
                            </li>
                          </ul>
                          <details v-if="scenario.blockingReasons.length || scenario.managerCandidates.some(item => item.hardRejects.length)">
                            <summary>Blocking / candidate rejects</summary>
                            <ul>
                              <li v-for="reason in scenario.blockingReasons" :key="reason">{{ reason }}</li>
                              <li v-for="candidate in scenario.managerCandidates.filter(item => item.hardRejects.length)" :key="candidate.userId">
                                {{ candidate.displayName }}: {{ candidate.hardRejects.join(', ') }}
                              </li>
                            </ul>
                          </details>
                        </article>
                      </div>
                    </section>

                    <section class="launch-plan-section">
                      <h4>2. Delivery plan thật sẽ được tạo</h4>
                      <p class="launch-plan-range">
                        {{ new Date(msg.projectLaunchPlan.deliveryPlan.startDate).toLocaleDateString('vi-VN') }}
                        → {{ new Date(msg.projectLaunchPlan.deliveryPlan.endDate).toLocaleDateString('vi-VN') }}
                      </p>
                      <details
                        v-for="sprint in msg.projectLaunchPlan.deliveryPlan.sprints.filter(item => item.selected)"
                        :key="sprint.clientId"
                        class="launch-sprint"
                        open
                      >
                        <summary>{{ sprint.name }} · {{ sprint.tasks.filter(item => item.selected).length }} tasks</summary>
                        <p>{{ sprint.objective }}</p>
                        <ol>
                          <li v-for="task in sprint.tasks.filter(item => item.selected)" :key="task.clientId">
                            <strong>{{ task.title }}</strong>
                            <span>{{ task.priority }} · {{ task.estimatedHours }}h</span>
                            <small>Skills: {{ task.requiredSkillNames.join(', ') || 'general' }}</small>
                            <small v-if="task.dependencyClientIds.length">Depends on: {{ task.dependencyClientIds.join(', ') }}</small>
                          </li>
                        </ol>
                      </details>
                      <details v-if="msg.projectLaunchPlan.deliveryPlan.externalDeferred.length" class="launch-external-deferred">
                        <summary>External actions được hoãn có chủ đích</summary>
                        <ul><li v-for="item in msg.projectLaunchPlan.deliveryPlan.externalDeferred" :key="item">{{ item }}</li></ul>
                      </details>
                    </section>

                    <section v-if="msg.projectLaunchPlan.executionReceipt" class="launch-receipt" data-testid="project-launch-receipt">
                      <h4>3. Execution receipt · {{ msg.projectLaunchPlan.executionReceipt.state }}</h4>
                      <p>
                        Transaction: <strong>{{ msg.projectLaunchPlan.executionReceipt.internalTransactionCommitted ? 'committed' : 'not committed' }}</strong>
                        · Read-back: <strong>{{ msg.projectLaunchPlan.executionReceipt.readBackVerified ? 'verified' : 'failed' }}</strong>
                      </p>
                      <ul>
                        <li v-for="command in msg.projectLaunchPlan.executionReceipt.commands" :key="command.commandId">
                          <span><strong>{{ command.adapterId }}</strong> · {{ command.status }}</span>
                          <small>{{ command.summary }}</small>
                          <button v-if="command.deepLink" type="button" @click="openLaunchLink(command.deepLink)">Mở</button>
                        </li>
                      </ul>
                    </section>

                    <section v-if="msg.projectLaunchPlan.latestReplanProposal" class="launch-replan" data-testid="project-replan-proposal">
                      <h4>Replan proposal rev {{ msg.projectLaunchPlan.latestReplanProposal.revision }} · chỉ review</h4>
                      <p>Qaly không tự sửa Task, assignee hoặc deadline.</p>
                      <ul>
                        <li v-for="change in msg.projectLaunchPlan.latestReplanProposal.changes" :key="`${change.changeType}-${change.summary}`">
                          <strong>{{ change.severity }} · {{ change.summary }}</strong>
                          <small>Baseline {{ change.baselineValue }} → hiện tại {{ change.currentValue }}</small>
                          <p>{{ change.suggestedAction }}</p>
                        </li>
                      </ul>
                    </section>

                    <footer class="launch-plan-actions">
                      <div>
                        <span>{{ msg.projectLaunchPlan.actualProvider }} / {{ msg.projectLaunchPlan.actualModel }}</span>
                        <small>{{ msg.projectLaunchPlan.scoringVersion }}</small>
                      </div>
                      <button
                        v-if="!msg.projectLaunchPlan.executionReceipt"
                        type="button"
                        class="launch-primary-action"
                        :disabled="Boolean(launchActionBusy) || Boolean(msg.projectLaunchPlan.blockingReasons.length)"
                        data-testid="project-launch-confirm"
                        @click="confirmLaunchPlan(msg, msg.projectLaunchPlan)"
                      >{{ launchActionBusy === msg.projectLaunchPlan.planId ? 'Đang thực thi…' : 'Xác nhận tạo Project' }}</button>
                      <template v-else>
                        <button type="button" :disabled="Boolean(launchActionBusy)" data-testid="project-launch-monitor" @click="monitorLaunch(msg, msg.projectLaunchPlan)">Monitor ngay</button>
                        <button
                          v-if="msg.projectLaunchPlan.executionReceipt.rollbackAvailable"
                          type="button"
                          class="launch-danger-action"
                          :disabled="Boolean(launchActionBusy)"
                          data-testid="project-launch-rollback"
                          @click="rollbackLaunch(msg, msg.projectLaunchPlan)"
                        >Rollback launch</button>
                        <button type="button" @click="openLaunchLink(`/projects/${msg.projectLaunchPlan.executionReceipt.projectId}`)">Mở Project</button>
                      </template>
                    </footer>
                  </article>

                  <article
                    v-if="msg.researchPlan"
                    class="research-plan-card"
                    data-testid="assistant-research-plan"
                  >
                    <header class="research-plan-header">
                      <div>
                        <span class="research-plan-kicker">Research Plan · có kiểm chứng</span>
                        <h3>{{ msg.researchPlan.objective }}</h3>
                        <p>{{ msg.researchPlan.scope.label }}</p>
                      </div>
                      <span class="research-plan-model">
                        {{ msg.researchPlan.actualProvider }} / {{ msg.researchPlan.actualModel }}
                      </span>
                    </header>

                    <section class="research-plan-section">
                      <h4>Facts từ dữ liệu được cấp quyền</h4>
                      <p v-if="!msg.researchPlan.findings.length" class="research-plan-empty">
                        Chưa đủ dữ liệu để khẳng định fact; xem mục Unknowns bên dưới.
                      </p>
                      <ol v-else class="research-finding-list">
                        <li v-for="finding in msg.researchPlan.findings" :key="finding.findingId">
                          <div class="research-row-title">
                            <span :class="`research-severity severity-${finding.severity}`">{{ finding.severity }}</span>
                            <strong>{{ Math.round(finding.confidence * 100) }}%</strong>
                          </div>
                          <p>{{ finding.statement }}</p>
                          <div class="research-source-list">
                            <code
                              v-for="sourceRef in finding.sourceRefs"
                              :key="sourceRef"
                              :title="sourceRef"
                            >{{ compactSourceRef(sourceRef) }}</code>
                          </div>
                        </li>
                      </ol>
                    </section>

                    <div class="research-plan-columns">
                      <section class="research-plan-section">
                        <h4>Unknowns cần làm rõ</h4>
                        <p v-if="!msg.researchPlan.unknowns.length" class="research-plan-empty">Không có unknown được ghi nhận.</p>
                        <ul v-else class="research-plain-list">
                          <li v-for="unknown in msg.researchPlan.unknowns" :key="unknown.unknownId">
                            <span v-if="unknown.blocking" class="research-blocking">Chặn</span>
                            {{ unknown.question }}
                          </li>
                        </ul>
                      </section>
                      <section class="research-plan-section">
                        <h4>Assumptions — không phải fact</h4>
                        <p v-if="!msg.researchPlan.assumptions.length" class="research-plan-empty">Không dùng giả định bổ sung.</p>
                        <ul v-else class="research-plain-list">
                          <li v-for="assumption in msg.researchPlan.assumptions" :key="assumption">{{ assumption }}</li>
                        </ul>
                      </section>
                    </div>

                    <section class="research-plan-section">
                      <h4>Phương án và trade-off</h4>
                      <div class="research-option-grid">
                        <article
                          v-for="option in msg.researchPlan.options"
                          :key="option.optionId"
                          class="research-option"
                          :class="{ recommended: option.optionId === msg.researchPlan.recommendedOptionId }"
                        >
                          <span v-if="option.optionId === msg.researchPlan.recommendedOptionId">Khuyến nghị</span>
                          <strong>{{ option.title }}</strong>
                          <p>{{ option.outcome }}</p>
                          <ul>
                            <li v-for="tradeOff in option.tradeOffs" :key="tradeOff">{{ tradeOff }}</li>
                          </ul>
                          <small>Effort: {{ option.estimatedEffort }} · Risk: {{ option.risk }}</small>
                        </article>
                      </div>
                      <p class="research-rationale">{{ msg.researchPlan.recommendationRationale }}</p>
                    </section>

                    <section v-if="msg.researchPlan.proposedActions.length" class="research-plan-section">
                      <h4>Action graph — chưa tự động thực thi</h4>
                      <div class="research-action-list">
                        <article
                          v-for="action in msg.researchPlan.proposedActions"
                          :key="action.actionId"
                          class="research-action"
                          :data-testid="`research-action-${action.actionId}`"
                        >
                          <div>
                            <strong>{{ action.title }}</strong>
                            <code>{{ action.capabilityId }}</code>
                            <small v-if="action.dependencyIds.length">Sau: {{ action.dependencyIds.join(', ') }}</small>
                          </div>
                          <button
                            v-if="action.executionEligible && action.capabilityId === 'task.create.v1'"
                            type="button"
                            class="erumi-draft-btn-confirm"
                            data-testid="research-action-open-draft"
                            @click="openResearchAction(action)"
                          >
                            Mở bản nháp task
                          </button>
                          <span v-else class="research-action-unavailable">Chưa thể áp dụng tự động</span>
                        </article>
                      </div>
                    </section>

                    <details v-if="msg.researchPlan.warnings.length" class="research-warnings">
                      <summary>Cảnh báo và giới hạn ({{ msg.researchPlan.warnings.length }})</summary>
                      <ul>
                        <li v-for="warning in msg.researchPlan.warnings" :key="warning">{{ warning }}</li>
                      </ul>
                    </details>
                    <details v-if="msg.researchPlan.privacyNotes.length" class="research-warnings">
                      <summary>Privacy & freshness · {{ new Date(msg.researchPlan.freshnessAt).toLocaleString('vi-VN') }}</summary>
                      <ul>
                        <li v-for="note in msg.researchPlan.privacyNotes" :key="note">{{ note }}</li>
                      </ul>
                    </details>
                  </article>

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
                    <template v-for="(action, actionIndex) in msg.actions" :key="`${action.type}-${action.label}-${actionIndex}`">
                      <div v-if="action.type === 'assistant_clarification'" class="erumi-draft-card clarification-card">
                        <strong>{{ action.payload?.prompt || action.label }}</strong>
                        <p class="erumi-draft-text">Chọn một dự án để Trợ lý AI tiếp tục. Việc chọn này chưa thay đổi dữ liệu.</p>
                        <div class="clarification-choices">
                          <button
                            v-for="choice in action.payload?.choices || []"
                            :key="choice.id"
                            type="button"
                            class="clarification-choice"
                            @click="answerProjectClarification(choice, action)"
                          >
                            <span>{{ choice.label }}</span>
                            <small v-if="choice.description">{{ choice.description }}</small>
                          </button>
                        </div>
                      </div>
                      <div v-else-if="action.type === 'assistant_progressive_questions'" class="assistant-progressive-card">
                        <section class="assistant-progressive-questions">
                          <header>
                            <strong>{{ pendingProgressiveQuestions(action.payload?.questions, action).length ? 'Mình cần biết thêm' : 'Đã đủ câu trả lời cần thiết' }}</strong>
                            <span>{{ clarificationDraftSaving ? 'Đang lưu nháp…' : 'Câu trả lời được lưu trên máy chủ' }}</span>
                          </header>
                          <article
                            v-for="question in filteredProgressiveQuestions(action.payload?.questions, action)"
                            :key="question.id"
                            :class="{ answered: Boolean(progressiveAnswer(question.id, action)?.value.trim()) }"
                          >
                            <strong>{{ question.text }}</strong>
                            <p>{{ question.reason }}</p>
                            <div v-if="question.quickReplies?.length" class="assistant-quick-replies">
                              <button
                                v-for="reply in question.quickReplies"
                                :key="reply.value"
                                type="button"
                                :class="{ selected: progressiveAnswer(question.id, action)?.value === reply.value }"
                                :aria-pressed="progressiveAnswer(question.id, action)?.value === reply.value"
                                @click="answerProgressiveQuestion(question, reply, action)"
                              >{{ reply.label }}</button>
                            </div>
                            <div v-if="question.allowFreeText" class="assistant-free-answer">
                              <input
                                :id="`clarification-${question.id}`"
                                type="text"
                                :value="progressiveAnswer(question.id, action)?.value || ''"
                                :aria-label="`Trả lời: ${question.text}`"
                                placeholder="Nhập câu trả lời…"
                                @focus="answerProgressiveWithComposer(question, action)"
                                @input="updateProgressiveFreeText(question, action, $event, false)"
                                @change="updateProgressiveFreeText(question, action, $event, true)"
                              />
                              <button
                                v-if="progressiveAnswer(question.id, action)"
                                type="button"
                                @click="setProgressiveAnswer(question, '', null, action)"
                              >Xóa</button>
                            </div>
                          </article>
                          <footer class="assistant-progressive-actions">
                            <button type="button" :disabled="!progressiveDraft" @click="clearProgressiveDraft">Xóa bản nháp</button>
                            <button
                              type="button"
                              class="launch-primary-action"
                              :disabled="!hasAllBlockingAnswers(action) || clarificationDraftSaving || isChatting"
                              @click="submitProgressiveDraft(action)"
                            >Gửi tất cả câu trả lời</button>
                          </footer>
                        </section>
                        <details v-if="action.payload?.guidance && !hasAllBlockingAnswers(action)" class="assistant-manual-guidance">
                          <summary>Cách làm tạm thời</summary>
                          <p>{{ action.payload.guidance.summary }}</p>
                          <ol>
                            <li v-for="step in action.payload.guidance.steps" :key="`${step.sequence}-${step.route}`">
                              <button type="button" @click="openGuidanceRoute(step.route)">
                                <span>{{ step.sequence }}. {{ step.label }}</span>
                                <small>{{ step.route }}</small>
                              </button>
                            </li>
                          </ol>
                        </details>
                        <details v-if="action.payload?.capabilityGap" class="assistant-capability-gap">
                          <summary>Chưa thể tự thực hiện trực tiếp</summary>
                          <p>{{ action.payload.capabilityGap.userMessage }}</p>
                        </details>
                      </div>
                      <button
                        v-else-if="action.type === 'assistant_resume_turn'"
                        type="button"
                        class="erumi-action-button"
                        @click="resumeAssistantTurn(action)"
                      >{{ action.label }}</button>
                      <button
                        v-else-if="action.type === 'assistant_navigation'"
                        type="button"
                        class="assistant-navigation-action"
                        :aria-label="`${action.label}: ${action.payload?.description || 'Mở trang'}`"
                        @click="openAssistantNavigation(action)"
                      >
                        <span>
                          <strong>{{ action.label }}</strong>
                          <small v-if="action.payload?.description">{{ action.payload.description }}</small>
                        </span>
                        <ArrowRight :size="18" aria-hidden="true" />
                      </button>
                      <div v-else-if="action.type === 'compose_task_plan'" class="erumi-draft-card">
                        <p class="erumi-draft-text">Yêu cầu đã được định tuyến sang Task Action Composer. AI chỉ soạn option; bạn vẫn kiểm tra và xác nhận trước khi tạo task.</p>
                        <div class="erumi-draft-buttons">
                          <button type="button" class="erumi-draft-btn-confirm" @click="openComposerAction(action)">
                            Mở phương án task
                          </button>
                        </div>
                      </div>
                      <div v-else-if="action.type === 'draft_change'" class="erumi-draft-card">
                        <p class="erumi-draft-text">
                          Luồng nháp legacy chỉ được giữ để đọc lịch sử. Hãy tạo yêu cầu mới để dùng preview, chỉnh sửa và xác nhận theo contract hiện tại.
                        </p>
                        <ol v-if="action.payload?.events?.length" class="erumi-run-progress" aria-label="Tiến trình agent">
                          <li v-for="event in action.payload.events" :key="`${event.type}-${event.at}`">
                            <span class="erumi-run-check" aria-hidden="true">✓</span>
                            <span>{{ event.message }}</span>
                          </li>
                        </ol>
                        <p class="erumi-draft-note">Không thể xác nhận trực tiếp từ card cũ vì thiếu row version, editable payload và execution receipt.</p>
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
              <button
                v-if="activeAssistantTurnId"
                type="button"
                class="assistant-cancel-turn"
                @click="cancelActiveAssistantTurn"
              >Hủy an toàn</button>
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
              aria-label="Nhập yêu cầu tiếp theo cho Trợ lý AI"
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
          :current-session-id="assistantSessionId"
          :loading="conversationHistoryLoading"
          @create="startNewConversation"
          @restore="restoreHistoryItem"
          @rename="renameHistoryItem"
          @archive="archiveHistoryItem"
          @delete="deleteHistoryItem"
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
          <button
            v-if="activeDrawerTab === 'settings'"
            type="button"
            class="analytics-settings-link"
            @click="router.push('/settings?tab=privacy')"
          >Mở thiết lập quyền riêng tư và dữ liệu AI</button>
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
  overflow: hidden;
  display: flex;
}

.empty-inner {
  flex: 1 1 auto;
  margin: 0 auto;
  width: 100%;
  height: 100%;
  max-width: 680px;
  min-height: 0;
  padding: 32px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 20px;
  overflow-y: auto;
}

.empty-avatar {
  margin-top: auto;
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
  margin: 0 0 auto;
  font-size: 26px;
  font-weight: 700;
  color: var(--text-strong);
  letter-spacing: -0.4px;
  line-height: 1.25;
  text-align: center;
}

.chat-empty .composer {
  position: sticky;
  z-index: 5;
  bottom: 0;
  flex: 0 0 auto;
  margin-top: 24px;
  padding-top: 12px;
  background: linear-gradient(to bottom, transparent, var(--bg) 18px);
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

.assistant-process {
  display: grid;
  gap: 6px;
  margin: 0;
  padding: 10px 12px;
  list-style: none;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: color-mix(in srgb, var(--surface) 88%, var(--primary) 12%);
  color: var(--muted);
  font-size: 12px;
}

.assistant-process li {
  display: grid;
  grid-template-columns: 10px minmax(0, 1fr) auto;
  gap: 8px;
  align-items: center;
}

.assistant-process li.status-completed {
  color: var(--text);
}

.assistant-process li.status-failed {
  color: var(--danger, #dc2626);
}

.assistant-process-dot {
  width: 7px;
  height: 7px;
  border-radius: 999px;
  background: currentColor;
  opacity: 0.7;
}

.assistant-process li.status-running .assistant-process-dot {
  animation: assistant-process-pulse 1.2s ease-in-out infinite;
}

.assistant-process-disclosure {
  padding: 8px 12px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--muted);
  font-size: 12px;
  margin-bottom: 8px;
}

.assistant-process-disclosure summary {
  cursor: pointer;
  color: var(--muted);
  font-weight: 600;
  user-select: none;
}

.assistant-process-disclosure summary:hover {
  color: var(--text);
}

.assistant-context-disclosure {
  padding: 9px 12px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--muted);
  font-size: 12px;
}

.assistant-context-disclosure summary {
  cursor: pointer;
  color: var(--text);
  font-weight: 700;
}

.assistant-capability-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 10px;
}

.assistant-capability-list span {
  padding: 2px 7px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--primary) 12%, transparent);
  color: var(--primary);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}

.assistant-source-list {
  display: grid;
  gap: 7px;
  margin: 10px 0 0;
  padding: 0;
  list-style: none;
}

.assistant-source-list li {
  display: grid;
  grid-template-columns: minmax(110px, auto) minmax(0, 1fr);
  gap: 8px;
}

.assistant-source-list li.source-denied {
  color: var(--danger, #dc2626);
}

.assistant-source-list li.source-skipped {
  opacity: 0.78;
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

.chat-title-wrap { min-width: 0; display: grid; gap: 2px; }
.chat-session-state { color: var(--muted); font-size: 10px; }
.chat-header-actions { display: flex; align-items: center; gap: 7px; }
.session-history-btn,
.empty-session-history { display: inline-flex; align-items: center; gap: 6px; border: 1px solid var(--line); border-radius: 9px; background: var(--panel); color: var(--text); padding: 7px 10px; font: inherit; font-size: 12px; font-weight: 650; cursor: pointer; }
.session-history-btn:hover,
.empty-session-history:hover { border-color: color-mix(in srgb, var(--primary) 42%, var(--line)); color: var(--primary); }
.empty-session-history { position: absolute; z-index: 6; top: 14px; right: 18px; }

.assistant-navigation-action {
  flex: 1 1 230px;
  min-width: min(230px, 100%);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 12px 14px;
  border: 1px solid var(--line);
  border-radius: 11px;
  background: var(--panel);
  color: var(--text-strong);
  text-align: left;
  cursor: pointer;
  transition: border-color 0.16s ease, background-color 0.16s ease, transform 0.16s ease;
}

.assistant-navigation-action > span {
  display: grid;
  gap: 4px;
}

.assistant-navigation-action strong {
  font-size: 13px;
}

.assistant-navigation-action small {
  color: var(--muted);
  font-size: 11px;
  line-height: 1.4;
}

.assistant-navigation-action > svg {
  flex: 0 0 auto;
  color: var(--primary-strong);
}

.assistant-navigation-action:hover,
.assistant-navigation-action:focus-visible {
  border-color: var(--primary);
  background: color-mix(in srgb, var(--primary) 6%, var(--panel));
  transform: translateY(-1px);
  outline: none;
}

.erumi-draft-card {
  background: var(--panel-soft);
  border: 1px dashed var(--line);
  padding: 12px 16px;
  border-radius: 10px;
  width: 100%;
}

.clarification-card {
  display: grid;
  gap: 10px;
  border-style: solid;
  border-color: color-mix(in srgb, var(--primary) 32%, var(--line));
}

.clarification-choices {
  display: grid;
  gap: 8px;
}

.clarification-choice {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--line);
  border-radius: 10px;
  color: var(--text);
  background: var(--panel);
  text-align: left;
  cursor: pointer;
}

.clarification-choice:hover,
.clarification-choice:focus-visible {
  border-color: var(--primary);
  background: color-mix(in srgb, var(--primary) 7%, var(--panel));
  outline: none;
}

.clarification-choice small {
  color: var(--muted);
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

.assistant-work-plan-card {
  margin: 12px 0 16px;
  padding: 16px;
  border: 1px solid #b8c8e8;
  border-radius: 14px;
  background: linear-gradient(145deg, rgba(239, 246, 255, 0.96), rgba(248, 250, 252, 0.98));
  color: #17233d;
}

.assistant-primary-answer { margin-bottom: 14px; }

.assistant-work-plan-header,
.assistant-work-plan-meta,
.assistant-selected-skill,
.assistant-missing-skill,
.assistant-work-plan-steps li {
  display: flex;
  gap: 10px;
}

.assistant-work-plan-header { justify-content: space-between; align-items: flex-start; }
.assistant-work-plan-header span,
.assistant-selected-skill > span,
.assistant-missing-skill > span { color: #2563eb; font-size: 12px; font-weight: 800; text-transform: uppercase; }
.assistant-work-plan-header h3 { margin: 3px 0; font-size: 16px; }
.assistant-work-plan-header p,
.assistant-selected-skill p,
.assistant-missing-skill p { margin: 0; color: #596780; font-size: 13px; }
.assistant-work-plan-meta { flex-wrap: wrap; margin: 12px 0; }
.assistant-work-plan-meta span { padding: 4px 8px; border-radius: 999px; background: #dfeafe; font-size: 11px; }
.assistant-selected-skill,
.assistant-missing-skill { flex-wrap: wrap; align-items: center; padding: 10px; border-radius: 10px; background: rgba(255,255,255,.72); }
.assistant-selected-skill p,
.assistant-missing-skill p { flex-basis: 100%; }
.assistant-missing-skill { border-left: 3px solid #f59e0b; }
.assistant-missing-skill { display: block; }
.assistant-missing-skill summary { cursor: pointer; color: #8a5300; font-size: 12px; font-weight: 800; }
.assistant-missing-skill p { margin-top: 8px; }
.assistant-work-plan-steps { margin: 12px 0 0; padding: 0; list-style: none; display: grid; gap: 7px; }
.assistant-work-plan-steps li { align-items: center; padding: 8px; border-radius: 9px; background: rgba(255,255,255,.72); }
.assistant-work-plan-steps li > span { width: 26px; height: 26px; display: grid; place-items: center; border-radius: 50%; background: #2563eb; color: white; font-size: 11px; }
.assistant-work-plan-steps li > div { display: grid; flex: 1; }
.assistant-work-plan-steps small { color: #6b7890; }
.assistant-work-plan-steps em { font-size: 11px; font-style: normal; color: #50617d; }
.assistant-work-plan-warnings { margin-top: 10px; font-size: 12px; color: #7c4a03; }

.research-plan-card {
  display: grid;
  gap: 14px;
  margin-top: 14px;
  padding: 16px;
  border: 1px solid color-mix(in srgb, var(--primary) 30%, var(--line));
  border-radius: 16px;
  background: color-mix(in srgb, var(--panel) 94%, var(--primary) 6%);
}

.research-plan-header,
.research-row-title,
.research-action {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.research-plan-header h3,
.research-plan-section h4 {
  margin: 0;
  color: var(--text-strong);
}

.research-plan-header h3 {
  margin-top: 4px;
  font-size: 17px;
}

.research-plan-header p,
.research-plan-section p,
.research-option p {
  margin: 5px 0 0;
}

.research-plan-kicker,
.research-plan-model,
.research-severity,
.research-blocking,
.research-action-unavailable {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
}

.research-plan-kicker {
  color: var(--primary);
  text-transform: uppercase;
  letter-spacing: .04em;
}

.research-plan-model {
  flex: 0 0 auto;
  padding: 5px 8px;
  background: var(--panel-soft);
  color: var(--muted);
}

.research-plan-columns,
.research-option-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.research-plan-section {
  display: grid;
  gap: 9px;
}

.research-plan-section h4 {
  font-size: 13px;
}

.research-finding-list,
.research-plain-list,
.research-option ul,
.research-warnings ul {
  margin: 0;
  padding-left: 18px;
}

.research-finding-list {
  display: grid;
  gap: 8px;
  list-style: none;
  padding: 0;
}

.research-finding-list > li,
.research-plan-columns > section,
.research-option,
.research-action {
  padding: 11px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--panel);
}

.research-severity {
  padding: 3px 7px;
  text-transform: uppercase;
  background: var(--panel-soft);
  color: var(--muted);
}

.research-severity.severity-high,
.research-severity.severity-critical {
  background: color-mix(in srgb, var(--danger, #dc2626) 12%, transparent);
  color: var(--danger, #dc2626);
}

.research-severity.severity-medium {
  background: color-mix(in srgb, var(--warning, #d97706) 14%, transparent);
  color: var(--warning-dark, #92400e);
}

.research-source-list {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  margin-top: 7px;
}

.research-source-list code,
.research-action code {
  padding: 3px 6px;
  border-radius: 6px;
  background: var(--panel-soft);
  color: var(--muted);
  font-size: 10px;
}

.research-blocking {
  margin-right: 4px;
  padding: 2px 6px;
  background: color-mix(in srgb, var(--danger, #dc2626) 12%, transparent);
  color: var(--danger, #dc2626);
}

.research-option {
  display: grid;
  gap: 6px;
}

.research-option.recommended {
  border-color: color-mix(in srgb, var(--primary) 55%, var(--line));
  box-shadow: inset 3px 0 var(--primary);
}

.research-option > span {
  color: var(--primary);
  font-size: 11px;
  font-weight: 700;
}

.research-option small,
.research-action small,
.research-plan-empty {
  color: var(--muted);
}

.research-rationale {
  padding: 10px;
  border-left: 3px solid var(--primary);
  background: color-mix(in srgb, var(--primary) 8%, transparent);
}

.research-action-list {
  display: grid;
  gap: 8px;
}

.research-action > div {
  display: grid;
  gap: 5px;
}

.research-action-unavailable {
  flex: 0 0 auto;
  padding: 5px 8px;
  background: var(--panel-soft);
  color: var(--muted);
}

.research-warnings {
  color: var(--muted);
  font-size: 12px;
}

@media (max-width: 820px) {
  .research-plan-columns,
  .research-option-grid {
    grid-template-columns: 1fr;
  }

  .research-plan-header,
  .research-action {
    flex-direction: column;
  }
}

/* Dark-mode metric tone tweaks */
:global(:root[data-theme='dark'] .erumi-metric.tone-good),
:global(:root[data-theme='dark'] .analytics-drawer-metric.tone-good) {
  background: rgba(34, 197, 94, 0.12);
  border-color: rgba(34, 197, 94, 0.4);
  color: #86efac;
}

:global(:root[data-theme='dark'] .erumi-metric.tone-warning),
:global(:root[data-theme='dark'] .analytics-drawer-metric.tone-warning) {
  background: rgba(245, 158, 11, 0.12);
  border-color: rgba(245, 158, 11, 0.4);
  color: #fcd34d;
}

:global(:root[data-theme='dark'] .erumi-metric.tone-danger),
:global(:root[data-theme='dark'] .analytics-drawer-metric.tone-danger) {
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

.project-launch-brief-card,
.project-launch-plan-card,
.assistant-progressive-card {
  border: 1px solid var(--border);
  border-radius: 14px;
  background: var(--surface);
  overflow: hidden;
}

.project-launch-brief-header,
.assistant-progressive-questions > header,
.assistant-manual-guidance > header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
  background: color-mix(in srgb, var(--surface) 88%, var(--primary) 12%);
}

.project-launch-brief-header h3,
.project-launch-brief-header p { margin: 3px 0 0; }
.project-launch-brief-header span,
.project-launch-status small { color: var(--muted); font-size: 11px; }
.project-launch-status { display: grid; justify-items: end; align-content: start; }
.project-launch-rulebook { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; padding: 9px 16px; border-top: 1px solid var(--border); border-bottom: 1px solid var(--border); font-size: 12px; }
.project-launch-rulebook button { margin-left: auto; border: 1px solid currentColor; border-radius: 8px; background: var(--surface); color: inherit; padding: 6px 9px; cursor: pointer; }
.project-launch-rulebook button:disabled { opacity: .55; cursor: not-allowed; }
.project-launch-rulebook.status-policy_missing { color: #d97706; background: color-mix(in srgb, #f59e0b 15%, var(--surface)); }
.project-launch-columns { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; padding: 14px 16px; }
.project-launch-columns section { padding: 10px; border: 1px solid var(--border); border-radius: 10px; }
.project-launch-columns h4,
.project-launch-columns p,
.project-launch-columns ul { margin: 0; }
.project-launch-columns ul { padding-left: 18px; }
.project-launch-decisions { margin: 0 16px 14px; }
.project-launch-decisions summary { cursor: pointer; font-weight: 700; }
.project-launch-decisions ul { display: grid; gap: 7px; list-style: none; padding: 8px 0 0; }
.project-launch-decisions li { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 3px 10px; padding: 9px; border: 1px solid var(--border); border-radius: 9px; }
.project-launch-decisions li p { grid-column: 1 / -1; margin: 0; color: var(--muted); }
.project-launch-decisions .decision-block { border-color: color-mix(in srgb, #ef4444 35%, var(--border)); }
.project-launch-decisions .decision-unknown { border-color: color-mix(in srgb, #f59e0b 35%, var(--border)); }
.project-launch-brief-card > footer { display: flex; flex-wrap: wrap; gap: 8px 14px; padding: 10px 16px; border-top: 1px solid var(--border); color: var(--muted); font-size: 11px; }
.safe-test-suite-list { display: grid; gap: 8px; margin: 0; padding: 14px 16px; list-style: none; }
.safe-test-suite-list li { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 9px 10px; border: 1px solid var(--border); border-radius: 9px; }
.safe-test-suite-list li div { display: grid; min-width: 0; }
.safe-test-suite-list small,
.safe-test-suite-list span { color: var(--muted); font-size: 11px; }
.safe-test-events { padding-top: 0; }
.safe-test-events .status-passed { border-color: #10b981; }
.safe-test-events .status-failed { border-color: #ef4444; }
.safe-test-summary { margin: 0; padding: 0 16px 12px; }
.safe-test-confirm { margin: 0 16px 14px auto; display: flex; }
.project-launch-plan-card { border: 1px solid color-mix(in srgb, var(--primary) 40%, var(--border)); border-radius: 14px; background: var(--surface); overflow: hidden; }
.project-launch-plan-header { display: flex; justify-content: space-between; gap: 16px; padding: 14px 16px; background: color-mix(in srgb, var(--surface) 82%, var(--primary) 18%); }
.project-launch-plan-header h3,
.project-launch-plan-header p { margin: 3px 0 0; }
.project-launch-plan-header span { color: var(--muted); font-size: 11px; }
.launch-blocking-list,
.launch-warning-list { padding: 10px 16px; border-top: 1px solid var(--border); font-size: 12px; }
.launch-blocking-list { color: #dc2626; background: color-mix(in srgb, #ef4444 15%, var(--surface)); }
.launch-warning-list { color: #d97706; background: color-mix(in srgb, #f59e0b 15%, var(--surface)); }
.launch-blocking-list ul,
.launch-warning-list ul { margin: 5px 0 0; padding-left: 18px; }
.launch-plan-section,
.launch-receipt,
.launch-replan { padding: 14px 16px; border-top: 1px solid var(--border); }
.launch-plan-section > h4,
.launch-receipt > h4,
.launch-replan > h4 { margin: 0 0 10px; }
.launch-scenario-list { display: grid; gap: 10px; }
.launch-scenario { padding: 11px; border: 1px solid var(--border); border-radius: 10px; background: color-mix(in srgb, var(--surface) 96%, var(--muted) 4%); }
.launch-scenario.feasible { border-color: #86efac; }
.launch-scenario.selected { box-shadow: 0 0 0 2px color-mix(in srgb, var(--primary) 35%, transparent); }
.launch-scenario > label { display: flex; gap: 9px; cursor: pointer; }
.launch-scenario > label span { display: grid; }
.launch-scenario > label small,
.launch-member-list small,
.launch-sprint small,
.launch-receipt small,
.launch-replan small,
.launch-plan-actions small { color: var(--muted); }
.launch-scenario > p { margin: 7px 0; }
.launch-member-list { display: grid; gap: 6px; padding: 0; list-style: none; }
.launch-member-list li { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 2px 10px; padding: 7px; border-radius: 8px; background: color-mix(in srgb, var(--surface) 88%, var(--primary) 12%); }
.launch-member-list small { grid-column: 1 / -1; }
.launch-sprint { margin-top: 8px; padding: 9px; border: 1px solid var(--border); border-radius: 9px; }
.launch-sprint summary { cursor: pointer; font-weight: 700; }
.launch-sprint > p { margin: 7px 0; color: var(--muted); }
.launch-sprint ol { display: grid; gap: 6px; padding-left: 22px; }
.launch-sprint li { padding-left: 3px; }
.launch-sprint li span { float: right; margin-left: 10px; color: var(--muted); }
.launch-sprint li small { display: block; }
.launch-plan-range { color: var(--muted); }
.launch-external-deferred { margin-top: 10px; color: var(--muted); }
.launch-receipt > ul,
.launch-replan > ul { display: grid; gap: 7px; padding: 0; list-style: none; }
.launch-receipt li,
.launch-replan li { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 3px 9px; padding: 9px; border: 1px solid var(--border); border-radius: 9px; }
.launch-receipt li small,
.launch-replan li small,
.launch-replan li p { grid-column: 1 / -1; margin: 0; }
.launch-receipt button { border: 0; background: none; color: var(--primary); cursor: pointer; }
.launch-replan { background: color-mix(in srgb, var(--surface) 90%, #f59e0b 10%); }
.launch-plan-actions { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 8px; padding: 12px 16px; border-top: 1px solid var(--border); }
.launch-plan-actions > div { display: grid; margin-right: auto; }
.launch-plan-actions button,
.launch-primary-action { border: 1px solid var(--border); border-radius: 9px; padding: 8px 11px; background: var(--surface); color: var(--text); cursor: pointer; }
.launch-plan-actions button:disabled,
.launch-primary-action:disabled { opacity: .55; cursor: not-allowed; }
.launch-primary-action { border-color: var(--primary); background: var(--primary); color: white; }
.launch-danger-action { border-color: #fecaca !important; color: #b91c1c !important; }

.assistant-progressive-card { display: grid; gap: 0; }
.assistant-progressive-questions > article,
.assistant-manual-guidance { padding: 12px 16px; border-top: 1px solid var(--border); }
.assistant-progressive-questions article > p,
.assistant-manual-guidance > p { margin: 3px 0 8px; color: var(--muted); font-size: 12px; }
.assistant-quick-replies { display: flex; flex-wrap: wrap; gap: 7px; }
.assistant-quick-replies button,
.assistant-free-text,
.assistant-cancel-turn { border: 1px solid var(--border); border-radius: 999px; background: var(--surface); color: var(--text); padding: 6px 10px; cursor: pointer; }
.assistant-quick-replies button.selected { border-color: var(--primary); background: color-mix(in srgb, var(--primary) 14%, var(--surface)); color: var(--primary); }
.assistant-free-text { margin-top: 7px; color: var(--primary); }
.assistant-free-answer { display: flex; gap: 7px; margin-top: 8px; }
.assistant-free-answer input { min-width: 0; flex: 1; border: 1px solid var(--border); border-radius: 9px; background: var(--surface); color: var(--text); padding: 8px 10px; }
.assistant-free-answer input:focus { outline: 2px solid color-mix(in srgb, var(--primary) 35%, transparent); border-color: var(--primary); }
.assistant-free-answer button,
.assistant-progressive-actions > button { border: 1px solid var(--border); border-radius: 9px; background: var(--surface); color: var(--text); padding: 7px 10px; cursor: pointer; }
.assistant-progressive-actions { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 16px; border-top: 1px solid var(--border); }
.assistant-progressive-actions button:disabled { opacity: .55; cursor: not-allowed; }
.assistant-manual-guidance ol { display: grid; gap: 7px; padding: 0; list-style: none; }
.assistant-manual-guidance li button { width: 100%; display: flex; justify-content: space-between; gap: 10px; border: 1px solid var(--border); border-radius: 9px; background: var(--surface); padding: 9px 10px; text-align: left; cursor: pointer; }
.assistant-manual-guidance li small { color: var(--muted); }
.assistant-capability-gap { margin: 0 16px 14px; color: var(--muted); }
.composer-question-context { display: flex; justify-content: space-between; gap: 8px; padding: 7px 10px; color: var(--primary); font-size: 12px; border-bottom: 1px solid var(--border); }
.composer-question-context button { border: 0; background: none; color: var(--muted); cursor: pointer; }
.assistant-cancel-turn { margin-top: 8px; color: var(--danger, #dc2626); }

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

  .project-launch-columns { grid-template-columns: 1fr; }
}
</style>
