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
  Gauge,
  ListChecks,
  ShieldCheck,
  Wrench,
  X,
  ArrowRight
} from 'lucide-vue-next'
import { useDashboardContext } from '../../composables/dashboard-context'
import { useErumiContext } from '../../composables/use-erumi-context'
import { apiJson, apiResult } from '../../utils/api-client'
import { showError, showInfo, showSuccess } from '../../composables/use-toast'
import { normalizeAssistantProjectTarget, resolveAssistantProjectId } from './assistant-project-context'
import { cloneAssistantJson } from './assistant-render-normalization'
import {
  applyLaunchMetricDefaults,
  launchMetricIntentLabel,
  launchMetricTemplates,
  launchMetricValueExample,
  normalizeLaunchAudience,
  normalizeLaunchTimebox,
  simplifyLaunchObjective,
  simplifyLaunchProblem,
  suggestLaunchBusinessValue,
} from './project-launch-form-ux'
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
  composeAction: [payload: { message: string; projectId: string; providerHint: string; modelProfile: string }]
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
  rowAction?: {
    label: string
    routeKey?: string | null
  } | null
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
  inputType?: 'text' | 'textarea' | 'select' | string
  placeholder?: string | null
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

type ProjectLaunchBriefDraft = {
  projectName: string
  objective: string
  targetTimebox: string
  primaryAudience: string
  objectiveProfile: ProjectLaunchObjectiveProfile
  features: ProjectLaunchFeature[]
  successMeasures: string
  exclusions: string
  customFeatureTitle: string
}
type ProjectObjectiveMetric = {
  metricId: string
  title: string
  metricType: string
  baseline?: number | null
  target?: number | null
  unit?: string | null
  measurementWindow?: string | null
  dataSource?: string | null
  owner?: string | null
  status: string
}
type ProjectLaunchObjectiveProfile = {
  problemStatement: string
  primaryAudience: string
  desiredOutcome: string
  businessValue: string
  metrics: ProjectObjectiveMetric[]
  guardrails: string[]
  assumptions: string[]
  nonGoals: string[]
}
type ProjectLaunchFeature = {
  featureId: string
  title: string
  category: string
  priority: string
  description: string
  primaryAudience: string
  acceptanceCriteria: string[]
  requiredSkillNames: string[]
  selected: boolean
  custom: boolean
}
type ProjectLaunchSkillOption = {
  skillId: string
  name: string
  category: string
  defaultRequiredLevel: string
  organizationDefined: boolean
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
  targetTimebox?: string | null
  primaryAudience?: string | null
  objectiveProfile?: ProjectLaunchObjectiveProfile | null
  features?: ProjectLaunchFeature[] | null
  skillCatalog?: ProjectLaunchSkillOption[] | null
}
type OrganizationWorkRule = {
  ruleKey: string
  category: string
  enforcement: 'block' | 'warn'
  description: string
  numericValue?: number | null
  unit?: string | null
  values?: string[] | null
  enabled: boolean
}
type OrganizationWorkRuleSet = {
  ruleSetId: string
  organizationId: string
  version: number
  status: string
  revision: number
  rules: OrganizationWorkRule[]
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

type ProjectStaffingCandidate = {
  userId: string
  displayName: string
  organizationRole: string
  managerEligible: boolean
  staffingEligible: boolean
  hardRejects: string[]
  evidenceSkills: string[]
  evidenceConfidence: number
  weeklyCapacityHours: number
  windowCapacityHours: number
  existingCommittedHours: number
  focusReserveHours: number
  availableHours: number
  proposedHours: number
  loadAfterPercent: number
  activeProjectCount: number
  timeZoneId: string
  capacityState: string
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
  managerCandidates: ProjectStaffingCandidate[]
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
  description: string
  acceptanceCriteria: string[]
  definitionOfDone: string[]
  priority: string
  estimatedHours: number
  proposedAssigneeId?: string | null
  proposedReviewerId?: string | null
  requiredSkillIds: string[]
  requiredSkillNames: string[]
  dependencyClientIds: string[]
  sourceRefs: string[]
  selected: boolean
  featureId?: string | null
  objectiveMetricIds?: string[] | null
}

type ProjectLaunchSprintPlan = {
  clientId: string
  name: string
  objective: string
  startDate: string
  endDate: string
  exitCriteria: string[]
  tasks: ProjectLaunchTaskPlan[]
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
  sprints: ProjectLaunchSprintPlan[]
  features?: ProjectLaunchFeature[] | null
  objectiveMetrics?: ProjectObjectiveMetric[] | null
  suggestedTeamSize?: number
  assignmentMode?: 'auto_balance' | 'preserve_assignments'
  scheduleMode?: 'sequential_sprints' | 'parallel_workstreams'
}

type ProjectLaunchPlanDraft = {
  selectedScenarioId: string
  staffing: Array<{ userId: string; proposedRole: string; proposedHours: number; included: boolean; manager: boolean }>
  sprints: ProjectLaunchSprintPlan[]
  assignmentMode: 'auto_balance' | 'preserve_assignments'
  scheduleMode: 'sequential_sprints' | 'parallel_workstreams'
  dirty: boolean
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

type AiNativeActionReceipt = {
  schemaId: 'ai_native_action_receipt.v1'
  receiptId: string
  draftId: string
  capabilityId: string
  status: string
  items: Array<{ entityType: string; entityId: string; label: string; url: string }>
  readBackLinks: string[]
  confirmedAt: string
  replayed: boolean
}

type AiNativeActionDraft = {
  draftId: string
  capabilityId: string
  schemaId: string
  rendererId: string
  targetType: string
  targetId: string
  projectId?: string | null
  status: string
  revision: number
  rowVersion: string
  payload: Record<string, any>
  sourceVersion: string
  expiresAt: string
  createdAt: string
  receipt?: AiNativeActionReceipt | null
}

type PortfolioScheduleAlternative = {
  userId: string
  fullName: string
  skillCoveragePercent: number
  remainingHours: number
  tradeOff: string
  evidenceConfidence: number
  loadBeforeHours: number
  capacityHours: number
  blockingReasons?: string[] | null
}

type PortfolioScheduleProposalItem = {
  itemId: string
  taskId: string
  taskTitle: string
  proposedAssigneeId: string
  proposedAssigneeName: string
  proposedStart: string
  proposedDue: string
  skillCoveragePercent: number
  evidenceConfidence: number
  loadBeforeHours: number
  loadAfterHours: number
  capacityHours: number
  dependencyConflicts: string[]
  deadlineRisks: string[]
  blockingReasons: string[]
  alternatives: PortfolioScheduleAlternative[]
  sourceRefs: string[]
  taskRowVersion: string
  selected: boolean
}

type PortfolioScheduleProposal = {
  draftId: string
  projectId: string
  status: string
  scoringVersion: string
  items: PortfolioScheduleProposalItem[]
  sources: Array<{ key: string; label: string; url?: string | null; restricted: boolean }>
  warnings: string[]
  rowVersion: string
  providerName: string
  modelName: string
  receipt?: { appliedCount: number; readBackVerified: boolean; readBackLinks: string[] } | null
}

function normalizePortfolioScheduleProposal(proposal?: PortfolioScheduleProposal | null) {
  if (!proposal) return null
  return {
    ...proposal,
    items: proposal.items.map(item => ({
      ...item,
      blockingReasons: item.blockingReasons ?? [],
      alternatives: item.alternatives.map(candidate => ({
        ...candidate,
        blockingReasons: candidate.blockingReasons ?? [],
      })),
    })),
  }
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
  nativeActionDraft?: AiNativeActionDraft | null
  portfolioScheduleProposal?: PortfolioScheduleProposal | null
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
  disposition: 'grounded_answer' | 'guided_answer' | 'research_plan' | 'project_launch_brief' | 'registered_action' | 'clarification_required' | 'unsupported' | 'unsupported_but_analyzed' | 'policy_blocked' | 'draft_ready' | 'native_action_draft'
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
  nativeActionDraft?: AiNativeActionDraft | null
  portfolioScheduleProposal?: PortfolioScheduleProposal | null
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
  nativeActionDraft?: AiNativeActionDraft | null
  portfolioScheduleProposal?: PortfolioScheduleProposal | null
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
const nativeActionBusy = ref<string | null>(null)
const assignmentProposalBusy = ref<string | null>(null)
const selectedLaunchScenarios = ref<Record<string, string>>({})
const rulebookDrafts = ref<Record<string, OrganizationWorkRuleSet>>({})
const rulebookReviewRules = ref<Record<string, OrganizationWorkRule[]>>({})
const projectLaunchBriefDrafts = ref<Record<string, ProjectLaunchBriefDraft>>({})
const projectLaunchPlanDrafts = ref<Record<string, ProjectLaunchPlanDraft>>({})
const PROJECT_LAUNCH_DRAFT_BACKUP_VERSION = 1

function projectLaunchDraftBackupKey() {
  const userId = String((currentUser.value as any)?.id || (currentUser.value as any)?.userId || 'current')
  return `qaly.ai-native.project-launch-working-drafts.v1:${userId}`
}

function normalizeLaunchBriefForEditing(draft: ProjectLaunchBriefDraft): ProjectLaunchBriefDraft {
  const targetTimebox = normalizeLaunchTimebox(draft.targetTimebox)
  const primaryAudience = normalizeLaunchAudience(draft.primaryAudience)
  const objective = simplifyLaunchObjective(draft.objective)
  return {
    ...draft,
    objective,
    targetTimebox,
    primaryAudience,
    objectiveProfile: {
      ...draft.objectiveProfile,
      problemStatement: simplifyLaunchProblem(draft.objectiveProfile.problemStatement, objective),
      desiredOutcome: objective,
      businessValue: suggestLaunchBusinessValue(draft.objectiveProfile.businessValue, objective),
      primaryAudience: normalizeLaunchAudience(draft.objectiveProfile.primaryAudience) || primaryAudience,
      metrics: draft.objectiveProfile.metrics.map(metric => applyLaunchMetricDefaults(metric, targetTimebox)),
    },
    features: draft.features.map(feature => ({
      ...feature,
      primaryAudience: normalizeLaunchAudience(feature.primaryAudience) || primaryAudience,
    })),
  }
}

function persistProjectLaunchWorkingDrafts() {
  try {
    window.localStorage.setItem(projectLaunchDraftBackupKey(), JSON.stringify({
      version: PROJECT_LAUNCH_DRAFT_BACKUP_VERSION,
      savedAt: new Date().toISOString(),
      briefDrafts: projectLaunchBriefDrafts.value,
      planDrafts: Object.fromEntries(
        Object.entries(projectLaunchPlanDrafts.value).filter(([, draft]) => draft.dirty),
      ),
    }))
  } catch {
    // Server-side review remains authoritative; storage exhaustion must not break chat.
  }
}

function restoreProjectLaunchWorkingDrafts() {
  try {
    const raw = window.localStorage.getItem(projectLaunchDraftBackupKey())
    if (!raw) return
    const saved = JSON.parse(raw) as {
      version?: number
      briefDrafts?: Record<string, ProjectLaunchBriefDraft>
      planDrafts?: Record<string, ProjectLaunchPlanDraft>
    }
    if (saved.version !== PROJECT_LAUNCH_DRAFT_BACKUP_VERSION) return
    projectLaunchBriefDrafts.value = saved.briefDrafts && typeof saved.briefDrafts === 'object'
      ? Object.fromEntries(Object.entries(saved.briefDrafts).map(([id, draft]) => [id, normalizeLaunchBriefForEditing(draft)]))
      : {}
    projectLaunchPlanDrafts.value = saved.planDrafts && typeof saved.planDrafts === 'object'
      ? saved.planDrafts
      : {}
  } catch {
    window.localStorage.removeItem(projectLaunchDraftBackupKey())
  }
}
const launchAudienceOptions = ['Nội bộ', 'Khách hàng', 'Người dùng công khai', 'Đối tác']
const launchTimeboxOptions = ['6 tuần', '8 tuần', '12 tuần']
const launchFeatureTemplates = [
  { title: 'Đăng nhập và phân quyền', category: 'Authentication/RBAC' },
  { title: 'Cổng thông tin khách hàng', category: 'Customer portal' },
  { title: 'Quản trị và vận hành', category: 'Admin/Operations' },
  { title: 'Đặt lịch và điều phối', category: 'Booking/Scheduling' },
  { title: 'Thanh toán và hóa đơn', category: 'Billing/Payment' },
  { title: 'Dashboard và báo cáo', category: 'Dashboard/Reporting' },
  { title: 'Thông báo', category: 'Notification' },
  { title: 'Tìm kiếm và nội dung', category: 'Search/Content' },
  { title: 'Nhật ký và bảo mật', category: 'Audit/Security' },
  { title: 'Tích hợp hệ thống', category: 'Integration' },
  { title: 'Nhập và xuất dữ liệu', category: 'Data import/export' },
]
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
let assistantScopeSyncQueue: Promise<void> = Promise.resolve()

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

function assistantContextForCurrentRoute(organizationId?: string | null) {
  const param = (name: string) => {
    const value = route.params[name]
    return typeof value === 'string' && value.trim() ? value : null
  }
  const routeProjectId = param('projectId')
  // The visible composer selection is the source of truth outside a concrete
  // Project route. externalProjectId is only a launcher input: once reflected
  // into selectedTarget it must never override a later selection by the user.
  const projectId = resolveAssistantProjectId(routeProjectId, selectedTarget.value)
  const taskId = param('taskId')
  const wikiId = param('wikiId')
  const groupId = param('groupId')
  const rawMeetingId = Array.isArray(route.query.meetingId) ? route.query.meetingId[0] : route.query.meetingId
  const meetingId = typeof rawMeetingId === 'string' && rawMeetingId.trim() ? rawMeetingId : null
  const entityType = taskId ? 'task' : wikiId ? 'wiki' : meetingId ? 'meeting' : groupId ? 'group' : projectId ? 'project' : null
  const entityId = taskId || wikiId || meetingId || groupId || projectId
  return {
    route: window.location.pathname,
    module: entityType || 'workspace',
    // A group route is the source entity, not a reason to discard an explicit
    // Project chosen by the user from the composer.
    projectId,
    entityType,
    entityId,
    organizationId: organizationId || null,
    selectionIds: [],
  }
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
const selectedActionModelProfile = computed(() => {
  if (selectedAiModel.value === 'deepseek-chat') return 'reasoning_strong'
  if (selectedAiModel.value === 'ollama-local') return 'fast_local'
  return 'balanced'
})

function composeActionPayload(message: string, projectId: string) {
  return {
    message,
    projectId,
    providerHint: selectedProviderHint.value,
    modelProfile: selectedActionModelProfile.value,
  }
}
const activeDrawerTab = ref<AnalyticsMiniTab>('sources')
const cockpitDrawerOpen = ref(false)
const selectedDrawerMessage = ref<ChatEntry | null>(null)
const isCompactViewport = ref(false)
const conversationHistory = ref<ConversationHistoryItem[]>([])
const conversationHistoryLoading = ref(false)
const assistantSessionId = ref<string | null>(null)
const assistantSessionVersion = ref(0)
const assistantSessionProjectId = ref<string | null>(null)
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

const newcomerShortcuts = [
  {
    label: 'Qaly giúp được gì?',
    description: 'Xem các luồng AI có thể phân tích hoặc hỗ trợ thực hiện.',
    prompt: 'Bạn có thể giúp tôi những gì trong Qaly? Hãy trả lời ngắn gọn và cho nút điều hướng phù hợp.'
  },
  {
    label: 'Việc nào cần chú ý?',
    description: 'Tìm rủi ro và công việc cần ưu tiên trong phạm vi hiện tại.',
    prompt: 'Trong phạm vi hiện tại, việc gì cần tôi chú ý trước và vì sao?'
  },
  {
    label: 'Tóm tắt nhanh',
    description: 'Nhận tóm tắt ngắn từ dữ liệu Qaly hiện tại.',
    prompt: 'Tóm tắt ngắn tình hình hiện tại bằng dữ liệu thật và đề xuất bước tiếp theo.'
  }
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

function hasAssistantContent(msg: ChatEntry) {
  return Boolean(
    msg.text ||
    msg.metrics?.length ||
    msg.tables?.length ||
    msg.charts?.length ||
    msg.actions?.length ||
    msg.files?.length ||
    msg.sources?.length ||
    msg.sourceRefs?.length ||
    msg.researchPlan ||
    msg.goalAnalysis ||
    msg.workPlan ||
    msg.conversation ||
    msg.projectLaunchBrief ||
    msg.projectLaunchPlan ||
    msg.safeTestRunPreview ||
    msg.safeTestRunReport ||
    msg.nativeActionDraft
  )
}

const latestAssistantMessage = computed(() => {
  return chatHistory.value
    .slice()
    .reverse()
    .find(msg => msg.role === 'assistant' && hasAssistantContent(msg))
})
const drawerMessage = computed(() => selectedDrawerMessage.value ?? latestAssistantMessage.value ?? null)
const drawerSources = computed(() => drawerMessage.value?.sources ?? [])
const drawerSourceRefs = computed(() => drawerMessage.value?.sourceRefs ?? [])
const drawerMetrics = computed(() => drawerMessage.value?.metrics ?? [])
const drawerTables = computed(() => drawerMessage.value?.tables ?? [])
const drawerCharts = computed(() => drawerMessage.value?.charts ?? [])
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
    tools: 'Công cụ AI',
    insights: 'Điểm nổi bật',
    metrics: 'Số liệu',
    risks: 'Rủi ro',
    sources: 'Nguồn dữ liệu',
    report: 'Báo cáo',
    actions: 'Việc cần làm',
    model: 'Model AI',
    history: 'Lịch sử phiên Trợ lý AI',
    settings: 'Thiết lập'
  }
  return titles[activeDrawerTab.value]
})

// Kebab (•••) menus. Keep secondary actions out of the default view while
// staying keyboard + screen-reader accessible.
const headerMenuItems = computed(() => {
  const items: Array<{ key: string; label: string; icon: any; separatorBefore?: boolean }> = [
    { key: 'new', label: 'Cuộc trò chuyện mới', icon: MessageSquarePlus },
    { key: 'history', label: 'Lịch sử phiên', icon: Clock3 },
    { key: 'tools', label: 'Công cụ AI', icon: Wrench, separatorBefore: true }
  ]
  const latest = latestAssistantMessage.value
  if (latest?.metrics?.length || latest?.tables?.length || latest?.charts?.length) {
    items.push({ key: 'metrics', label: 'Dữ liệu gần nhất', icon: Gauge })
  }
  if (latest?.actions?.length) {
    items.push({ key: 'actions', label: 'Việc cần làm gần nhất', icon: ListChecks })
  }
  items.push(
    { key: 'sources', label: 'Nguồn dữ liệu', icon: Database },
    { key: 'model', label: 'Model đang dùng', icon: Settings2, separatorBefore: true }
  )
  if (latest?.text.trim()) items.push({ key: 'export', label: 'Xuất báo cáo', icon: Download })
  items.push({ key: 'settings', label: 'Quyền riêng tư AI', icon: ShieldCheck })
  return items
})

function messageMenuItems(msg: ChatEntry) {
  const items: Array<{ key: string; label: string; icon: any; separatorBefore?: boolean }> = []
  if (msg.metrics?.length || msg.tables?.length || msg.charts?.length) {
    items.push({ key: 'metrics', label: 'Xem dữ liệu', icon: Gauge })
  }
  if (msg.actions?.length) {
    items.push({ key: 'actions', label: 'Xem việc cần làm', icon: ListChecks })
  }
  if (msg.sources?.length || msg.sourceRefs?.length) {
    items.push({ key: 'sources', label: 'Xem nguồn', icon: Database })
  }
  if (msg.text.trim()) {
    items.push(
      { key: 'copy', label: 'Sao chép', icon: Copy, separatorBefore: items.length > 0 },
      { key: 'export', label: 'Xuất báo cáo', icon: Download }
    )
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
    case 'tools':
      openCockpitDrawer('tools')
      break
    case 'metrics':
      openCockpitDrawer('metrics')
      break
    case 'actions':
      openCockpitDrawer('actions')
      break
    case 'sources':
      openCockpitDrawer('sources')
      break
    case 'model':
      openCockpitDrawer('model')
      break
    case 'export':
      exportLatestReport()
      break
    case 'settings':
      closeCockpitDrawer()
      void router.push('/settings?tab=privacy')
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
    case 'metrics':
      openCockpitDrawer('metrics', msg)
      break
    case 'actions':
      openCockpitDrawer('actions', msg)
      break
  }
}

async function startNewConversation() {
  chatHistory.value = [buildWelcomeMessage()]
  selectedDrawerMessage.value = null
  closeCockpitDrawer()
  assistantSessionId.value = null
  assistantSessionVersion.value = 0
  assistantSessionProjectId.value = null
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
  emit('composeAction', composeActionPayload(message, projectId))
}

async function selectProjectForTaskPlan(projectId: string, action: ErumiAction) {
  const message = String(action.payload?.message || '').trim()
  if (!message || !projectId) return
  keepConversationForNextTargetChange = true
  selectedTarget.value = projectId
  await nextTick()
  emit('composeAction', composeActionPayload(message, projectId))
}

function drawerActionHint(action: ErumiAction) {
  if (action.type === 'assistant_navigation') return String(action.payload?.description || 'Mở đúng màn hình nghiệp vụ')
  if (action.type === 'compose_task_plan') return 'Mở bản nháp có cấu trúc; chỉ ghi dữ liệu sau khi bạn xác nhận.'
  if (action.type === 'assistant_resume_turn') return 'Tiếp tục lượt đã lưu trên máy chủ.'
  if (['assistant_clarification', 'assistant_progressive_questions', 'select_project_for_task_plan', 'draft_change'].includes(action.type)) {
    return 'Mở lại card tương tác trong hội thoại để chọn hoặc trả lời đầy đủ.'
  }
  return 'Điền gợi ý này vào ô chat để bạn kiểm tra trước khi gửi.'
}

function handleDrawerAction(action: ErumiAction) {
  if (action.type === 'assistant_navigation') {
    openAssistantNavigation(action)
    return
  }
  if (action.type === 'compose_task_plan') {
    openComposerAction(action)
    return
  }
  if (action.type === 'assistant_resume_turn') {
    void resumeAssistantTurn(action)
    return
  }
  if (['assistant_clarification', 'assistant_progressive_questions', 'select_project_for_task_plan', 'draft_change'].includes(action.type)) {
    focusDrawerMessage()
    return
  }
  fillComposer(action.label)
}

function openResearchAction(action: AiAssistantResearchAction) {
  if (!action.executionEligible || action.capabilityId !== 'task.create.v1') return
  const message = String(action.draftInput?.message || '').trim()
  const projectId = String(action.draftInput?.projectId || '').trim()
  if (!message || !projectId) {
    showError('Action này thiếu project hoặc nội dung bản nháp nên chưa thể mở Task Composer.')
    return
  }
  emit('composeAction', composeActionPayload(message, projectId))
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

function progressiveAnswerDisplay(question: AiAssistantConversationQuestion, action: ErumiAction) {
  const answer = progressiveAnswer(question.id, action)
  if (!answer) return ''
  const selectedReply = question.quickReplies?.find(reply => reply.value === answer.value)
  return selectedReply?.label || answer.value
}

function hasAllBlockingAnswers(action: ErumiAction) {
  const draft = progressiveDraft.value
  if (!draft || draft.originTurnId !== String(action.payload?.originTurnId || '')) return false
  const answered = new Set(draft.answers.filter(answer => answer.value.trim()).map(answer => answer.questionId))
  return draft.questions.filter(question => question.blocking).every(question => answered.has(question.id))
}

function filteredProgressiveQuestions(questions: AiAssistantConversationQuestion[] | null | undefined, action: ErumiAction): AiAssistantConversationQuestion[] {
  void action
  // The server owns clarification state. Inferring answers from arbitrary chat text
  // made the form disappear while the durable Brief was still blocked.
  return Array.isArray(questions) ? questions : []
}

function pendingProgressiveQuestions(questions: AiAssistantConversationQuestion[] | null | undefined, action: ErumiAction) {
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
  const internalEntityRoute = /^\/projects\/[0-9a-f-]{36}(?:\/tasks\/[0-9a-f-]{36})?(?:\?(?:tab=(?:capacity|tasks|members|roadmap|wiki)|assignmentPlanner=1))?(?:#milestone-[0-9a-f-]{36})?$/i.test(routePath)
  if (!allowed.includes(routePath) && !internalEntityRoute) return
  router.push(routePath)
}

function openAssistantNavigation(action: ErumiAction) {
  const routePath = String(action.payload?.route || '').trim()
  openGuidanceRoute(routePath)
}

function mapAssistantTurn(turn: AiAssistantTurnResponse, originalMessage?: string): ErumiChatResponse {
  const answer = turn.answer
  const actions = [...(answer?.actions ?? [])]
  const actualProvider = turn.actualProvider?.trim()
  const actualModel = turn.actualModel?.trim()
  const reachedModel = Boolean(actualProvider && actualModel &&
    actualProvider !== 'not_reached' && actualModel !== 'not_reached')
  const answerModelReached = Boolean(answer?.model?.provider && answer?.model?.id &&
    answer.model.provider !== 'not_reached' && answer.model.id !== 'not_reached')

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
    model: answerModelReached ? answer?.model : (reachedModel
      ? {
          id: actualModel ?? null,
          label: [actualProvider, actualModel].filter(Boolean).join(' / '),
          provider: actualProvider ?? null,
          status: 'actual',
        }
      : null),
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
    nativeActionDraft: turn.nativeActionDraft ?? null,
    portfolioScheduleProposal: normalizePortfolioScheduleProposal(turn.portfolioScheduleProposal),
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
      safeTestRunReport: response.safeTestRunReport,
      nativeActionDraft: response.nativeActionDraft,
      portfolioScheduleProposal: response.portfolioScheduleProposal
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
  assistantSessionProjectId.value = session.projectId ?? null
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

function applyAssistantSessionScopeMetadata(session: AiAssistantSession) {
  assistantSessionId.value = session.sessionId
  assistantSessionProjectId.value = session.projectId ?? null
  assistantSessionVersion.value = session.version
  window.localStorage.setItem(AI_ACTIVE_SESSION_STORAGE_KEY, session.sessionId)
  progressiveDraft.value = session.clarificationDraft ?? progressiveDraft.value
}

async function createAssistantSession() {
  const context = assistantContextForCurrentRoute()
  const session = await apiJson<AiAssistantSession>('/api/ai/assistant/sessions', {
    method: 'POST',
    body: JSON.stringify({
      context,
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

async function persistAssistantSessionScope(projectId: string | null) {
  await ensureAssistantSession()
  if (!assistantSessionId.value || assistantSessionProjectId.value === projectId) return
  try {
    const updated = await apiJson<AiAssistantSession>(
      `/api/ai/assistant/sessions/${assistantSessionId.value}/scope`,
      {
        method: 'PUT',
        body: JSON.stringify({
          expectedVersion: assistantSessionVersion.value,
          projectId
        })
      }
    )
    // A scope update must not restore the old target/history over a newer
    // composer selection. Only advance the durable session metadata here.
    applyAssistantSessionScopeMetadata(updated)
  } catch (error) {
    try {
      const current = await apiJson<AiAssistantSession>(
        `/api/ai/assistant/sessions/${assistantSessionId.value}`
      )
      applyAssistantSessionScopeMetadata(current)
    } catch {
      // Keep the original scope error; the next restore/send can recover the server session.
    }
    throw error
  }
}

function queueAssistantSessionScope(projectId: string | null) {
  const queued = assistantScopeSyncQueue
    .catch(() => undefined)
    .then(() => persistAssistantSessionScope(projectId))
  assistantScopeSyncQueue = queued
  return queued
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

function openTableRowAction(table: ErumiTable, row: Record<string, unknown>) {
  const routeKey = String(table.rowAction?.routeKey || 'route')
  const routePath = String(row[routeKey] || '').trim()
  if (!routePath) return
  openGuidanceRoute(routePath)
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

function choosePrompt(prompt: string) {
  closeCockpitDrawer()
  fillComposer(prompt)
}

function useAnalysisTool(tool: AiToolbarAction) {
  if (tool.behavior === 'open-drawer') {
    openCockpitDrawer(tool.tab)
    return
  }
  if (tool.prompt) choosePrompt(tool.prompt)
}

function focusDrawerMessage() {
  const messageIndex = selectedDrawerMessage.value
    ? chatHistory.value.indexOf(selectedDrawerMessage.value)
    : chatHistory.value.length - 1
  closeCockpitDrawer()
  void nextTick(() => {
    const message = chatContainerRef.value?.querySelector<HTMLElement>(`[data-message-index="${messageIndex}"]`)
    if (message) message.scrollIntoView({ behavior: 'smooth', block: 'center' })
    else scrollToBottom()
  })
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
  choosePrompt(`Giải thích nguồn "${sourceLabel}" và dữ liệu nào đã được dùng để tạo nhận định này.`)
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
  const nextProjectId = normalizeAssistantProjectTarget(selectedTarget.value)
  if (assistantSessionId.value && assistantSessionProjectId.value === nextProjectId) {
    selectedDrawerMessage.value = null
    scrollToBottom()
    return
  }
  selectedDrawerMessage.value = null
  try {
    await queueAssistantSessionScope(nextProjectId)
  } catch (error) {
    showError(error instanceof Error
      ? `Không thể đổi phạm vi cuộc trò chuyện: ${error.message}`
      : 'Không thể đổi phạm vi cuộc trò chuyện. Phiên đã được tải lại theo dữ liệu máy chủ.')
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
    return `### 📊 Đánh giá hiệu suất làm việc tuần qua\n\n- **Tiến độ**: Các dự án trong Workspace hoạt động đúng tiến độ đạt **75%**. Tổng số nhiệm vụ đã hoàn tất trong tuần là **8 nhiệm vụ**.\n- **Thời gian**: Toàn nhóm đã ghi nhận **32 giờ chấm công thực tế**.\n- **Nhận xét**: Năng suất duy trì ở mức ổn định. Điểm sáng là sự tập trung cao độ ở các task thuộc luồng quan trọng.`
  }
  if (p.includes('rủi ro') || p.includes('chậm') || p.includes('risk') || p.includes('quá hạn')) {
    return `### ⚠️ Đánh giá rủi ro toàn Workspace\n\n- **Nhiệm vụ trễ hạn**: Phát hiện dự án đang có **1 nhiệm vụ quá hạn** cần xử lý.\n- **Dự án chịu ảnh hưởng**: Dự án DATN đang có tỉ lệ quá hạn nhẹ.\n- **Giải pháp**: Nhắc nhở người thực hiện trực tiếp hoặc phân bổ thêm thành viên hỗ trợ để tháo gỡ điểm nghẽn.`
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

function launchReviewSummary(plan: ProjectLaunchPlan) {
  const draft = launchPlanDraft(plan)
  const scenario = plan.staffingScenarios.find(item => item.scenarioId === draft.selectedScenarioId)
  const selectedSprints = draft.sprints.filter(item => item.selected)
  return {
    manager: scenario?.managerName || 'Chưa chọn',
    memberCount: draft.staffing.filter(item => item.included).length,
    sprintCount: selectedSprints.length,
    taskCount: selectedSprints.reduce((total, sprint) => total + sprint.tasks.filter(item => item.selected).length, 0),
  }
}

function launchBriefStateLabel(state: string) {
  if (state === 'BRIEF_READY') return 'Đủ dữ kiện để lập phương án'
  if (state === 'CLARIFICATION_REQUIRED') return 'Cần bổ sung thông tin'
  return state
}

function launchPlanStateLabel(state: string) {
  if (state === 'pending_review') return 'Sẵn sàng để bạn xem lại'
  if (state === 'blocked') return 'Cần điều chỉnh trước khi tạo'
  if (state === 'executing') return 'Đang tạo dự án'
  if (state === 'executed') return 'Đã tạo và kiểm tra lại'
  if (state === 'rolled_back') return 'Đã hoàn tác'
  return state
}

function staffingRejectLabel(code: string) {
  const labels: Record<string, string> = {
    missing_capacity_profile: 'chưa khai báo năng lực theo tuần',
    max_concurrent_projects_reached: 'đã đạt giới hạn dự án đồng thời',
    no_effective_capacity: 'không còn thời gian khả dụng sau khi trừ tải và thời gian dự phòng',
    unavailable_for_full_window: 'không khả dụng trong toàn bộ thời gian dự án',
  }
  return labels[code] || code
}

function launchFeatureTitle(plan: ProjectLaunchPlan, featureId: string | null | undefined) {
  if (!featureId) return ''
  return plan.deliveryPlan.features?.find(item => item.featureId === featureId)?.title || featureId
}

function hasAssistantCapability(entry: ChatEntry, capabilityId: string) {
  return Boolean(entry.capabilities?.some(item => item.capabilityId === capabilityId))
}

function canManageProjectLaunch(entry: ChatEntry) {
  return hasAssistantCapability(entry, 'project.staffing.plan.v1')
}

function canExecuteProjectLaunch(entry: ChatEntry) {
  return hasAssistantCapability(entry, 'project.launch.execute.v1')
}

function selectedLaunchCandidates(plan?: ProjectLaunchPlan | null) {
  if (!plan) return []
  return plan.staffingScenarios.find(item => item.scenarioId === selectedLaunchScenarioId(plan))?.managerCandidates || []
}

function selectLaunchScenario(plan: ProjectLaunchPlan, scenarioId: string) {
  selectedLaunchScenarios.value = { ...selectedLaunchScenarios.value, [plan.planId]: scenarioId }
  const scenario = plan.staffingScenarios.find(item => item.scenarioId === scenarioId)
  if (scenario) projectLaunchPlanDrafts.value = {
    ...projectLaunchPlanDrafts.value,
    [plan.planId]: createLaunchPlanDraft(plan, scenario),
  }
}

function createLaunchPlanDraft(plan: ProjectLaunchPlan, scenario: ProjectStaffingScenario): ProjectLaunchPlanDraft {
  const memberMap = new Map(scenario.members.map(item => [item.userId, item]))
  return {
    selectedScenarioId: scenario.scenarioId,
    staffing: scenario.managerCandidates.map(candidate => {
      const member = memberMap.get(candidate.userId)
      return {
        userId: candidate.userId,
        proposedRole: member?.proposedRole || 'Member',
        proposedHours: member?.proposedHours || Math.min(8, candidate.availableHours || 0),
        included: Boolean(member),
        manager: scenario.managerUserId === candidate.userId,
      }
    }),
    sprints: cloneAssistantJson(plan.deliveryPlan.sprints || []),
    assignmentMode: plan.deliveryPlan.assignmentMode || 'auto_balance',
    scheduleMode: plan.deliveryPlan.scheduleMode || 'sequential_sprints',
    dirty: false,
  }
}

function updateLaunchPlanMode(
  plan: ProjectLaunchPlan,
  field: 'assignmentMode' | 'scheduleMode',
  event: Event,
) {
  const draft = launchPlanDraft(plan)
  const value = (event.target as HTMLSelectElement).value
  projectLaunchPlanDrafts.value = {
    ...projectLaunchPlanDrafts.value,
    [plan.planId]: { ...draft, [field]: value, dirty: true },
  }
}

function launchPlanDraft(plan: ProjectLaunchPlan) {
  const existing = projectLaunchPlanDrafts.value[plan.planId]
  if (existing) return existing
  const scenarioId = selectedLaunchScenarioId(plan)
  const scenario = plan.staffingScenarios.find(item => item.scenarioId === scenarioId) || plan.staffingScenarios[0]
  const draft = createLaunchPlanDraft(plan, scenario)
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: draft }
  return draft
}

function updateLaunchStaffing(
  plan: ProjectLaunchPlan,
  userId: string,
  field: 'included' | 'manager' | 'proposedRole' | 'proposedHours',
  event: Event
) {
  const draft = launchPlanDraft(plan)
  const input = event.target as HTMLInputElement | HTMLSelectElement
  const value = field === 'included' || field === 'manager'
    ? (input as HTMLInputElement).checked
    : field === 'proposedHours' ? Number(input.value) : input.value
  const staffing = draft.staffing.map(item => {
    if (item.userId !== userId) return field === 'manager' ? { ...item, manager: false } : item
    return { ...item, [field]: value, included: field === 'manager' && value ? true : item.included }
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, staffing, dirty: true } }
}

function updateLaunchSprint(
  plan: ProjectLaunchPlan,
  sprintId: string,
  field: 'selected' | 'name' | 'objective' | 'startDate' | 'endDate',
  event: Event
) {
  const draft = launchPlanDraft(plan)
  const input = event.target as HTMLInputElement
  let value: string | boolean = field === 'selected' ? input.checked : input.value
  if ((field === 'startDate' || field === 'endDate') && typeof value === 'string' && value)
    value = new Date(`${value}T00:00:00Z`).toISOString()
  const sprints = draft.sprints.map(item => item.clientId === sprintId ? { ...item, [field]: value } : item)
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function addLaunchSprint(plan: ProjectLaunchPlan) {
  const draft = launchPlanDraft(plan)
  const lastEnd = draft.sprints.at(-1)?.endDate || plan.deliveryPlan.startDate
  const start = new Date(lastEnd)
  const end = new Date(start)
  end.setUTCDate(end.getUTCDate() + 14)
  const sprint: ProjectLaunchSprintPlan = {
    clientId: `sprint-${crypto.randomUUID()}`,
    name: `Sprint ${draft.sprints.length + 1}`,
    objective: 'Mục tiêu Sprint cần được xác nhận',
    startDate: start.toISOString(),
    endDate: end.toISOString(),
    exitCriteria: ['Các công việc đã chọn đạt tiêu chí nghiệm thu.'],
    tasks: [],
    selected: true,
  }
  projectLaunchPlanDrafts.value = {
    ...projectLaunchPlanDrafts.value,
    [plan.planId]: { ...draft, sprints: [...draft.sprints, sprint], dirty: true },
  }
}

function moveLaunchSprint(plan: ProjectLaunchPlan, sprintId: string, direction: -1 | 1) {
  const draft = launchPlanDraft(plan)
  const sprints = [...draft.sprints]
  const index = sprints.findIndex(item => item.clientId === sprintId)
  const target = index + direction
  if (index < 0 || target < 0 || target >= sprints.length) return
  ;[sprints[index], sprints[target]] = [sprints[target], sprints[index]]
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function addLaunchTask(plan: ProjectLaunchPlan, sprintId: string) {
  const draft = launchPlanDraft(plan)
  const feature = plan.deliveryPlan.features?.find(item => item.selected)
  const task: ProjectLaunchTaskPlan = {
    clientId: `task-${crypto.randomUUID()}`,
    title: 'Công việc mới',
    description: 'Mô tả kết quả cần hoàn thành.',
    acceptanceCriteria: ['Kết quả đáp ứng tiêu chí nghiệm thu đã duyệt.'],
    definitionOfDone: ['Đã review và kiểm chứng trên dữ liệu phù hợp.'],
    priority: 'Medium',
    estimatedHours: 8,
    proposedAssigneeId: null,
    proposedReviewerId: null,
    requiredSkillIds: [],
    requiredSkillNames: feature?.requiredSkillNames || [],
    dependencyClientIds: [],
    sourceRefs: [],
    selected: true,
    featureId: feature?.featureId || null,
    objectiveMetricIds: plan.deliveryPlan.objectiveMetrics?.slice(0, 1).map(item => item.metricId) || [],
  }
  const sprints = draft.sprints.map(item => item.clientId === sprintId ? { ...item, tasks: [...item.tasks, task] } : item)
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function moveLaunchTask(plan: ProjectLaunchPlan, sprintId: string, taskId: string, direction: -1 | 1) {
  const draft = launchPlanDraft(plan)
  const sprints = draft.sprints.map(sprint => {
    if (sprint.clientId !== sprintId) return sprint
    const tasks = [...sprint.tasks]
    const index = tasks.findIndex(item => item.clientId === taskId)
    const target = index + direction
    if (index < 0 || target < 0 || target >= tasks.length) return sprint
    ;[tasks[index], tasks[target]] = [tasks[target], tasks[index]]
    return { ...sprint, tasks }
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function updateLaunchTask(
  plan: ProjectLaunchPlan,
  sprintId: string,
  taskId: string,
  field: 'selected' | 'title' | 'estimatedHours' | 'proposedAssigneeId' | 'proposedReviewerId',
  event: Event
) {
  const draft = launchPlanDraft(plan)
  const input = event.target as HTMLInputElement | HTMLSelectElement
  const value = field === 'selected'
    ? (input as HTMLInputElement).checked
    : field === 'estimatedHours' ? Number(input.value) : input.value || null
  const sprints = draft.sprints.map(sprint => sprint.clientId !== sprintId ? sprint : {
    ...sprint,
    tasks: sprint.tasks.map(task => {
      if (task.clientId !== taskId) return task
      const updated = { ...task, [field]: value }
      if (field === 'proposedAssigneeId' && updated.proposedReviewerId === value)
        updated.proposedReviewerId = null
      return updated
    }),
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function updateLaunchTaskDetail(
  plan: ProjectLaunchPlan,
  sprintId: string,
  taskId: string,
  field: 'description' | 'acceptanceCriteria' | 'definitionOfDone' | 'requiredSkillNames' | 'featureId',
  event: Event
) {
  const draft = launchPlanDraft(plan)
  const raw = (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value
  const value = field === 'acceptanceCriteria' || field === 'definitionOfDone' || field === 'requiredSkillNames'
    ? raw.split(/\r?\n/).map(item => item.trim()).filter(Boolean)
    : raw
  const sprints = draft.sprints.map(sprint => sprint.clientId !== sprintId ? sprint : {
    ...sprint,
    tasks: sprint.tasks.map(task => {
      if (task.clientId !== taskId) return task
      if (field !== 'featureId') return { ...task, [field]: value }
      const feature = plan.deliveryPlan.features?.find(item => item.featureId === value)
      return {
        ...task,
        featureId: String(value),
        requiredSkillNames: [...new Set([...task.requiredSkillNames, ...(feature?.requiredSkillNames || [])])],
      }
    }),
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function toggleLaunchTaskMetric(plan: ProjectLaunchPlan, sprintId: string, taskId: string, metricId: string) {
  const draft = launchPlanDraft(plan)
  const sprints = draft.sprints.map(sprint => sprint.clientId !== sprintId ? sprint : {
    ...sprint,
    tasks: sprint.tasks.map(task => task.clientId !== taskId ? task : {
      ...task,
      objectiveMetricIds: (task.objectiveMetricIds || []).includes(metricId)
        ? (task.objectiveMetricIds || []).filter(item => item !== metricId)
        : [...(task.objectiveMetricIds || []), metricId],
    }),
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

function toggleLaunchTaskDependency(plan: ProjectLaunchPlan, sprintId: string, taskId: string, dependencyId: string) {
  const draft = launchPlanDraft(plan)
  const sprints = draft.sprints.map(sprint => sprint.clientId !== sprintId ? sprint : {
    ...sprint,
    tasks: sprint.tasks.map(task => task.clientId !== taskId ? task : {
      ...task,
      dependencyClientIds: task.dependencyClientIds.includes(dependencyId)
        ? task.dependencyClientIds.filter(item => item !== dependencyId)
        : [...task.dependencyClientIds, dependencyId],
    }),
  })
  projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value, [plan.planId]: { ...draft, sprints, dirty: true } }
}

async function saveLaunchPlanReview(entry: ChatEntry, plan: ProjectLaunchPlan) {
  if (launchActionBusy.value) return
  const draft = launchPlanDraft(plan)
  launchActionBusy.value = `review:${plan.planId}`
  try {
    const updated = await apiResult<ProjectLaunchPlan>(`/api/ai/project-launch/plans/${plan.planId}`, {
      method: 'PUT',
      body: JSON.stringify({
        expectedRevision: plan.rowRevision,
        selectedScenarioId: draft.selectedScenarioId,
        staffing: draft.staffing,
        sprints: draft.sprints,
        assignmentMode: draft.assignmentMode,
        scheduleMode: draft.scheduleMode,
      }),
    })
    entry.projectLaunchPlan = updated
    selectedLaunchScenarios.value = { ...selectedLaunchScenarios.value, [plan.planId]: updated.selectedScenarioId || 'custom' }
    delete projectLaunchPlanDrafts.value[plan.planId]
    projectLaunchPlanDrafts.value = { ...projectLaunchPlanDrafts.value }
    showSuccess(updated.blockingReasons.length
      ? 'Đã lưu. Qaly đã chỉ rõ các điểm cần xử lý trước khi tạo Project.'
      : 'Đã lưu và kiểm tra lại đội hình, lịch, kỹ năng và Sprint.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể lưu phương án đã chỉnh.')
  } finally {
    launchActionBusy.value = null
  }
}

function launchBriefDraft(brief: ProjectLaunchBrief): ProjectLaunchBriefDraft {
  const current = projectLaunchBriefDrafts.value[brief.briefId]
  if (current) return current
  const objectiveProfile: ProjectLaunchObjectiveProfile = cloneAssistantJson(brief.objectiveProfile || {
    problemStatement: brief.objective || '',
    primaryAudience: brief.primaryAudience || 'Chưa quyết định',
    desiredOutcome: brief.objective || '',
    businessValue: '',
    metrics: (brief.successMeasures || []).map((title, index) => ({
      metricId: `metric-${index + 1}`,
      title,
      metricType: 'outcome',
      baseline: null,
      target: null,
      unit: null,
      measurementWindow: null,
      dataSource: null,
      owner: null,
      status: 'needs_confirmation',
    })),
    guardrails: [],
    assumptions: brief.assumptions || [],
    nonGoals: brief.exclusions || [],
  })
  const features: ProjectLaunchFeature[] = cloneAssistantJson(brief.features || (brief.scope || []).map((title, index) => ({
    featureId: `feature-${index + 1}`,
    title,
    category: 'Product flow',
    priority: index < 3 ? 'must_have' : 'should_have',
    description: title,
    primaryAudience: brief.primaryAudience || 'Chưa quyết định',
    acceptanceCriteria: [`Luồng ${title} đáp ứng tiêu chí nghiệm thu đã duyệt.`],
    requiredSkillNames: [],
    selected: true,
    custom: false,
  })))
  const created = normalizeLaunchBriefForEditing({
    projectName: brief.proposedProjectName || '',
    objective: brief.objective || '',
    targetTimebox: normalizeLaunchTimebox(brief.targetTimebox),
    primaryAudience: normalizeLaunchAudience(brief.primaryAudience),
    objectiveProfile,
    features,
    successMeasures: (brief.successMeasures || []).join('\n'),
    exclusions: (brief.exclusions || []).join('\n'),
    customFeatureTitle: '',
  })
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: created }
  return created
}

function updateLaunchBriefDraft(
  brief: ProjectLaunchBrief,
  field: 'projectName' | 'objective' | 'targetTimebox' | 'primaryAudience' | 'successMeasures' | 'exclusions' | 'customFeatureTitle',
  event: Event
) {
  const draft = { ...launchBriefDraft(brief) }
  draft[field] = (event.target as HTMLInputElement | HTMLTextAreaElement).value
  if (field === 'objective') draft.objectiveProfile = { ...draft.objectiveProfile, desiredOutcome: draft.objective }
  if (field === 'primaryAudience') {
    draft.objectiveProfile = { ...draft.objectiveProfile, primaryAudience: draft.primaryAudience }
    draft.features = draft.features.map(item => ({ ...item, primaryAudience: draft.primaryAudience }))
  }
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: draft }
}

function setLaunchBriefChoice(brief: ProjectLaunchBrief, field: 'targetTimebox' | 'primaryAudience', value: string) {
  const draft = launchBriefDraft(brief)
  const next = { ...draft, [field]: value }
  if (field === 'primaryAudience') {
    next.objectiveProfile = { ...next.objectiveProfile, primaryAudience: value }
    next.features = next.features.map(item => ({ ...item, primaryAudience: value }))
  }
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: next }
}

function addLaunchMetric(brief: ProjectLaunchBrief, title: string) {
  const draft = launchBriefDraft(brief)
  if (draft.objectiveProfile.metrics.some(item => item.title === title)) return
  const metric: ProjectObjectiveMetric = applyLaunchMetricDefaults({
    metricId: `metric-${Date.now()}`,
    title,
    metricType: 'outcome',
    baseline: null,
    target: null,
    unit: null,
    measurementWindow: draft.targetTimebox || null,
    dataSource: null,
    owner: null,
    status: 'needs_confirmation',
  }, draft.targetTimebox)
  const next = { ...draft, objectiveProfile: { ...draft.objectiveProfile, metrics: [...draft.objectiveProfile.metrics, metric] } }
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: next }
}

function updateLaunchObjectiveField(
  brief: ProjectLaunchBrief,
  field: 'problemStatement' | 'businessValue',
  event: Event
) {
  const draft = launchBriefDraft(brief)
  const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: { ...draft, objectiveProfile: { ...draft.objectiveProfile, [field]: value } },
  }
}

function updateLaunchObjectiveList(
  brief: ProjectLaunchBrief,
  field: 'guardrails' | 'assumptions' | 'nonGoals',
  event: Event
) {
  const draft = launchBriefDraft(brief)
  const values = (event.target as HTMLTextAreaElement).value.split(/\r?\n/).map(item => item.trim()).filter(Boolean)
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: { ...draft, objectiveProfile: { ...draft.objectiveProfile, [field]: values } },
  }
}

function updateLaunchMetric(brief: ProjectLaunchBrief, metricId: string, field: keyof ProjectObjectiveMetric, event: Event) {
  const draft = launchBriefDraft(brief)
  const raw = (event.target as HTMLInputElement).value
  const value = field === 'baseline' || field === 'target' ? (raw === '' ? null : Number(raw)) : raw
  const metrics = draft.objectiveProfile.metrics.map(item => item.metricId === metricId
    ? { ...item, [field]: value, status: field === 'baseline' || field === 'target' ? 'reviewed' : item.status }
    : item)
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: { ...draft, objectiveProfile: { ...draft.objectiveProfile, metrics } },
  }
}

function removeLaunchMetric(brief: ProjectLaunchBrief, metricId: string) {
  const draft = launchBriefDraft(brief)
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: {
      ...draft,
      objectiveProfile: { ...draft.objectiveProfile, metrics: draft.objectiveProfile.metrics.filter(item => item.metricId !== metricId) },
    },
  }
}

function moveLaunchMetric(brief: ProjectLaunchBrief, metricId: string, direction: -1 | 1) {
  const draft = launchBriefDraft(brief)
  const metrics = [...draft.objectiveProfile.metrics]
  const index = metrics.findIndex(item => item.metricId === metricId)
  const target = index + direction
  if (index < 0 || target < 0 || target >= metrics.length) return
  ;[metrics[index], metrics[target]] = [metrics[target], metrics[index]]
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: { ...draft, objectiveProfile: { ...draft.objectiveProfile, metrics } },
  }
}

function toggleLaunchFeatureTemplate(brief: ProjectLaunchBrief, template: { title: string; category: string }) {
  const draft = launchBriefDraft(brief)
  const existing = draft.features.find(item => item.title === template.title)
  const features = existing
    ? draft.features.map(item => item.featureId === existing.featureId ? { ...item, selected: !item.selected } : item)
    : [...draft.features, {
      featureId: `feature-${Date.now()}`,
      title: template.title,
      category: template.category,
      priority: 'must_have',
      description: template.title,
      primaryAudience: draft.primaryAudience || 'Chưa quyết định',
      acceptanceCriteria: [`Luồng ${template.title} hoạt động end-to-end.`],
      requiredSkillNames: [],
      selected: true,
      custom: false,
    }]
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: { ...draft, features } }
}

function addCustomLaunchFeature(brief: ProjectLaunchBrief) {
  const draft = launchBriefDraft(brief)
  const title = draft.customFeatureTitle.trim()
  if (!title) return
  const features = [...draft.features, {
    featureId: `custom-${Date.now()}`,
    title,
    category: 'Khác',
    priority: 'must_have',
    description: title,
    primaryAudience: draft.primaryAudience || 'Chưa quyết định',
    acceptanceCriteria: [`Luồng ${title} hoạt động end-to-end.`],
    requiredSkillNames: [],
    selected: true,
    custom: true,
  }]
  projectLaunchBriefDrafts.value = {
    ...projectLaunchBriefDrafts.value,
    [brief.briefId]: { ...draft, features, customFeatureTitle: '' },
  }
}

function updateLaunchFeature(
  brief: ProjectLaunchBrief,
  featureId: string,
  field: 'title' | 'category' | 'priority' | 'description' | 'primaryAudience' | 'acceptanceCriteria',
  event: Event
) {
  const draft = launchBriefDraft(brief)
  const raw = (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value
  const value = field === 'acceptanceCriteria'
    ? raw.split(/\r?\n/).map(item => item.trim()).filter(Boolean)
    : raw
  const features = draft.features.map(item => item.featureId === featureId ? { ...item, [field]: value } : item)
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: { ...draft, features } }
}

function toggleLaunchFeatureSkill(brief: ProjectLaunchBrief, featureId: string, skillName: string) {
  const draft = launchBriefDraft(brief)
  const features = draft.features.map(item => {
    if (item.featureId !== featureId) return item
    const selected = item.requiredSkillNames.includes(skillName)
    return { ...item, requiredSkillNames: selected
      ? item.requiredSkillNames.filter(name => name !== skillName)
      : [...item.requiredSkillNames, skillName] }
  })
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: { ...draft, features } }
}

function addCustomLaunchFeatureSkill(brief: ProjectLaunchBrief, featureId: string, event: KeyboardEvent) {
  const input = event.target as HTMLInputElement
  const skillName = input.value.trim()
  if (!skillName) return
  const draft = launchBriefDraft(brief)
  const features = draft.features.map(item => item.featureId === featureId && !item.requiredSkillNames.includes(skillName)
    ? { ...item, requiredSkillNames: [...item.requiredSkillNames, skillName] }
    : item)
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: { ...draft, features } }
  input.value = ''
}

function moveLaunchFeature(brief: ProjectLaunchBrief, featureId: string, direction: -1 | 1) {
  const draft = launchBriefDraft(brief)
  const features = [...draft.features]
  const index = features.findIndex(item => item.featureId === featureId)
  const target = index + direction
  if (index < 0 || target < 0 || target >= features.length) return
  ;[features[index], features[target]] = [features[target], features[index]]
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: { ...draft, features } }
}

function launchBriefMissingFields(brief: ProjectLaunchBrief) {
  const draft = launchBriefDraft(brief)
  const missing: string[] = []
  if (!draft.projectName.trim()) missing.push('tên dự án')
  if (!draft.objective.trim()) missing.push('kết quả mong muốn')
  if (!draft.targetTimebox.trim() || draft.targetTimebox === 'Chưa quyết định') missing.push('thời hạn')
  if (!draft.primaryAudience.trim() || draft.primaryAudience === 'Chưa quyết định') missing.push('người dùng chính')
  if (!draft.objectiveProfile.metrics.some(item => item.title.trim())) missing.push('ít nhất một thước đo')
  if (!draft.features.some(item => item.selected && item.priority !== 'out_of_scope')) missing.push('ít nhất một chức năng trong phạm vi')
  return missing
}

function canSubmitLaunchBriefDraft(brief: ProjectLaunchBrief) {
  return launchBriefMissingFields(brief).length === 0
}

function applyLaunchBriefSuggestions(brief: ProjectLaunchBrief) {
  const draft = launchBriefDraft(brief)
  const next = {
    ...draft,
    targetTimebox: draft.targetTimebox || '8 tuần',
  }
  next.objectiveProfile = { ...next.objectiveProfile, primaryAudience: next.primaryAudience }
  projectLaunchBriefDrafts.value = { ...projectLaunchBriefDrafts.value, [brief.briefId]: next }
}

async function submitLaunchBriefReview(brief: ProjectLaunchBrief) {
  if (!canSubmitLaunchBriefDraft(brief) || isChatting.value) return
  const draft = launchBriefDraft(brief)
  const reviewedFeatures = draft.features.map(item => item.priority === 'out_of_scope'
    ? { ...item, selected: false }
    : item)
  const payload = {
    ...draft,
    features: reviewedFeatures,
    scope: reviewedFeatures.filter(item => item.selected).map(item => item.title).join('\n'),
    successMeasures: draft.objectiveProfile.metrics.map(item => item.title).join('\n'),
  }
  await submitChat(
    `Cập nhật Project Launch Brief revision ${brief.revision} bằng biểu mẫu đã review.`,
    undefined,
    `Đã cập nhật Launch Brief: ${draft.projectName}`,
    undefined,
    'project.launch.analyze.v1',
    brief.organizationId,
    [{
      questionId: 'launch.brief_form',
      value: JSON.stringify(payload),
      label: `Launch Brief: ${draft.projectName}`,
    }]
  )
}

function recommendedRulebookRules(): OrganizationWorkRule[] {
  return [
    { ruleKey: 'active_membership_required', category: 'governance', enforcement: 'block', description: 'Chỉ thành viên đang hoạt động mới được tham gia phương án.', enabled: true },
    { ruleKey: 'max_active_projects', category: 'portfolio_capacity', enforcement: 'block', description: 'Số dự án hoạt động tối đa của mỗi người.', numericValue: 3, unit: 'projects', enabled: true },
    { ruleKey: 'max_utilization_percent', category: 'portfolio_capacity', enforcement: 'block', description: 'Mức sử dụng tối đa sau khi nhận dự án mới.', numericValue: 85, unit: 'percent', enabled: true },
    { ruleKey: 'focus_reserve_percent', category: 'portfolio_capacity', enforcement: 'block', description: 'Phần thời gian dự phòng cho hỗ trợ và chuyển ngữ cảnh.', numericValue: 15, unit: 'percent', enabled: true },
    { ruleKey: 'capacity_evidence_required', category: 'staffing', enforcement: 'block', description: 'Mỗi người phải có capacity và availability còn hiệu lực.', enabled: true },
  ]
}

function startRecommendedRulebookReview(brief: ProjectLaunchBrief) {
  rulebookReviewRules.value = {
    ...rulebookReviewRules.value,
    [brief.organizationId]: recommendedRulebookRules(),
  }
}

function cancelRecommendedRulebookReview(organizationId: string) {
  const next = { ...rulebookReviewRules.value }
  delete next[organizationId]
  rulebookReviewRules.value = next
}

function updateRulebookReviewRule(organizationId: string, ruleKey: string, field: 'enabled' | 'numericValue', event: Event) {
  const input = event.target as HTMLInputElement
  const rules = (rulebookReviewRules.value[organizationId] || []).map(rule => rule.ruleKey === ruleKey
    ? { ...rule, [field]: field === 'enabled' ? input.checked : Number(input.value) }
    : rule)
  rulebookReviewRules.value = { ...rulebookReviewRules.value, [organizationId]: rules }
}

function rulebookRuleMin(ruleKey: string) {
  return ruleKey === 'max_active_projects' ? 1 : ruleKey === 'max_utilization_percent' ? 10 : 0
}

function rulebookRuleMax(ruleKey: string) {
  return ruleKey === 'max_active_projects' ? 50 : ruleKey === 'max_utilization_percent' ? 100 : 50
}

async function createRecommendedRulebookDraft(brief: ProjectLaunchBrief) {
  const reviewedRules = rulebookReviewRules.value[brief.organizationId]
  if (!reviewedRules?.length) {
    startRecommendedRulebookReview(brief)
    return
  }
  launchActionBusy.value = `rulebook:${brief.organizationId}`
  try {
    const result = await apiResult<OrganizationWorkRuleSet>(
      `/api/organizations/${brief.organizationId}/work-rulebook`,
      {
        method: 'POST',
        body: JSON.stringify({
          effectiveFrom: new Date().toISOString(),
          rules: reviewedRules
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
  if (!draft || !window.confirm(`Áp dụng bộ quy tắc v${draft.version} vừa xem và tiếp tục lập phương án?`)) return
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

async function refreshCreatedProjectContext(plan: ProjectLaunchPlan) {
  await loadDashboard()
  const receipt = plan.executionReceipt
  if (receipt?.internalTransactionCommitted && receipt.projectId) {
    selectedTarget.value = receipt.projectId
  }
}

async function confirmLaunchPlan(entry: ChatEntry, plan: ProjectLaunchPlan) {
  if (launchActionBusy.value) return
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
    if (updated.executionReceipt?.readBackVerified) {
      showSuccess('Đã tạo Project và kiểm tra lại dữ liệu thành công.')
    } else if (updated.state === 'verification_failed') {
      showError('Project đã được tạo nhưng dữ liệu đọc lại chưa khớp phương án. Không tạo lại; hãy mở receipt để kiểm tra.')
    } else {
      showInfo('Project đã được commit và máy chủ đang xác minh dữ liệu. Nút xác nhận được khóa để tránh tạo trùng.')
    }
    await refreshCreatedProjectContext(updated)
  } catch (error) {
    // A timeout can happen after the canonical graph was committed. Reconcile first,
    // then report an error only if the server also says the launch did not complete.
    let launchStillExecuting = false
    for (let attempt = 0; attempt < 5; attempt += 1) {
      try {
        const canonical = await apiResult<ProjectLaunchPlan>(`/api/ai/project-launch/plans/${plan.planId}`)
        entry.projectLaunchPlan = canonical
        if (canonical.executionReceipt?.readBackVerified) {
          showSuccess('Project đã được tạo và đối chiếu lại dữ liệu thành công.')
          await refreshCreatedProjectContext(canonical)
          return
        }
        launchStillExecuting = canonical.state.toLowerCase() === 'executing'
        if (!launchStillExecuting) break
      } catch {
        // Keep polling briefly; the write transaction may still be completing.
      }
      await new Promise(resolve => window.setTimeout(resolve, 500))
    }
    if (launchStillExecuting) {
      showInfo('Project đang được máy chủ hoàn tất. Qaly đã khóa nút xác nhận để tránh tạo lặp; receipt sẽ xuất hiện khi đọc lại phiên.')
      return
    }
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

function nativeActionTitle(draft: AiNativeActionDraft) {
  const labels: Record<string, string> = {
    'task.acceptance_checklist.v1': 'Checklist nghiệm thu',
    'task.breakdown.v1': 'Tách task thành các subtask',
    'wiki.brief_task.v1': 'Brief và task từ Wiki',
    'group.poll.create.v1': 'Poll của nhóm',
    'project.digest.configure.v1': 'Lịch gửi tổng hợp dự án',
    'meeting.actions.review.v1': 'Quyết định và action item cuộc họp',
    'project.roadmap.adjust.v1': 'Điều chỉnh Roadmap/Sprint',
    'task.skill_evidence.confirm.v1': 'Đóng góp và bằng chứng kỹ năng',
  }
  return labels[draft.capabilityId] || 'Thay đổi do AI đề xuất'
}

function nativeActionStatusLabel(draft: AiNativeActionDraft) {
  if (draft.receipt || draft.status === 'confirmed') return 'Đã tạo'
  if (draft.status === 'rejected') return 'Đã bỏ bản nháp'
  return 'Chờ xác nhận'
}

function nativeActionReadOnly(draft: AiNativeActionDraft) {
  return Boolean(draft.receipt) || draft.status !== 'pending_review'
}

function openNativeActionLink(path: string) {
  if (!/^\/(?:projects|groups|tasks)(?:\/[0-9a-f-]{36})?(?:[/?#].*)?$/i.test(path)) return
  void router.push(path)
}

async function confirmNativeAction(entry: ChatEntry, draft: AiNativeActionDraft) {
  if (nativeActionBusy.value || draft.receipt || draft.status !== 'pending_review') return
  nativeActionBusy.value = draft.draftId
  try {
    const receipt = await apiJson<AiNativeActionReceipt>(`/api/ai/native-actions/${draft.draftId}/confirm`, {
      method: 'POST',
      headers: { 'Idempotency-Key': `native-action:${draft.draftId}:${draft.rowVersion}` },
      body: JSON.stringify({
        expectedRevision: draft.revision,
        rowVersion: draft.rowVersion,
        payload: draft.payload,
      })
    })
    entry.nativeActionDraft = { ...draft, status: 'confirmed', receipt }
    showSuccess(receipt.replayed ? 'Thay đổi đã tồn tại; Qaly đã đọc lại receipt cũ.' : 'Đã tạo dữ liệu thật và đọc lại thành công.')
    await loadDashboard()
  } catch (error) {
    try {
      entry.nativeActionDraft = await apiJson<AiNativeActionDraft>(`/api/ai/native-actions/${draft.draftId}`)
    } catch {
      // Keep the reviewed draft visible when reconciliation is temporarily unavailable.
    }
    showError(error instanceof Error ? error.message : 'Không thể xác nhận thay đổi.')
  } finally {
    nativeActionBusy.value = null
  }
}

async function saveNativeAction(entry: ChatEntry, draft: AiNativeActionDraft) {
  if (nativeActionBusy.value || nativeActionReadOnly(draft)) return
  nativeActionBusy.value = draft.draftId
  try {
    entry.nativeActionDraft = await apiJson<AiNativeActionDraft>(`/api/ai/native-actions/${draft.draftId}`, {
      method: 'PATCH',
      body: JSON.stringify({
        expectedRevision: draft.revision,
        rowVersion: draft.rowVersion,
        payload: draft.payload,
      })
    })
    showSuccess('Đã lưu bản nháp trên máy chủ; có thể tiếp tục sau khi tải lại hoặc đổi phiên.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể lưu bản nháp.')
  } finally {
    nativeActionBusy.value = null
  }
}

async function rejectNativeAction(entry: ChatEntry, draft: AiNativeActionDraft) {
  if (nativeActionBusy.value || nativeActionReadOnly(draft)) return
  nativeActionBusy.value = draft.draftId
  try {
    entry.nativeActionDraft = await apiJson<AiNativeActionDraft>(`/api/ai/native-actions/${draft.draftId}/reject`, {
      method: 'POST',
      body: JSON.stringify({
        expectedRevision: draft.revision,
        rowVersion: draft.rowVersion,
      })
    })
    showSuccess('Đã bỏ bản nháp; không có dữ liệu domain nào được tạo.')
  } catch (error) {
    showError(error instanceof Error ? error.message : 'Không thể bỏ bản nháp.')
  } finally {
    nativeActionBusy.value = null
  }
}

function assignmentCandidateOptions(item: PortfolioScheduleProposalItem) {
  const current = {
    userId: item.proposedAssigneeId,
    fullName: item.proposedAssigneeName,
    skillCoveragePercent: item.skillCoveragePercent,
    remainingHours: Math.max(0, item.capacityHours - item.loadBeforeHours),
    tradeOff: 'Phương án Qaly đề xuất',
    evidenceConfidence: item.evidenceConfidence,
    loadBeforeHours: item.loadBeforeHours,
    capacityHours: item.capacityHours,
    blockingReasons: item.blockingReasons,
  }
  return [current, ...item.alternatives].filter((candidate, index, rows) =>
    rows.findIndex(other => other.userId === candidate.userId) === index)
}

function changeAssignmentCandidate(item: PortfolioScheduleProposalItem, event: Event) {
  const userId = (event.target as HTMLSelectElement).value
  const candidate = assignmentCandidateOptions(item).find(option => option.userId === userId)
  if (!candidate) return
  const estimatedHours = Math.max(0, item.loadAfterHours - item.loadBeforeHours)
  item.proposedAssigneeId = candidate.userId
  item.proposedAssigneeName = candidate.fullName
  item.skillCoveragePercent = candidate.skillCoveragePercent
  item.evidenceConfidence = candidate.evidenceConfidence
  item.loadBeforeHours = candidate.loadBeforeHours
  item.capacityHours = candidate.capacityHours
  item.loadAfterHours = candidate.loadBeforeHours + estimatedHours
  item.blockingReasons = candidate.blockingReasons ?? []
  item.selected = item.blockingReasons.length === 0
}

function toDateInput(value: string) {
  return value ? new Date(value).toISOString().slice(0, 10) : ''
}

function changeAssignmentDate(item: PortfolioScheduleProposalItem, field: 'proposedStart' | 'proposedDue', event: Event) {
  const value = (event.target as HTMLInputElement).value
  if (!value) return
  item[field] = new Date(`${value}T00:00:00.000Z`).toISOString()
}

async function confirmAssignmentProposal(entry: ChatEntry, proposal: PortfolioScheduleProposal) {
  if (assignmentProposalBusy.value || proposal.status !== 'pending_review') return
  const selectedItemIds = proposal.items.filter(item => item.selected).map(item => item.itemId)
  if (!selectedItemIds.length) {
    showError('Hãy chọn ít nhất một Task cần áp dụng.')
    return
  }
  assignmentProposalBusy.value = proposal.draftId
  try {
    const reviewed = await apiResult<PortfolioScheduleProposal>(
      `/api/projects/${proposal.projectId}/schedule-proposals/${proposal.draftId}`,
      { method: 'PATCH', body: JSON.stringify({ items: proposal.items, rowVersion: proposal.rowVersion }) },
    )
    const key = `assistant-assignment-confirm:${proposal.draftId}`
    const confirmed = await apiResult<PortfolioScheduleProposal>(
      `/api/projects/${proposal.projectId}/schedule-proposals/${proposal.draftId}/confirm`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': key },
        body: JSON.stringify({ selectedItemIds, rowVersion: reviewed.rowVersion, idempotencyKey: key, confirmed: true }),
      },
    )
    if (!confirmed.receipt?.readBackVerified) throw new Error('Qaly chưa đọc lại được Task canonical sau khi ghi.')
    entry.portfolioScheduleProposal = normalizePortfolioScheduleProposal(confirmed)
    showSuccess(`Đã áp dụng và đọc lại ${confirmed.receipt.appliedCount} Task.`)
    await loadDashboard()
  } catch (error) {
    try {
      entry.portfolioScheduleProposal = normalizePortfolioScheduleProposal(await apiResult<PortfolioScheduleProposal>(
        `/api/projects/${proposal.projectId}/schedule-proposals/${proposal.draftId}`,
      ))
    } catch {
      // Keep the reviewed card visible if a refresh is temporarily unavailable.
    }
    showError(error instanceof Error ? error.message : 'Không thể xác nhận phương án phân công.')
  } finally {
    assignmentProposalBusy.value = null
  }
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
    // Capture and persist the exact visible Project before creating the turn.
    // This closes the select-then-send race and keeps session scope, request
    // context and the composer chip on the same Project.
    const turnContext = assistantContextForCurrentRoute(requestedOrganizationId)
    await queueAssistantSessionScope(turnContext.projectId)
    chatHistory.value.push({ role: 'assistant', text: '' })
    const lastIdx = chatHistory.value.length - 1
    const attachedFileContexts = filesToSend.length ? await parseAttachedFiles(filesToSend) : []
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
          context: turnContext,
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
      safeTestRunReport: fastReply.safeTestRunReport,
      nativeActionDraft: fastReply.nativeActionDraft,
      portfolioScheduleProposal: fastReply.portfolioScheduleProposal
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
  restoreProjectLaunchWorkingDrafts()
  await loadConversationHistory()
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

watch([projectLaunchBriefDrafts, projectLaunchPlanDrafts], persistProjectLaunchWorkingDrafts, { deep: true })

let lastAppliedExternalPromptToken: number | null = null
watch(
  () => [props.externalPromptToken, props.externalProjectId, projects.value.length] as const,
  () => {
    if (lastAppliedExternalPromptToken === props.externalPromptToken) return
    const externalProjectId = props.externalProjectId?.trim() || ''
    // Project data can arrive after the launcher event. Wait until the target
    // can be represented truthfully in the visible selector.
    if (externalProjectId && !projects.value.some((project: { id: string }) => project.id === externalProjectId)) return
    if (externalProjectId) selectedTarget.value = externalProjectId
    lastAppliedExternalPromptToken = props.externalPromptToken
    if (props.externalPrompt.trim()) fillComposer(props.externalPrompt.trim())
    else nextTick(() => textareaRef.value?.focus())
  },
  { immediate: true },
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
  persistProjectLaunchWorkingDrafts()
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
      <div class="empty-header-actions">
        <button type="button" class="empty-session-history" data-testid="assistant-session-history" @click="openSessionHistory">
          <Clock3 :size="16" aria-hidden="true" /> Phiên
        </button>
        <OverflowMenu
          :items="headerMenuItems"
          aria-label="Công cụ và tùy chọn Trợ lý AI"
          trigger-title="Mở công cụ và tùy chọn"
          @select="handleHeaderMenu"
        />
      </div>
      <div class="empty-inner">
        <div class="empty-avatar">
          <ChatbotAvatar size="medium" />
        </div>
        <div class="empty-intro">
          <h1 class="empty-heading">Bạn muốn Qaly giúp gì?</h1>
          <p>Hỏi tự nhiên, xem phân tích hoặc chuẩn bị bản nháp có xác nhận.</p>
          <div class="newcomer-shortcuts" aria-label="Bắt đầu nhanh">
            <button
              v-for="shortcut in newcomerShortcuts"
              :key="shortcut.label"
              type="button"
              :title="shortcut.description"
              @click="fillComposer(shortcut.prompt)"
            >{{ shortcut.label }}</button>
          </div>
          <small>Gợi ý chỉ điền câu hỏi; bạn vẫn kiểm tra trước khi gửi.</small>
        </div>

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
            aria-label="Công cụ và tùy chọn Trợ lý AI"
            trigger-title="Mở công cụ và tùy chọn"
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
            :data-message-index="i"
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
                        <span v-else-if="msg.goalAnalysis.actualProvider !== 'not_reached' && msg.goalAnalysis.actualModel !== 'not_reached'">{{ msg.goalAnalysis.actualProvider }} / {{ msg.goalAnalysis.actualModel }}</span>
                        <span v-else>Qaly server · chưa gọi model</span>
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
                    v-if="msg.nativeActionDraft"
                    class="project-launch-brief-card native-action-card"
                    data-testid="native-action-draft"
                  >
                    <header class="project-launch-brief-header">
                      <div>
                        <span>AI Native · bản nháp có cấu trúc</span>
                        <h3>{{ nativeActionTitle(msg.nativeActionDraft) }}</h3>
                        <p>Kiểm tra và chỉnh trực tiếp. Chỉ tạo dữ liệu thật sau nút xác nhận bên dưới.</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ nativeActionStatusLabel(msg.nativeActionDraft) }}</strong>
                        <small>Revision {{ msg.nativeActionDraft.revision }}</small>
                      </div>
                    </header>

                    <section v-if="msg.nativeActionDraft.capabilityId === 'task.acceptance_checklist.v1'" class="native-action-editor">
                      <label v-for="(_item, index) in msg.nativeActionDraft.payload.items" :key="index">
                        Tiêu chí {{ Number(index) + 1 }}
                        <input v-model="msg.nativeActionDraft.payload.items[index]" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" />
                      </label>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'task.breakdown.v1'" class="native-action-editor">
                      <label v-for="(item, index) in msg.nativeActionDraft.payload.subtasks" :key="index">
                        Subtask {{ Number(index) + 1 }}
                        <input v-model="item.title" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" />
                        <textarea v-model="item.description" rows="2" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" />
                        <span class="native-action-inline-fields">
                          <label>Ưu tiên<select v-model="item.priority" :disabled="nativeActionReadOnly(msg.nativeActionDraft)"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select></label>
                          <label>Ước lượng (giờ)<input v-model.number="item.estimatedHours" type="number" min="1" max="10000" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        </span>
                        <label>Kỹ năng bắt buộc
                          <input :value="item.requiredSkillName || 'Chưa có kỹ năng phù hợp trong catalog'" disabled />
                          <small>Kỹ năng lấy từ catalog của tổ chức và sẽ được lưu cùng subtask sau xác nhận.</small>
                        </label>
                        <span v-if="Number(index) > 0" class="native-action-check"><input v-model="item.dependsOnPrevious" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Phụ thuộc subtask trước</span>
                      </label>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'wiki.brief_task.v1'" class="native-action-editor">
                      <label>Tóm tắt có nguồn<textarea v-model="msg.nativeActionDraft.payload.summary" rows="4" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      <small v-for="source in msg.nativeActionDraft.payload.sectionRefs" :key="source">Nguồn: {{ source }}</small>
                      <template v-if="msg.nativeActionDraft.payload.taskCandidates?.length">
                        <h4>Task tùy chọn (tối đa 3)</h4>
                        <article v-for="(candidate, index) in msg.nativeActionDraft.payload.taskCandidates" :key="candidate.clientId" class="native-action-subcard">
                          <label class="native-action-check"><input v-model="candidate.selected" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Chọn Task {{ Number(index) + 1 }}</label>
                          <label>Tiêu đề<input v-model="candidate.title" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                          <label>Mô tả<textarea v-model="candidate.description" rows="3" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                          <small>Nguồn: {{ candidate.sourceRef }}</small>
                        </article>
                      </template>
                      <template v-else>
                        <label class="native-action-check"><input v-model="msg.nativeActionDraft.payload.createTask" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Tạo Task từ brief</label>
                        <label v-if="msg.nativeActionDraft.payload.createTask">Tiêu đề Task<input v-model="msg.nativeActionDraft.payload.taskTitle" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <label v-if="msg.nativeActionDraft.payload.createTask">Mô tả Task<textarea v-model="msg.nativeActionDraft.payload.taskDescription" rows="3" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      </template>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'group.poll.create.v1'" class="native-action-editor">
                      <label>Câu hỏi<input v-model="msg.nativeActionDraft.payload.question" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      <label v-for="(_option, index) in msg.nativeActionDraft.payload.options" :key="index">Lựa chọn {{ Number(index) + 1 }}<input v-model="msg.nativeActionDraft.payload.options[index]" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      <label class="native-action-check"><input v-model="msg.nativeActionDraft.payload.allowMultiple" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Cho phép chọn nhiều</label>
                      <label>Hạn bình chọn<input v-model="msg.nativeActionDraft.payload.expiredAt" type="datetime-local" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'project.digest.configure.v1'" class="native-action-editor native-action-grid">
                      <label class="native-action-check"><input v-model="msg.nativeActionDraft.payload.isEnabled" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Bật gửi tổng hợp</label>
                      <label>Ngày gửi<select v-model.number="msg.nativeActionDraft.payload.dayOfWeek" :disabled="nativeActionReadOnly(msg.nativeActionDraft)"><option :value="1">Thứ Hai</option><option :value="2">Thứ Ba</option><option :value="3">Thứ Tư</option><option :value="4">Thứ Năm</option><option :value="5">Thứ Sáu</option><option :value="6">Thứ Bảy</option><option :value="0">Chủ Nhật</option></select></label>
                      <label>Giờ gửi (phút từ 00:00)<input v-model.number="msg.nativeActionDraft.payload.localTimeMinutes" type="number" min="0" max="1439" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      <label>Múi giờ<input v-model="msg.nativeActionDraft.payload.timeZoneId" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'meeting.actions.review.v1'" class="native-action-editor">
                      <label>Tóm tắt có nguồn<textarea v-model="msg.nativeActionDraft.payload.summary" rows="3" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                      <div v-if="msg.nativeActionDraft.payload.decisions?.length" class="native-action-subcard"><strong>Quyết định</strong><ul><li v-for="decision in msg.nativeActionDraft.payload.decisions" :key="decision">{{ decision }}</li></ul></div>
                      <div v-if="msg.nativeActionDraft.payload.blockers?.length" class="native-action-subcard"><strong>Blocker/rủi ro</strong><ul><li v-for="blocker in msg.nativeActionDraft.payload.blockers" :key="blocker">{{ blocker }}</li></ul></div>
                      <article v-for="action in msg.nativeActionDraft.payload.actionItems" :key="action.itemIndex" class="native-action-subcard">
                        <label>Action item<input v-model="action.title" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <label>Xử lý<select v-model="action.mappingMode" :disabled="nativeActionReadOnly(msg.nativeActionDraft)"><option value="none">Chưa map — không ghi</option><option value="existing_task">Map vào Task có sẵn</option><option value="new_task">Tạo bản nháp Task mới</option></select></label>
                        <label v-if="action.mappingMode === 'existing_task'">Task có sẵn<select v-model="action.existingTaskId" :disabled="nativeActionReadOnly(msg.nativeActionDraft)"><option :value="null">Chọn Task…</option><option v-for="task in msg.nativeActionDraft.payload.existingTaskOptions" :key="task.taskId" :value="task.taskId">{{ task.title }}</option></select></label>
                        <label v-if="action.mappingMode === 'new_task'">Mô tả Task<textarea v-model="action.description" rows="2" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <small>Nguồn transcript: {{ action.sourceEvidence || 'Đoạn nguồn đã lưu cùng extraction' }}</small>
                      </article>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'project.roadmap.adjust.v1'" class="native-action-editor">
                      <p>{{ msg.nativeActionDraft.payload.summary }}</p>
                      <article v-for="adjustment in msg.nativeActionDraft.payload.adjustments" :key="adjustment.sprintId || adjustment.sprintName" class="native-action-subcard">
                        <label class="native-action-check"><input v-model="adjustment.selected" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> Áp dụng điều chỉnh này</label>
                        <label>Tên Sprint<input v-model="adjustment.sprintName" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <div class="native-action-grid"><span><strong>Trước</strong><br />{{ adjustment.beforeStart }} → {{ adjustment.beforeEnd }}</span><span><strong>Sau</strong><br />{{ adjustment.afterStart }} → {{ adjustment.afterEnd }}</span></div>
                        <label>Bắt đầu sau điều chỉnh<input v-model="adjustment.afterStart" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <label>Kết thúc sau điều chỉnh<input v-model="adjustment.afterEnd" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /></label>
                        <small>{{ adjustment.reason }}</small>
                      </article>
                    </section>

                    <section v-else-if="msg.nativeActionDraft.capabilityId === 'task.skill_evidence.confirm.v1'" class="native-action-editor">
                      <div class="native-action-subcard"><strong>Acceptance đã xác nhận</strong><ul><li v-for="evidence in msg.nativeActionDraft.payload.acceptanceEvidence" :key="evidence">{{ evidence }}</li></ul></div>
                      <div class="native-action-subcard"><strong>Kỹ năng từ catalog của Task</strong><ul><li v-for="skill in msg.nativeActionDraft.payload.skills" :key="skill.skillId">{{ skill.name }} · {{ skill.requiredLevel }}</li></ul></div>
                      <label v-for="candidate in msg.nativeActionDraft.payload.contributors" :key="candidate.userId" class="native-action-check"><input v-model="candidate.selected" type="checkbox" :disabled="nativeActionReadOnly(msg.nativeActionDraft)" /> {{ candidate.name }}</label>
                      <small>Không sử dụng label hoặc tin nhắn riêng làm bằng chứng.</small>
                    </section>

                    <section v-if="msg.nativeActionDraft.receipt" class="launch-receipt" data-testid="native-action-receipt">
                      <h4>Receipt · read-back verified</h4>
                      <button
                        v-for="item in msg.nativeActionDraft.receipt.items"
                        :key="`${item.entityType}-${item.entityId}`"
                        type="button"
                        @click="openNativeActionLink(item.url)"
                      >{{ item.label }} →</button>
                    </section>
                    <div v-else-if="msg.nativeActionDraft.status === 'pending_review'" class="native-action-controls">
                      <button
                        type="button"
                        class="secondary-button"
                        :disabled="Boolean(nativeActionBusy)"
                        data-testid="native-action-save"
                        @click="saveNativeAction(msg, msg.nativeActionDraft)"
                      >Lưu bản nháp</button>
                      <button
                        type="button"
                        class="danger-button"
                        :disabled="Boolean(nativeActionBusy)"
                        data-testid="native-action-reject"
                        @click="rejectNativeAction(msg, msg.nativeActionDraft)"
                      >Bỏ bản nháp</button>
                      <button
                        type="button"
                        class="launch-primary-action"
                        :disabled="Boolean(nativeActionBusy)"
                        data-testid="native-action-confirm"
                        @click="confirmNativeAction(msg, msg.nativeActionDraft)"
                      >{{ nativeActionBusy === msg.nativeActionDraft.draftId ? 'Đang xử lý…' : 'Xác nhận và tạo dữ liệu thật' }}</button>
                    </div>
                    <p v-else class="native-action-closed">Bản nháp đã được bỏ; không có dữ liệu domain nào được tạo.</p>
                  </article>

                  <article
                    v-if="msg.portfolioScheduleProposal"
                    class="project-launch-brief-card assignment-proposal-card"
                    data-testid="assistant-assignment-proposal"
                  >
                    <header class="project-launch-brief-header">
                      <div>
                        <span>Phương án giao việc & lịch · dữ liệu thật</span>
                        <h3>{{ msg.portfolioScheduleProposal.items[0]?.taskTitle }}</h3>
                        <p>So khớp kỹ năng, bằng chứng đã xác nhận, capacity, lịch vắng, deadline và tải trên mọi dự án.</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ msg.portfolioScheduleProposal.receipt?.readBackVerified ? 'Đã áp dụng' : 'Chưa ghi dữ liệu' }}</strong>
                        <small>{{ msg.portfolioScheduleProposal.providerName }} / {{ msg.portfolioScheduleProposal.modelName }}</small>
                      </div>
                    </header>
                    <section
                      v-for="item in msg.portfolioScheduleProposal.items"
                      :key="item.itemId"
                      class="assignment-proposal-item"
                    >
                      <label class="native-action-check"><input v-model="item.selected" type="checkbox" :disabled="msg.portfolioScheduleProposal.status !== 'pending_review' || item.blockingReasons.length > 0" /> Áp dụng Task này</label>
                      <div class="assignment-proposal-grid">
                        <label>Người thực hiện
                          <select :value="item.proposedAssigneeId" :disabled="msg.portfolioScheduleProposal.status !== 'pending_review'" @change="changeAssignmentCandidate(item, $event)">
                            <option v-for="candidate in assignmentCandidateOptions(item)" :key="candidate.userId" :value="candidate.userId">
                              {{ candidate.fullName }} · skill {{ candidate.skillCoveragePercent }}% · còn {{ candidate.remainingHours }}h
                            </option>
                          </select>
                        </label>
                        <label>Bắt đầu<input type="date" :value="toDateInput(item.proposedStart)" :disabled="msg.portfolioScheduleProposal.status !== 'pending_review'" @change="changeAssignmentDate(item, 'proposedStart', $event)" /></label>
                        <label>Hạn hoàn thành<input type="date" :value="toDateInput(item.proposedDue)" :disabled="msg.portfolioScheduleProposal.status !== 'pending_review'" @change="changeAssignmentDate(item, 'proposedDue', $event)" /></label>
                      </div>
                      <div class="assignment-facts">
                        <span>Skill match <strong>{{ item.skillCoveragePercent }}%</strong></span>
                        <span>Evidence <strong>{{ Math.round(item.evidenceConfidence * 100) }}%</strong></span>
                        <span>Tải <strong>{{ item.loadAfterHours }}/{{ item.capacityHours }}h</strong></span>
                      </div>
                      <details v-if="item.alternatives.length"><summary>Ứng viên thay thế ({{ item.alternatives.length }})</summary><ul><li v-for="candidate in item.alternatives" :key="candidate.userId"><strong>{{ candidate.fullName }}</strong> · {{ candidate.tradeOff }}</li></ul></details>
                      <section v-if="item.blockingReasons.length" class="assignment-blockers" role="alert"><strong>Chưa thể xác nhận phương án này</strong><ul><li v-for="reason in item.blockingReasons" :key="reason">{{ reason }}</li></ul><small>Chọn ứng viên an toàn khác hoặc cập nhật capacity/availability rồi lập lại phương án.</small></section>
                      <details v-if="item.deadlineRisks.length || item.dependencyConflicts.length"><summary>Cảnh báo cần xem ({{ item.deadlineRisks.length + item.dependencyConflicts.length }})</summary><ul><li v-for="warning in [...item.deadlineRisks, ...item.dependencyConflicts]" :key="warning">{{ warning }}</li></ul></details>
                    </section>
                    <section v-if="msg.portfolioScheduleProposal.receipt" class="launch-receipt" data-testid="assistant-assignment-receipt">
                      <h4>Đã đọc lại dữ liệu canonical</h4>
                      <button v-for="link in msg.portfolioScheduleProposal.receipt.readBackLinks" :key="link" type="button" @click="openNativeActionLink(link)">Mở Task →</button>
                    </section>
                    <footer v-else class="native-action-controls">
                      <small>Bạn có thể đổi người hoặc ngày; một lần xác nhận sẽ lưu bản review rồi mới áp dụng.</small>
                      <button type="button" class="launch-primary-action" :disabled="Boolean(assignmentProposalBusy) || !msg.portfolioScheduleProposal.items.some(item => item.selected && item.blockingReasons.length === 0)" data-testid="assistant-assignment-confirm" @click="confirmAssignmentProposal(msg, msg.portfolioScheduleProposal)">{{ assignmentProposalBusy === msg.portfolioScheduleProposal.draftId ? 'Đang kiểm tra và đọc lại…' : 'Xác nhận giao việc & lịch' }}</button>
                    </footer>
                  </article>

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
                        <span>Project Launch Brief · chỉnh sửa trước khi tạo</span>
                        <h3>{{ msg.projectLaunchBrief.proposedProjectName }}</h3>
                        <p>{{ msg.projectLaunchBrief.objective }}</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ launchBriefStateLabel(msg.projectLaunchBrief.state) }}</strong>
                        <small>Bản {{ msg.projectLaunchBrief.revision }}</small>
                        <small>{{ msg.projectLaunchBrief.organizationName }}</small>
                      </div>
                    </header>
                    <section class="project-launch-review-form project-launch-workspace" aria-label="Chỉnh sửa Project Launch Brief">
                      <nav class="launch-stepper" aria-label="Tiến trình khởi tạo dự án">
                        <span class="active">1. Mục tiêu</span><span class="active">2. Phạm vi</span><span>3. Nhân sự</span><span>4. Sprint</span><span>5. Xác nhận</span>
                      </nav>
                      <div class="launch-simple-guide project-launch-form-field--wide">
                        <strong>Bạn chỉ cần kiểm tra 4 mục</strong>
                        <span>Tên dự án · kết quả cần đạt · thời hạn · người dùng chính.</span>
                        <small>Cách đo, kỹ năng và tiêu chí chi tiết đã được AI soạn sẵn và có thể chỉnh sau.</small>
                      </div>

                      <div class="project-launch-form-field project-launch-form-field--wide">
                        <label :for="`launch-name-${msg.projectLaunchBrief.briefId}`">Tên dự án <b>*</b></label>
                        <input
                          :id="`launch-name-${msg.projectLaunchBrief.briefId}`"
                          type="text"
                          :value="launchBriefDraft(msg.projectLaunchBrief).projectName"
                          placeholder="Ví dụ: Nền tảng dịch vụ SPA"
                          @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'projectName', $event)"
                        >
                      </div>

                      <section class="launch-builder-block project-launch-form-field--wide">
                        <header class="launch-friendly-header">
                          <div><strong>Kết quả dự án cần đạt</strong><small>AI đã soạn bản nháp. Bạn chỉ cần đọc và sửa nếu chưa đúng ý.</small></div>
                          <span>AI đề xuất</span>
                        </header>
                        <label class="project-launch-form-field project-launch-form-field--wide">Khi dự án hoàn thành, điều gì phải hoạt động? <b>*</b>
                          <textarea rows="2" :value="launchBriefDraft(msg.projectLaunchBrief).objective" placeholder="Ví dụ: Khách hàng có thể tìm, đặt và thanh toán dịch vụ theo gói trên web." @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'objective', $event)" />
                          <small class="launch-field-hint">Viết một câu về kết quả người dùng nhận được; không cần mô tả cách AI lập kế hoạch.</small>
                        </label>
                        <details class="launch-optional-goal-details">
                          <summary>Mô tả thêm về vấn đề và giá trị mang lại <span>Không bắt buộc</span></summary>
                          <div class="project-launch-form-more-grid">
                            <label class="project-launch-form-field">Vấn đề hiện nay
                              <textarea rows="2" :value="launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.problemStatement" placeholder="Ví dụ: Khách phải liên hệ thủ công để biết gói còn trống." @input="updateLaunchObjectiveField(msg.projectLaunchBrief, 'problemStatement', $event)" />
                            </label>
                            <label class="project-launch-form-field">Giá trị mang lại
                              <textarea rows="2" :value="launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.businessValue" placeholder="Ví dụ: Đặt dịch vụ nhanh hơn và giảm thao tác xử lý thủ công." @input="updateLaunchObjectiveField(msg.projectLaunchBrief, 'businessValue', $event)" />
                            </label>
                          </div>
                        </details>

                        <details class="launch-metrics-review">
                          <summary>
                            <span><strong>{{ launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.metrics.length }} cách kiểm tra thành công</strong><small>AI đã chọn cách đo và nguồn dữ liệu; chưa cần nhập con số.</small></span>
                            <em>Xem hoặc chỉnh</em>
                          </summary>
                          <div class="launch-template-row"><span>Thêm mẫu phổ biến:</span><button v-for="item in launchMetricTemplates" :key="item.title" type="button" @click="addLaunchMetric(msg.projectLaunchBrief, item.title)">+ {{ item.title }}</button></div>
                          <div class="launch-metric-grid">
                            <article v-for="metric in launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.metrics" :key="metric.metricId" class="launch-metric-card">
                              <input :value="metric.title" aria-label="Tên thước đo" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'title', $event)">
                              <div class="launch-metric-simple-fields">
                                <label>Hướng cải thiện
                                  <select :value="metric.metricType" @change="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'metricType', $event)">
                                    <option value="increase">Tăng lên</option><option value="decrease">Giảm xuống</option><option value="maintain">Duy trì ổn định</option><option value="delivery">Hoàn thành đúng cam kết</option>
                                  </select>
                                </label>
                                <label>Đo ở đâu
                                  <input :list="`launch-source-${msg.projectLaunchBrief.briefId}`" :value="metric.dataSource || ''" placeholder="Chọn hoặc nhập nguồn khác" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'dataSource', $event)">
                                </label>
                              </div>
                              <p class="launch-metric-summary">{{ launchMetricIntentLabel(metric.metricType) }} · kiểm tra trong {{ metric.measurementWindow || launchBriefDraft(msg.projectLaunchBrief).targetTimebox || 'timebox đã chọn' }}.</p>
                              <details class="launch-metric-advanced">
                                <summary>Thêm con số cụ thể và người phụ trách <span>Không bắt buộc</span></summary>
                                <div><label>Giá trị hiện nay<input type="number" :value="metric.baseline ?? ''" :placeholder="launchMetricValueExample(metric.title, 'baseline')" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'baseline', $event)"></label><label>Mục tiêu muốn đạt<input type="number" :value="metric.target ?? ''" :placeholder="launchMetricValueExample(metric.title, 'target')" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'target', $event)"></label></div>
                                <div><label>Đơn vị<input :list="`launch-units-${msg.projectLaunchBrief.briefId}`" :value="metric.unit || ''" placeholder="Chọn hoặc nhập đơn vị" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'unit', $event)"></label><label>Người theo dõi<input :list="`launch-owners-${msg.projectLaunchBrief.briefId}`" :value="metric.owner || ''" placeholder="Chọn vai trò hoặc nhập tên" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'owner', $event)"></label></div>
                                <label>Thời điểm kiểm tra<input :value="metric.measurementWindow || ''" placeholder="Ví dụ: cuối mỗi Sprint" @input="updateLaunchMetric(msg.projectLaunchBrief, metric.metricId, 'measurementWindow', $event)"></label>
                                <small>{{ metric.baseline == null || metric.target == null ? 'Có thể bổ sung sau khi đã có dữ liệu thật; Qaly không tự bịa số.' : 'Đã có giá trị để kiểm chứng.' }}</small>
                              </details>
                              <div class="launch-card-actions"><button type="button" @click="moveLaunchMetric(msg.projectLaunchBrief, metric.metricId, -1)">Lên</button><button type="button" @click="moveLaunchMetric(msg.projectLaunchBrief, metric.metricId, 1)">Xuống</button><button type="button" class="launch-link-danger" @click="removeLaunchMetric(msg.projectLaunchBrief, metric.metricId)">Bỏ</button></div>
                            </article>
                          </div>
                          <datalist :id="`launch-source-${msg.projectLaunchBrief.briefId}`"><option value="Qaly Sprint / Task"/><option value="Product analytics"/><option value="Nhật ký hệ thống"/><option value="Monitoring"/><option value="Khảo sát người dùng"/></datalist>
                          <datalist :id="`launch-units-${msg.projectLaunchBrief.briefId}`"><option value="%"/><option value="phút"/><option value="lượt"/><option value="lỗi/tháng"/><option value="điểm CSAT"/></datalist>
                          <datalist :id="`launch-owners-${msg.projectLaunchBrief.briefId}`"><option value="Project Manager"/><option value="Product Owner"/><option value="Tech Lead"/><option value="QA Lead"/></datalist>
                        </details>
                      </section>

                      <div class="project-launch-form-field">
                        <label>Thời hạn <b>*</b></label>
                        <div class="launch-choice-row"><button v-for="item in launchTimeboxOptions" :key="item" type="button" :class="{ selected: launchBriefDraft(msg.projectLaunchBrief).targetTimebox === item }" @click="setLaunchBriefChoice(msg.projectLaunchBrief, 'targetTimebox', item)">{{ item }}</button></div>
                        <input :value="launchBriefDraft(msg.projectLaunchBrief).targetTimebox" placeholder="Khác, ví dụ: 10 tuần hoặc 30/11/2026" @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'targetTimebox', $event)">
                        <small class="launch-field-hint">Chọn nhanh ở trên, hoặc nhập thời hạn riêng.</small>
                      </div>
                      <div class="project-launch-form-field">
                        <label>Người dùng chính <b>*</b></label>
                        <div class="launch-choice-row"><button v-for="item in launchAudienceOptions" :key="item" type="button" :class="{ selected: launchBriefDraft(msg.projectLaunchBrief).primaryAudience === item }" @click="setLaunchBriefChoice(msg.projectLaunchBrief, 'primaryAudience', item)">{{ item }}</button></div>
                        <input :value="launchBriefDraft(msg.projectLaunchBrief).primaryAudience" placeholder="Khác, ví dụ: chủ spa hoặc khách đặt dịch vụ" @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'primaryAudience', $event)">
                        <small class="launch-field-hint">Chọn nhóm sử dụng sản phẩm thường xuyên nhất.</small>
                      </div>

                      <section class="launch-builder-block project-launch-form-field--wide">
                        <header><div><strong>Phạm vi và chức năng <b>*</b></strong><small>Những mục được chọn sẽ được truy vết sang Sprint, Task và kỹ năng.</small></div></header>
                        <div class="launch-template-row launch-template-grid"><button v-for="item in launchFeatureTemplates" :key="item.title" type="button" :class="{ selected: launchBriefDraft(msg.projectLaunchBrief).features.some(feature => feature.title === item.title && feature.selected) }" @click="toggleLaunchFeatureTemplate(msg.projectLaunchBrief, item)">{{ item.title }}</button></div>
                        <div class="launch-custom-add"><input :value="launchBriefDraft(msg.projectLaunchBrief).customFeatureTitle" placeholder="Thêm chức năng khác" @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'customFeatureTitle', $event)" @keydown.enter.prevent="addCustomLaunchFeature(msg.projectLaunchBrief)"><button type="button" @click="addCustomLaunchFeature(msg.projectLaunchBrief)">Thêm</button></div>
                        <div class="launch-feature-list">
                          <article v-for="feature in launchBriefDraft(msg.projectLaunchBrief).features.filter(item => item.selected)" :key="feature.featureId" class="launch-feature-card">
                            <input :value="feature.title" aria-label="Tên chức năng" @input="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'title', $event)">
                            <div><select :value="feature.priority" @change="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'priority', $event)"><option value="must_have">Bắt buộc</option><option value="should_have">Nên có</option><option value="could_have">Có thể thêm</option><option value="out_of_scope">Ngoài phạm vi</option></select><input :value="feature.category" aria-label="Nhóm chức năng" @input="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'category', $event)"></div>
                            <details><summary>Mô tả và tiêu chí nghiệm thu</summary><div class="launch-feature-detail"><label>Mô tả<textarea rows="2" :value="feature.description" @input="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'description', $event)" /></label><label>Nhóm người dùng<input :value="feature.primaryAudience" @input="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'primaryAudience', $event)"></label><label>Tiêu chí nghiệm thu — mỗi dòng một ý<textarea rows="3" :value="feature.acceptanceCriteria.join('\n')" @input="updateLaunchFeature(msg.projectLaunchBrief, feature.featureId, 'acceptanceCriteria', $event)" /></label></div></details>
                            <details><summary>Kỹ năng cần thiết ({{ feature.requiredSkillNames.length }})</summary><div class="launch-skill-options"><button v-for="skill in msg.projectLaunchBrief.skillCatalog || []" :key="skill.skillId" type="button" :class="{ selected: feature.requiredSkillNames.includes(skill.name) }" @click="toggleLaunchFeatureSkill(msg.projectLaunchBrief, feature.featureId, skill.name)">{{ skill.name }}</button></div><input class="launch-custom-skill" placeholder="Thêm kỹ năng chuyên sâu rồi nhấn Enter" @keydown.enter.prevent="addCustomLaunchFeatureSkill(msg.projectLaunchBrief, feature.featureId, $event)"><small>Kỹ năng chưa có trong catalog sẽ được giữ là khoảng trống cần xác minh, không gán giả cho thành viên.</small></details>
                            <div class="launch-card-actions"><button type="button" @click="moveLaunchFeature(msg.projectLaunchBrief, feature.featureId, -1)">Lên</button><button type="button" @click="moveLaunchFeature(msg.projectLaunchBrief, feature.featureId, 1)">Xuống</button><button type="button" class="launch-link-danger" @click="toggleLaunchFeatureTemplate(msg.projectLaunchBrief, { title: feature.title, category: feature.category })">Bỏ chọn</button></div>
                          </article>
                        </div>
                      </section>

                      <details class="project-launch-form-more project-launch-form-field--wide">
                        <summary>Giả định, ngoài phạm vi và tùy chỉnh nâng cao</summary>
                        <div class="project-launch-form-more-grid"><label class="project-launch-form-field">Điều không được đánh đổi<textarea rows="3" :value="launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.guardrails.join('\n')" placeholder="Mỗi dòng một guardrail" @input="updateLaunchObjectiveList(msg.projectLaunchBrief, 'guardrails', $event)" /></label><label class="project-launch-form-field">Giả định tạm thời<textarea rows="3" :value="launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.assumptions.join('\n')" placeholder="Mỗi dòng một giả định" @input="updateLaunchObjectiveList(msg.projectLaunchBrief, 'assumptions', $event)" /></label><label class="project-launch-form-field">Không nằm trong mục tiêu<textarea rows="3" :value="launchBriefDraft(msg.projectLaunchBrief).objectiveProfile.nonGoals.join('\n')" placeholder="Mỗi dòng một non-goal" @input="updateLaunchObjectiveList(msg.projectLaunchBrief, 'nonGoals', $event)" /></label><label class="project-launch-form-field">Ngoài phạm vi kỹ thuật/tích hợp<textarea rows="3" :value="launchBriefDraft(msg.projectLaunchBrief).exclusions" @input="updateLaunchBriefDraft(msg.projectLaunchBrief, 'exclusions', $event)" /></label><p class="launch-help">Số liệu hiện tại hoặc mục tiêu còn thiếu được giữ ở trạng thái “Cần xác nhận”, không được biến thành dữ liệu thật.</p></div>
                      </details>
                      <div class="project-launch-form-actions project-launch-form-field--wide">
                        <small v-if="launchBriefMissingFields(msg.projectLaunchBrief).length">Còn thiếu: {{ launchBriefMissingFields(msg.projectLaunchBrief).join(', ') }}.</small>
                        <small v-else>Thông tin bắt buộc đã đủ; bạn vẫn có thể chỉnh mọi card trước khi lập phương án.</small>
                        <button v-if="!launchBriefDraft(msg.projectLaunchBrief).targetTimebox" type="button" class="secondary-button" @click="applyLaunchBriefSuggestions(msg.projectLaunchBrief)">Gợi ý thời hạn 8 tuần</button>
                        <button type="button" class="launch-primary-action" :disabled="isChatting || !canSubmitLaunchBriefDraft(msg.projectLaunchBrief)" data-testid="project-launch-brief-save" @click="submitLaunchBriefReview(msg.projectLaunchBrief)">Lưu và lập phương án</button>
                      </div>
                    </section>
                    <div class="project-launch-rulebook" :class="`status-${msg.projectLaunchBrief.rulebookStatus}`">
                      <strong>Quy tắc làm việc của tổ chức</strong>
                      <span v-if="msg.projectLaunchBrief.ruleSetVersion">Bản {{ msg.projectLaunchBrief.ruleSetVersion }} đang áp dụng</span>
                      <span v-else>Chưa có quy tắc đang áp dụng</span>
                      <button
                        v-if="canManageProjectLaunch(msg) && msg.projectLaunchBrief.rulebookStatus === 'policy_missing' && !rulebookDrafts[msg.projectLaunchBrief.organizationId] && !rulebookReviewRules[msg.projectLaunchBrief.organizationId]"
                        type="button"
                        :disabled="launchActionBusy === `rulebook:${msg.projectLaunchBrief.organizationId}`"
                        @click="startRecommendedRulebookReview(msg.projectLaunchBrief)"
                      >Xem và tùy chỉnh bộ quy tắc đề xuất</button>
                      <section
                        v-if="canManageProjectLaunch(msg) && rulebookReviewRules[msg.projectLaunchBrief.organizationId] && !rulebookDrafts[msg.projectLaunchBrief.organizationId]"
                        class="rulebook-review-editor"
                      >
                        <p>Kiểm tra giới hạn trước khi lưu. Đây mới là bản review, chưa áp dụng vào tổ chức.</p>
                        <label
                          v-for="rule in rulebookReviewRules[msg.projectLaunchBrief.organizationId]"
                          :key="rule.ruleKey"
                          class="rulebook-review-rule"
                        >
                          <input type="checkbox" :checked="rule.enabled" @change="updateRulebookReviewRule(msg.projectLaunchBrief.organizationId, rule.ruleKey, 'enabled', $event)">
                          <span><strong>{{ rule.description }}</strong><small>{{ rule.ruleKey }} · {{ rule.enforcement === 'block' ? 'Bắt buộc' : 'Cảnh báo' }}</small></span>
                          <span v-if="rule.numericValue != null" class="rulebook-review-value">
                            <input
                              type="number"
                              :min="rulebookRuleMin(rule.ruleKey)"
                              :max="rulebookRuleMax(rule.ruleKey)"
                              :value="rule.numericValue"
                              :disabled="!rule.enabled"
                              @input="updateRulebookReviewRule(msg.projectLaunchBrief.organizationId, rule.ruleKey, 'numericValue', $event)"
                            >
                            <small>{{ rule.unit === 'percent' ? '%' : 'dự án' }}</small>
                          </span>
                        </label>
                        <div class="rulebook-review-actions">
                          <button type="button" @click="cancelRecommendedRulebookReview(msg.projectLaunchBrief.organizationId)">Hủy</button>
                          <button type="button" :disabled="launchActionBusy === `rulebook:${msg.projectLaunchBrief.organizationId}`" @click="createRecommendedRulebookDraft(msg.projectLaunchBrief)">Lưu bản nháp đã review</button>
                        </div>
                      </section>
                      <details
                        v-else-if="canManageProjectLaunch(msg) && rulebookDrafts[msg.projectLaunchBrief.organizationId]?.status === 'draft'"
                        class="rulebook-draft-review"
                      >
                        <summary>Xem bản {{ rulebookDrafts[msg.projectLaunchBrief.organizationId].version }} trước khi áp dụng</summary>
                        <ul>
                          <li v-for="rule in rulebookDrafts[msg.projectLaunchBrief.organizationId].rules" :key="rule.ruleKey">
                            <strong>{{ rule.description }}</strong>
                            <span v-if="rule.numericValue != null">{{ rule.numericValue }} {{ rule.unit === 'percent' ? '%' : 'dự án' }}</span>
                            <span v-else>{{ rule.enabled ? 'Bật' : 'Tắt' }}</span>
                          </li>
                        </ul>
                        <button
                          type="button"
                          :disabled="launchActionBusy === `rulebook:${msg.projectLaunchBrief.organizationId}`"
                          @click="activateRulebookAndResume(msg.projectLaunchBrief)"
                        >Áp dụng bản đã review</button>
                      </details>
                    </div>
                    <details class="project-launch-summary-details">
                      <summary>Tóm tắt phạm vi, thước đo và các điểm cần lưu ý</summary>
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
                        <h4>Điểm cần làm rõ</h4>
                        <p v-if="!msg.projectLaunchBrief.unknowns.length">Không còn điểm nào cần làm rõ.</p>
                        <ul v-else><li v-for="item in msg.projectLaunchBrief.unknowns" :key="item">{{ item }}</li></ul>
                      </section>
                    </div>
                    <details class="project-launch-decisions">
                      <summary>Chi tiết kiểm tra quy tắc ({{ msg.projectLaunchBrief.ruleDecisions.length }})</summary>
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
                    </details>
                    <footer>
                      <strong>Chưa tạo dự án hoặc phân công công việc</strong>
                      <button
                        class="launch-primary-action"
                        type="button"
                        :disabled="isChatting || !canManageProjectLaunch(msg) || msg.projectLaunchBrief.rulebookStatus !== 'effective' || msg.projectLaunchBrief.questions.some(question => question.blocking)"
                        data-testid="project-launch-plan-start"
                        @click="createLaunchPlan(msg.projectLaunchBrief)"
                      >Lập phương án nhân sự và công việc</button>
                      <small v-if="msg.projectLaunchBrief.rulebookStatus !== 'effective'">Cần áp dụng quy tắc làm việc trước khi chọn người và lịch.</small>
                      <small v-else-if="!canManageProjectLaunch(msg)">Bạn có thể xem và hoàn thiện brief; chỉ Owner/Manager tổ chức mới được lập đội hình và tạo dự án.</small>
                      <small v-else-if="msg.projectLaunchBrief.questions.some(question => question.blocking)">Cần gửi đủ câu trả lời ảnh hưởng trực tiếp đến phương án.</small>
                      <details class="launch-technical-details"><summary>Thông tin kỹ thuật</summary><span>{{ msg.projectLaunchBrief.actualProvider }} / {{ msg.projectLaunchBrief.actualModel }}</span><span>{{ msg.projectLaunchBrief.promptVersion }}</span></details>
                    </footer>
                  </article>

                  <article
                    v-if="msg.projectLaunchPlan"
                    class="project-launch-plan-card"
                    data-testid="project-launch-plan"
                  >
                    <header class="project-launch-plan-header">
                      <div>
                        <span>Phương án khởi chạy dự án</span>
                        <h3>{{ msg.projectLaunchPlan.deliveryPlan.proposedProjectName }}</h3>
                        <p>{{ msg.projectLaunchPlan.deliveryPlan.objective }}</p>
                      </div>
                      <div class="project-launch-status">
                        <strong>{{ launchPlanStateLabel(msg.projectLaunchPlan.state) }}</strong>
                        <small>Bản {{ msg.projectLaunchPlan.rowRevision }}</small>
                        <small>Quy tắc làm việc bản {{ msg.projectLaunchPlan.ruleSetVersion || 'chưa có' }}</small>
                      </div>
                    </header>
                    <nav class="launch-stepper launch-stepper--plan" aria-label="Tiến trình khởi tạo dự án">
                      <span class="active">1. Mục tiêu</span><span class="active">2. Phạm vi</span><span class="active">3. Nhân sự</span><span class="active">4. Sprint</span><span :class="{ active: Boolean(msg.projectLaunchPlan.executionReceipt) }">5. Xác nhận</span>
                    </nav>

                    <section
                      v-if="!msg.projectLaunchPlan.executionReceipt"
                      class="launch-final-review-summary"
                      data-testid="project-launch-final-review"
                    >
                      <header><strong>Tóm tắt trước khi xác nhận</strong><small>Chưa ghi dữ liệu</small></header>
                      <dl>
                        <div><dt>Project</dt><dd>{{ msg.projectLaunchPlan.deliveryPlan.proposedProjectName }}</dd></div>
                        <div><dt>Manager / team</dt><dd>{{ launchReviewSummary(msg.projectLaunchPlan).manager }} · {{ launchReviewSummary(msg.projectLaunchPlan).memberCount }} người</dd></div>
                        <div><dt>Sprint</dt><dd>{{ launchReviewSummary(msg.projectLaunchPlan).sprintCount }}</dd></div>
                        <div><dt>Task</dt><dd>{{ launchReviewSummary(msg.projectLaunchPlan).taskCount }}</dd></div>
                      </dl>
                      <p>Mọi chỉnh sửa phải được lưu và kiểm tra lại trước khi nút xác nhận được mở.</p>
                    </section>

                    <div v-if="msg.projectLaunchPlan.blockingReasons.length" class="launch-blocking-list">
                      <strong>Chưa thể xác nhận</strong>
                      <ul><li v-for="item in msg.projectLaunchPlan.blockingReasons" :key="item">{{ item }}</li></ul>
                    </div>
                    <div v-if="msg.projectLaunchPlan.warnings.length" class="launch-warning-list">
                      <strong>Cảnh báo</strong>
                      <ul><li v-for="item in msg.projectLaunchPlan.warnings" :key="item">{{ item }}</li></ul>
                    </div>

                    <section class="launch-plan-section">
                      <h4>1. Chọn phương án nhân sự</h4>
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
                              @change="selectLaunchScenario(msg.projectLaunchPlan, scenario.scenarioId)"
                            >
                            <span><strong>{{ scenario.title }}</strong><small>Điểm phù hợp {{ scenario.score.toFixed(1) }} · {{ scenario.feasible ? 'có thể áp dụng' : 'cần điều chỉnh' }}</small></span>
                          </label>
                          <p>{{ scenario.description }}</p>
                          <p><strong>Người quản lý:</strong> {{ scenario.managerName || 'chưa đủ điều kiện' }}</p>
                          <ul class="launch-member-list">
                            <li v-for="member in scenario.members" :key="member.userId">
                              <strong>{{ member.displayName }}</strong>
                              <span>{{ member.proposedRole }} · {{ member.proposedHours }} giờ · mức sử dụng {{ member.loadAfterPercent.toFixed(0) }}%</span>
                              <small>Kỹ năng đã có bằng chứng: {{ member.coveredSkills.join(', ') || 'chưa có' }}</small>
                            </li>
                          </ul>
                          <details v-if="scenario.blockingReasons.length || scenario.managerCandidates.some(item => item.hardRejects.length)">
                            <summary>Vì sao chưa thể chọn một số người</summary>
                            <ul>
                              <li v-for="reason in scenario.blockingReasons" :key="reason">{{ reason }}</li>
                              <li v-for="candidate in scenario.managerCandidates.filter(item => item.hardRejects.length)" :key="candidate.userId">
                                {{ candidate.displayName }}: {{ candidate.hardRejects.map(staffingRejectLabel).join(', ') }}
                              </li>
                            </ul>
                          </details>
                        </article>
                      </div>
                      <details v-if="!msg.projectLaunchPlan.executionReceipt" class="launch-plan-customizer">
                        <summary>Tùy chỉnh đội hình</summary>
                        <p>Thêm, thay người hoặc chỉnh số giờ. Qaly sẽ kiểm tra lại kỹ năng, lịch, tải đa dự án và quy tắc làm việc trước khi tạo dự án.</p>
                        <div class="launch-staffing-editor">
                          <article
                            v-for="candidate in selectedLaunchCandidates(msg.projectLaunchPlan)"
                            :key="candidate.userId"
                            :class="{ rejected: !candidate.staffingEligible || candidate.hardRejects.length }"
                          >
                            <label><input type="checkbox" :checked="launchPlanDraft(msg.projectLaunchPlan).staffing.find(item => item.userId === candidate.userId)?.included" :disabled="!candidate.staffingEligible" @change="updateLaunchStaffing(msg.projectLaunchPlan, candidate.userId, 'included', $event)"> {{ candidate.displayName }}</label>
                            <small>{{ candidate.organizationRole }} · còn {{ candidate.availableHours.toFixed(0) }}h · {{ candidate.activeProjectCount }} dự án</small>
                            <label>Quản lý <input type="radio" :name="`custom-manager-${msg.projectLaunchPlan.planId}`" :checked="launchPlanDraft(msg.projectLaunchPlan).staffing.find(item => item.userId === candidate.userId)?.manager" :disabled="!candidate.managerEligible || !candidate.staffingEligible" @change="updateLaunchStaffing(msg.projectLaunchPlan, candidate.userId, 'manager', $event)"></label>
                            <label>Vai trò<select :value="launchPlanDraft(msg.projectLaunchPlan).staffing.find(item => item.userId === candidate.userId)?.proposedRole || 'Member'" :disabled="!candidate.staffingEligible" @change="updateLaunchStaffing(msg.projectLaunchPlan, candidate.userId, 'proposedRole', $event)"><option value="Member">Thành viên</option><option value="Developer">Phát triển</option><option value="Tester">Kiểm thử</option><option value="Reviewer">Review</option></select></label>
                            <label>Số giờ<input type="number" min="1" :max="candidate.availableHours" :value="launchPlanDraft(msg.projectLaunchPlan).staffing.find(item => item.userId === candidate.userId)?.proposedHours || 0" :disabled="!candidate.staffingEligible" @input="updateLaunchStaffing(msg.projectLaunchPlan, candidate.userId, 'proposedHours', $event)"></label>
                            <small v-if="candidate.hardRejects.length">Chưa thể chọn: {{ candidate.hardRejects.map(staffingRejectLabel).join(', ') }}</small>
                          </article>
                        </div>
                      </details>
                    </section>

                    <section class="launch-plan-section">
                      <h4>2. Sprint và công việc sẽ được tạo</h4>
                      <p class="launch-plan-range">
                        {{ new Date(msg.projectLaunchPlan.deliveryPlan.startDate).toLocaleDateString('vi-VN') }}
                        → {{ new Date(msg.projectLaunchPlan.deliveryPlan.endDate).toLocaleDateString('vi-VN') }}
                      </p>
                      <div class="launch-autonomy-controls">
                        <label>Cách phân công
                          <select :value="launchPlanDraft(msg.projectLaunchPlan).assignmentMode" @change="updateLaunchPlanMode(msg.projectLaunchPlan, 'assignmentMode', $event)">
                            <option value="auto_balance">Qaly tự cân bằng các việc chưa giao</option>
                            <option value="preserve_assignments">Giữ đúng lựa chọn · cho phép backlog chưa giao</option>
                          </select>
                        </label>
                        <label>Cấu trúc lịch
                          <select :value="launchPlanDraft(msg.projectLaunchPlan).scheduleMode" @change="updateLaunchPlanMode(msg.projectLaunchPlan, 'scheduleMode', $event)">
                            <option value="sequential_sprints">Sprint tuần tự</option>
                            <option value="parallel_workstreams">Workstream song song</option>
                          </select>
                        </label>
                        <small>Qaly chỉ tự thay người khi bạn chọn chế độ tự cân bằng. Các ràng buộc quyền, kỹ năng thật, capacity và dependency vẫn luôn được kiểm tra.</small>
                      </div>
                      <fieldset class="launch-editor-fieldset" :disabled="Boolean(msg.projectLaunchPlan.executionReceipt)">
                      <button type="button" class="secondary-button launch-add-sprint" @click="addLaunchSprint(msg.projectLaunchPlan)">+ Thêm Sprint</button>
                      <details v-for="sprint in launchPlanDraft(msg.projectLaunchPlan).sprints" :key="sprint.clientId" class="launch-sprint">
                        <summary><input type="checkbox" :checked="sprint.selected" @click.stop @change="updateLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 'selected', $event)"> {{ sprint.name }} · {{ sprint.tasks.filter(item => item.selected).length }} task</summary>
                        <div class="launch-card-actions"><button type="button" @click="moveLaunchSprint(msg.projectLaunchPlan, sprint.clientId, -1)">Lên</button><button type="button" @click="moveLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 1)">Xuống</button><button type="button" @click="addLaunchTask(msg.projectLaunchPlan, sprint.clientId)">+ Thêm công việc</button></div>
                        <div class="launch-sprint-editor">
                          <label>Tên Sprint<input :value="sprint.name" @input="updateLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 'name', $event)"></label>
                          <label>Mục tiêu Sprint<input :value="sprint.objective" @input="updateLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 'objective', $event)"></label>
                          <label>Bắt đầu<input type="date" :value="sprint.startDate.slice(0, 10)" @input="updateLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 'startDate', $event)"></label>
                          <label>Kết thúc<input type="date" :value="sprint.endDate.slice(0, 10)" @input="updateLaunchSprint(msg.projectLaunchPlan, sprint.clientId, 'endDate', $event)"></label>
                        </div>
                        <ol class="launch-task-editor">
                          <li v-for="task in sprint.tasks" :key="task.clientId">
                            <input type="checkbox" :checked="task.selected" @change="updateLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'selected', $event)">
                            <input class="launch-task-title" :value="task.title" @input="updateLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'title', $event)">
                            <label>Giờ<input type="number" min="1" max="1000" :value="task.estimatedHours" @input="updateLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'estimatedHours', $event)"></label>
                            <label>Người thực hiện<select :value="task.proposedAssigneeId || ''" @change="updateLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'proposedAssigneeId', $event)"><option value="">{{ launchPlanDraft(msg.projectLaunchPlan).assignmentMode === 'auto_balance' ? 'Để Qaly cân bằng' : 'Chưa giao · đưa vào backlog' }}</option><option v-for="member in launchPlanDraft(msg.projectLaunchPlan).staffing.filter(item => item.included)" :key="member.userId" :value="member.userId">{{ (msg.projectLaunchPlan.staffingScenarios[0]?.managerCandidates || []).find(item => item.userId === member.userId)?.displayName || member.userId }}</option></select></label>
                            <label>Người review<select :value="task.proposedReviewerId || ''" @change="updateLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'proposedReviewerId', $event)"><option value="">{{ launchPlanDraft(msg.projectLaunchPlan).assignmentMode === 'auto_balance' ? 'Qaly chọn người phù hợp' : 'Chưa chọn reviewer' }}</option><option v-for="member in launchPlanDraft(msg.projectLaunchPlan).staffing.filter(item => item.included && item.userId !== task.proposedAssigneeId)" :key="member.userId" :value="member.userId">{{ (msg.projectLaunchPlan.staffingScenarios[0]?.managerCandidates || []).find(item => item.userId === member.userId)?.displayName || member.userId }}</option></select></label>
                            <small>Kỹ năng: {{ task.requiredSkillNames.join(', ') || 'Chưa xác định' }}<template v-if="task.featureId"> · thuộc chức năng {{ launchFeatureTitle(msg.projectLaunchPlan, task.featureId) }}</template></small>
                            <small v-if="task.dependencyClientIds.length">Phụ thuộc: {{ task.dependencyClientIds.join(', ') }}</small>
                            <details class="launch-task-detail-editor">
                              <summary>Mô tả, nghiệm thu, kỹ năng và liên kết</summary>
                              <label>Mô tả<textarea rows="2" :value="task.description" @input="updateLaunchTaskDetail(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'description', $event)" /></label>
                              <label>Tiêu chí nghiệm thu — mỗi dòng một ý<textarea rows="3" :value="task.acceptanceCriteria.join('\n')" @input="updateLaunchTaskDetail(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'acceptanceCriteria', $event)" /></label>
                              <label>Hoàn thành khi — mỗi dòng một ý<textarea rows="3" :value="task.definitionOfDone.join('\n')" @input="updateLaunchTaskDetail(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'definitionOfDone', $event)" /></label>
                              <label>Thuộc chức năng<select :value="task.featureId || ''" @change="updateLaunchTaskDetail(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'featureId', $event)"><option v-for="feature in (msg.projectLaunchPlan.deliveryPlan.features || []).filter(item => item.selected && item.priority !== 'out_of_scope')" :key="feature.featureId" :value="feature.featureId">{{ feature.title }}</option></select></label>
                              <label>Kỹ năng cần thiết — mỗi dòng một kỹ năng<textarea rows="3" :value="task.requiredSkillNames.join('\n')" @input="updateLaunchTaskDetail(msg.projectLaunchPlan, sprint.clientId, task.clientId, 'requiredSkillNames', $event)" /></label>
                              <div v-if="msg.projectLaunchPlan.deliveryPlan.objectiveMetrics?.length" class="launch-task-link-options"><strong>Thước đo mà công việc này đóng góp</strong><label v-for="metric in msg.projectLaunchPlan.deliveryPlan.objectiveMetrics" :key="metric.metricId"><input type="checkbox" :checked="task.objectiveMetricIds?.includes(metric.metricId)" @change="toggleLaunchTaskMetric(msg.projectLaunchPlan, sprint.clientId, task.clientId, metric.metricId)"> {{ metric.title }}</label></div>
                              <div class="launch-task-link-options"><strong>Công việc phải hoàn thành trước</strong><label v-for="dependency in launchPlanDraft(msg.projectLaunchPlan).sprints.flatMap(item => item.tasks).filter(item => item.clientId !== task.clientId && item.selected)" :key="dependency.clientId"><input type="checkbox" :checked="task.dependencyClientIds.includes(dependency.clientId)" @change="toggleLaunchTaskDependency(msg.projectLaunchPlan, sprint.clientId, task.clientId, dependency.clientId)"> {{ dependency.title }}</label></div>
                            </details>
                            <span class="launch-task-order"><button type="button" @click="moveLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, -1)">Lên</button><button type="button" @click="moveLaunchTask(msg.projectLaunchPlan, sprint.clientId, task.clientId, 1)">Xuống</button></span>
                          </li>
                        </ol>
                      </details>
                      <button v-if="launchPlanDraft(msg.projectLaunchPlan).dirty" type="button" class="launch-primary-action" :disabled="Boolean(launchActionBusy)" @click="saveLaunchPlanReview(msg, msg.projectLaunchPlan)">{{ launchActionBusy === `review:${msg.projectLaunchPlan.planId}` ? 'Đang kiểm tra lại…' : 'Lưu thay đổi và kiểm tra lại' }}</button>
                      </fieldset>
                      <details v-if="msg.projectLaunchPlan.deliveryPlan.externalDeferred.length" class="launch-external-deferred">
                        <summary>Kết nối bên ngoài chưa được thực hiện</summary>
                        <ul><li v-for="item in msg.projectLaunchPlan.deliveryPlan.externalDeferred" :key="item">{{ item }}</li></ul>
                      </details>
                    </section>

                    <section v-if="msg.projectLaunchPlan.executionReceipt" class="launch-receipt" data-testid="project-launch-receipt">
                      <h4>Dự án đã được tạo và đọc lại từ dữ liệu thật</h4>
                      <p>Qaly chỉ báo thành công sau khi Project, thành viên, Sprint và Task đã được lưu và kiểm tra lại.</p>
                      <div class="launch-receipt-links"><button v-for="link in msg.projectLaunchPlan.executionReceipt.createdEntityLinks" :key="link" type="button" @click="openLaunchLink(link)">{{ link.includes('tasks') ? 'Mở công việc' : link.includes('roadmap') ? 'Mở lộ trình' : 'Mở dự án' }}</button></div>
                      <details><summary>Chi tiết biên nhận và kết nối chưa thực hiện</summary><p>Giao dịch: <strong>{{ msg.projectLaunchPlan.executionReceipt.internalTransactionCommitted ? 'đã lưu trọn vẹn' : 'chưa lưu' }}</strong> · Đọc lại: <strong>{{ msg.projectLaunchPlan.executionReceipt.readBackVerified ? 'đã xác minh' : 'không đạt' }}</strong></p><ul><li v-for="command in msg.projectLaunchPlan.executionReceipt.commands" :key="command.commandId"><span><strong>{{ command.summary }}</strong></span><button v-if="command.deepLink" type="button" @click="openLaunchLink(command.deepLink)">Mở</button></li></ul></details>
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
                      <details class="launch-technical-details"><summary>Thông tin kỹ thuật</summary><span>{{ msg.projectLaunchPlan.actualProvider }} / {{ msg.projectLaunchPlan.actualModel }}</span><small>{{ msg.projectLaunchPlan.scoringVersion }}</small></details>
                      <button
                        v-if="!msg.projectLaunchPlan.executionReceipt"
                        type="button"
                        class="launch-primary-action"
                        :disabled="Boolean(launchActionBusy) || !canExecuteProjectLaunch(msg) || launchPlanDraft(msg.projectLaunchPlan).dirty || msg.projectLaunchPlan.state === 'executing' || Boolean(msg.projectLaunchPlan.blockingReasons.length)"
                        data-testid="project-launch-confirm"
                        @click="confirmLaunchPlan(msg, msg.projectLaunchPlan)"
                      >{{ launchActionBusy === msg.projectLaunchPlan.planId || msg.projectLaunchPlan.state === 'executing' ? 'Đang hoàn tất Project…' : 'Xác nhận tạo Project' }}</button>
                      <small v-if="!msg.projectLaunchPlan.executionReceipt && launchPlanDraft(msg.projectLaunchPlan).dirty">Lưu và kiểm tra lại thay đổi trước khi tạo Project.</small>
                      <template v-if="msg.projectLaunchPlan.executionReceipt">
                        <button type="button" :disabled="Boolean(launchActionBusy)" data-testid="project-launch-monitor" @click="monitorLaunch(msg, msg.projectLaunchPlan)">Kiểm tra tình trạng</button>
                        <button
                          v-if="msg.projectLaunchPlan.executionReceipt?.rollbackAvailable"
                          type="button"
                          class="launch-danger-action"
                          :disabled="Boolean(launchActionBusy)"
                          data-testid="project-launch-rollback"
                          @click="rollbackLaunch(msg, msg.projectLaunchPlan)"
                        >Hoàn tác lần tạo này</button>
                        <button type="button" @click="openLaunchLink(`/projects/${msg.projectLaunchPlan.executionReceipt?.projectId || ''}`)">Mở dự án</button>
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
                              <th v-if="table.rowAction" class="align-right">Mở</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr v-if="!table.rows.length">
                              <td :colspan="table.columns.length + (table.rowAction ? 1 : 0)" class="empty-cell">Không có dữ liệu phù hợp</td>
                            </tr>
                            <tr v-for="(row, rowIndex) in table.rows" :key="rowIndex">
                              <td
                                v-for="column in table.columns"
                                :key="column.key"
                                :class="`align-${column.align || 'left'}`"
                              >
                                {{ tableCell(row, column.key) }}
                              </td>
                              <td v-if="table.rowAction" class="align-right">
                                <button
                                  type="button"
                                  class="erumi-table-row-action"
                                  :disabled="!row[table.rowAction.routeKey || 'route']"
                                  @click="openTableRowAction(table, row)"
                                >
                                  {{ table.rowAction.label }}
                                </button>
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
                      <div v-else-if="action.type === 'select_project_for_task_plan'" class="erumi-draft-card clarification-card">
                        <strong>{{ action.label }}</strong>
                        <p class="erumi-draft-text">Chọn Project thật để Qaly mở Task Composer đúng ngữ cảnh. Bước này chưa tạo task.</p>
                        <div v-if="activeProjects.length" class="clarification-choices">
                          <button
                            v-for="project in activeProjects"
                            :key="project.id"
                            type="button"
                            class="clarification-choice"
                            @click="selectProjectForTaskPlan(project.id, action)"
                          >
                            <span>{{ project.name }}</span>
                            <small>Mở phương án task trong Project này</small>
                          </button>
                        </div>
                        <button v-else type="button" class="erumi-action-button" @click="openGuidanceRoute('/projects')">
                          Mở danh sách Project
                        </button>
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
                              <button
                                v-if="question.allowFreeText"
                                type="button"
                                @click="answerProgressiveWithComposer(question, action)"
                              >Khác…</button>
                            </div>
                            <div v-if="question.allowFreeText" class="assistant-free-answer">
                              <textarea
                                v-if="question.inputType === 'textarea'"
                                :id="`clarification-${question.id}`"
                                rows="3"
                                :value="progressiveAnswerDisplay(question, action)"
                                :aria-label="`Trả lời: ${question.text}`"
                                :placeholder="question.placeholder || 'Nhập câu trả lời…'"
                                @focus="answerProgressiveWithComposer(question, action)"
                                @input="updateProgressiveFreeText(question, action, $event, false)"
                                @change="updateProgressiveFreeText(question, action, $event, true)"
                              />
                              <input
                                v-else
                                :id="`clarification-${question.id}`"
                                type="text"
                                :value="progressiveAnswerDisplay(question, action)"
                                :aria-label="`Trả lời: ${question.text}`"
                                :placeholder="question.placeholder || 'Nhập câu trả lời…'"
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

                <footer v-if="hasAssistantContent(msg)" class="assistant-meta">
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
                      v-if="messageMenuItems(msg).length"
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
        <section v-if="activeDrawerTab === 'tools'" class="analytics-drawer-section assistant-tools-hub">
          <div class="assistant-tools-scope">
            <span>Phạm vi hiện tại</span>
            <strong>{{ selectedTargetLabel }}</strong>
          </div>
          <div>
            <h3>Bắt đầu nhanh</h3>
            <p class="analytics-drawer-muted">Chọn một câu để điền vào ô chat; Qaly chưa tự gửi hoặc thay đổi dữ liệu.</p>
          </div>
          <div class="assistant-tools-grid">
            <button
              v-for="shortcut in newcomerShortcuts"
              :key="shortcut.label"
              type="button"
              @click="choosePrompt(shortcut.prompt)"
            >
              <strong>{{ shortcut.label }}</strong>
              <small>{{ shortcut.description }}</small>
            </button>
          </div>
          <div>
            <h3>Phân tích & kết quả</h3>
            <p class="analytics-drawer-muted">Công cụ mở dữ liệu gần nhất hoặc chuẩn bị một câu hỏi có thể chỉnh sửa.</p>
          </div>
          <div class="assistant-tools-grid">
            <button
              v-for="tool in analysisTools"
              :key="tool.key"
              type="button"
              @click="useAnalysisTool(tool)"
            >
              <span class="assistant-tool-title">
                <strong>{{ tool.label }}</strong>
                <em>{{ tool.behavior === 'open-drawer' ? 'Mở kết quả' : tool.isWriteLike ? 'Bản nháp' : 'Điền câu hỏi' }}</em>
              </span>
              <small>{{ tool.description }}</small>
            </button>
          </div>
        </section>

        <template v-else-if="activeDrawerTab === 'sources'">
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
          <p class="analytics-drawer-muted">Dữ liệu được lấy từ phản hồi Erumi đang chọn, không gọi backend mới.</p>
          <div v-if="drawerMetrics.length" class="analytics-drawer-metric-list">
            <article v-for="metric in drawerMetrics" :key="metric.label" class="analytics-drawer-metric" :class="`tone-${metric.tone || 'neutral'}`">
              <span>{{ metric.label }}</span>
              <strong>{{ metric.value }}</strong>
              <small v-if="metric.hint">{{ metric.hint }}</small>
            </article>
          </div>
          <div v-if="drawerTables.length || drawerCharts.length" class="analytics-drawer-table-note">
            <strong>{{ drawerTables.length }} bảng · {{ drawerCharts.length }} biểu đồ</strong>
            <span>Chi tiết vẫn nằm trong phản hồi gốc để giữ bảng công cụ này gọn.</span>
          </div>
          <div v-if="!drawerMetrics.length && !drawerTables.length && !drawerCharts.length" class="analytics-drawer-empty">
            <strong>Chưa có dữ liệu có cấu trúc</strong>
            <span>Hãy hỏi Erumi về tiến độ, tải công việc hoặc rủi ro.</span>
            <button type="button" class="analytics-empty-action" @click="choosePrompt('Tóm tắt các số liệu quan trọng nhất trong phạm vi hiện tại và giải thích ngắn gọn.')">Hỏi về số liệu</button>
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
          <p class="analytics-drawer-muted">Mỗi action giữ đúng hành vi nghiệp vụ: điều hướng sẽ mở đúng trang, bản nháp sẽ mở composer và mutation vẫn cần xác nhận rõ ràng.</p>
          <div v-if="drawerActions.length" class="analytics-action-list">
            <button
              v-for="action in drawerActions"
              :key="`${action.type}-${action.label}`"
              type="button"
              class="analytics-action-draft"
              @click="handleDrawerAction(action)"
            >
              <strong>{{ action.label }}</strong>
              <small>{{ drawerActionHint(action) }}</small>
            </button>
          </div>
          <div v-else class="analytics-drawer-empty">
            <strong>Chưa có action</strong>
            <span>Mở Công cụ AI để chọn một câu hỏi hoặc chuẩn bị đề xuất tiếp theo.</span>
            <button type="button" class="analytics-empty-action" @click="openCockpitDrawer('tools')">Mở Công cụ AI</button>
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
  margin: 0;
  font-size: 26px;
  font-weight: 700;
  color: var(--text-strong);
  letter-spacing: -0.4px;
  line-height: 1.25;
  text-align: center;
}

.empty-intro {
  width: 100%;
  margin-bottom: auto;
  display: grid;
  justify-items: center;
  gap: 9px;
  text-align: center;
}

.empty-intro > p,
.empty-intro > small { margin: 0; color: var(--muted); }
.empty-intro > p { font-size: 13px; }
.empty-intro > small { font-size: 11px; }
.newcomer-shortcuts { display: flex; justify-content: center; flex-wrap: wrap; gap: 7px; }
.newcomer-shortcuts button {
  border: 1px solid var(--line);
  border-radius: 999px;
  background: var(--panel);
  color: var(--text);
  padding: 7px 11px;
  font-size: 12px;
  font-weight: 650;
  cursor: pointer;
}
.newcomer-shortcuts button:hover,
.newcomer-shortcuts button:focus-visible { border-color: var(--primary); color: var(--primary-strong); outline: none; }

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

.erumi-table-row-action {
  border: 1px solid var(--primary-border, #bfdbfe);
  border-radius: 8px;
  background: var(--primary-soft, #eff6ff);
  color: var(--primary, #1d4ed8);
  cursor: pointer;
  font: inherit;
  font-weight: 650;
  padding: 6px 9px;
  white-space: nowrap;
}

.erumi-table-row-action:hover:not(:disabled) {
  border-color: var(--primary, #2563eb);
}

.erumi-table-row-action:disabled {
  cursor: not-allowed;
  opacity: 0.45;
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
.empty-header-actions { position: absolute; z-index: 6; top: 14px; right: 18px; display: flex; align-items: center; gap: 6px; }

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

.analytics-action-draft strong,
.analytics-action-draft small { display: block; }
.analytics-action-draft small { margin-top: 4px; color: var(--muted); font-weight: 400; }

.analytics-action-draft:hover,
.analytics-action-draft:focus-visible {
  border-color: var(--primary);
  outline: none;
}

.assistant-tools-hub h3 { margin: 0 0 4px; color: var(--text-strong); font-size: 13px; }
.assistant-tools-scope {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid var(--line);
  border-radius: 9px;
  background: var(--panel-soft);
  padding: 9px 10px;
}
.assistant-tools-scope span { color: var(--muted); font-size: 11px; }
.assistant-tools-scope strong { min-width: 0; overflow: hidden; color: var(--text-strong); font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.assistant-tools-grid { display: grid; gap: 7px; }
.assistant-tools-grid > button {
  display: grid;
  gap: 4px;
  width: 100%;
  border: 1px solid var(--line);
  border-radius: 9px;
  background: var(--panel);
  color: var(--text);
  padding: 10px;
  text-align: left;
  cursor: pointer;
}
.assistant-tools-grid > button:hover,
.assistant-tools-grid > button:focus-visible { border-color: var(--primary); background: var(--primary-soft); outline: none; }
.assistant-tools-grid strong { font-size: 12px; }
.assistant-tools-grid small { color: var(--muted); font-size: 11px; line-height: 1.35; }
.assistant-tool-title { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.assistant-tool-title em { border-radius: 999px; background: var(--panel-soft); color: var(--muted); padding: 2px 6px; font-size: 9px; font-style: normal; }
.analytics-empty-action {
  justify-self: start;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--panel);
  color: var(--primary-strong);
  padding: 7px 9px;
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
}
.analytics-empty-action:hover,
.analytics-empty-action:focus-visible { border-color: var(--primary); background: var(--primary-soft); outline: none; }

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
.project-launch-rulebook.status-policy_missing { color: #b45309; background: #fffbeb; }
.rulebook-review-editor, .rulebook-draft-review { flex: 1 0 100%; color: var(--text-primary); background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 12px; }
.rulebook-review-editor > p { margin: 0 0 10px; color: var(--text-secondary); }
.rulebook-review-rule { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 9px 0; border-top: 1px solid var(--border); }
.rulebook-review-rule > span { display: grid; gap: 2px; }
.rulebook-review-rule small { color: var(--text-secondary); font-weight: 500; }
.rulebook-review-value { grid-template-columns: minmax(68px, 90px) auto !important; align-items: center; }
.rulebook-review-value input { width: 100%; border: 1px solid var(--border); border-radius: 7px; padding: 6px 8px; }
.rulebook-review-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
.rulebook-review-actions button, .rulebook-draft-review button { margin-left: 0; }
.rulebook-draft-review summary { cursor: pointer; font-weight: 700; }
.rulebook-draft-review ul { display: grid; gap: 6px; margin: 10px 0; padding-left: 18px; }
.rulebook-draft-review li { display: flex; justify-content: space-between; gap: 12px; }
.project-launch-review-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 11px; padding: 14px 16px; border-top: 1px solid var(--border); background: color-mix(in srgb, var(--surface) 96%, var(--primary) 4%); }
.launch-autonomy-controls { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; margin: 10px 0 12px; padding: 11px; border: 1px solid var(--border); border-radius: 10px; background: color-mix(in srgb, var(--surface) 94%, var(--primary) 6%); }
.launch-autonomy-controls label { display: grid; gap: 5px; color: var(--text-secondary); font-size: 12px; font-weight: 700; }
.launch-autonomy-controls select { width: 100%; border: 1px solid var(--border); border-radius: 8px; padding: 8px 9px; color: var(--text-primary); background: var(--surface); }
.launch-autonomy-controls small { grid-column: 1 / -1; color: var(--text-secondary); line-height: 1.45; }
.project-launch-form-field { min-width: 0; display: grid; gap: 6px; }
.project-launch-form-field--wide { grid-column: 1 / -1; }
.project-launch-form-field label { color: var(--text); font-size: 12px; font-weight: 750; }
.project-launch-form-field input,
.project-launch-form-field textarea { width: 100%; box-sizing: border-box; border: 1px solid var(--border); border-radius: 9px; padding: 9px 10px; color: var(--text); background: var(--surface); font: inherit; }
.project-launch-form-field textarea { resize: vertical; line-height: 1.45; }
.project-launch-form-field input:focus,
.project-launch-form-field textarea:focus { outline: 2px solid color-mix(in srgb, var(--primary) 28%, transparent); border-color: var(--primary); }
.project-launch-form-more { border: 1px solid var(--border); border-radius: 10px; padding: 9px 10px; background: var(--surface); }
.project-launch-form-more > summary { cursor: pointer; color: var(--muted); font-size: 12px; font-weight: 750; }
.project-launch-form-more-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; padding-top: 10px; }
.project-launch-form-actions { display: flex; align-items: center; justify-content: flex-end; gap: 12px; }
.project-launch-form-actions small { margin-right: auto; color: var(--muted); }
.launch-stepper { grid-column: 1 / -1; display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 6px; }
.launch-stepper span { padding: 7px 8px; border-radius: 999px; background: var(--surface); color: var(--muted); text-align: center; font-size: 11px; font-weight: 750; }
.launch-stepper span.active { background: color-mix(in srgb, var(--primary) 16%, var(--surface)); color: var(--primary); }
.launch-simple-guide { display: grid; grid-template-columns: auto minmax(0, 1fr); align-items: center; gap: 3px 10px; padding: 10px 12px; border: 1px solid color-mix(in srgb, var(--primary) 28%, var(--border)); border-radius: 10px; background: color-mix(in srgb, var(--surface) 92%, var(--primary) 8%); font-size: 12px; }
.launch-simple-guide strong { color: var(--primary); }
.launch-simple-guide small { grid-column: 1 / -1; color: var(--muted); }
.launch-field-hint { color: var(--muted); font-weight: 500; line-height: 1.4; }
.launch-builder-block { display: grid; gap: 10px; padding: 12px; border: 1px solid var(--border); border-radius: 12px; background: var(--surface); }
.launch-builder-block > header { display: flex; justify-content: space-between; gap: 10px; }
.launch-builder-block > header div { display: grid; gap: 3px; }
.launch-builder-block small, .launch-help { color: var(--muted); font-size: 11px; }
.launch-friendly-header > span { align-self: start; border-radius: 999px; padding: 4px 8px; background: #ecfdf5; color: #047857; font-size: 10px; font-weight: 750; }
.launch-optional-goal-details, .launch-metrics-review { border: 1px solid var(--border); border-radius: 10px; background: color-mix(in srgb, var(--surface) 97%, var(--primary) 3%); }
.launch-optional-goal-details > summary, .launch-metrics-review > summary { cursor: pointer; padding: 10px 11px; color: var(--text); font-size: 12px; font-weight: 750; }
.launch-optional-goal-details > summary span, .launch-metric-advanced > summary span { margin-left: 6px; color: var(--muted); font-size: 10px; font-weight: 600; }
.launch-optional-goal-details .project-launch-form-more-grid { padding: 0 11px 11px; }
.launch-metrics-review > summary { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.launch-metrics-review > summary > span { display: grid; gap: 2px; }
.launch-metrics-review > summary small { color: var(--muted); font-weight: 500; }
.launch-metrics-review > summary em { color: var(--primary); font-size: 10px; font-style: normal; white-space: nowrap; }
.launch-metrics-review > .launch-template-row, .launch-metrics-review > .launch-metric-grid { margin: 0 11px 11px; }
.launch-template-row, .launch-choice-row, .launch-skill-options { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; }
.launch-template-row > span { color: var(--muted); font-size: 11px; }
.launch-template-row button, .launch-choice-row button, .launch-skill-options button { border: 1px solid var(--border); border-radius: 999px; padding: 6px 9px; background: var(--surface); color: var(--text); cursor: pointer; font-size: 11px; }
.launch-template-row button.selected, .launch-choice-row button.selected, .launch-skill-options button.selected { border-color: var(--primary); background: color-mix(in srgb, var(--primary) 13%, var(--surface)); color: var(--primary); }
.launch-template-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); }
.launch-template-grid button { border-radius: 9px; text-align: left; }
.launch-metric-grid, .launch-feature-list { display: grid; gap: 8px; }
.launch-metric-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.launch-metric-card, .launch-feature-card { display: grid; gap: 7px; padding: 10px; border: 1px solid var(--border); border-radius: 10px; background: color-mix(in srgb, var(--surface) 96%, var(--primary) 4%); }
.launch-metric-card > div, .launch-feature-card > div { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; }
.launch-metric-card label { display: grid; gap: 3px; color: var(--muted); font-size: 10px; }
.launch-metric-card input, .launch-metric-card select, .launch-feature-card input, .launch-feature-card select, .launch-custom-add input { min-width: 0; border: 1px solid var(--border); border-radius: 8px; padding: 7px 8px; background: var(--surface); color: var(--text); }
.launch-metric-simple-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; }
.launch-metric-summary { margin: 0; padding: 7px 8px; border-radius: 8px; background: color-mix(in srgb, var(--surface) 87%, #10b981 13%); color: #047857; font-size: 11px; font-weight: 650; }
.launch-metric-advanced { border-top: 1px solid var(--border); padding-top: 6px; }
.launch-metric-advanced > summary { cursor: pointer; color: var(--primary); font-size: 10px; font-weight: 700; }
.launch-metric-advanced[open] { display: grid; gap: 7px; }
.launch-metric-advanced > div { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; }
.launch-metric-advanced > label { display: grid; gap: 3px; color: var(--muted); font-size: 10px; }
.launch-feature-detail { display: grid !important; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; padding-top: 7px; }
.launch-feature-detail label { display: grid; gap: 4px; color: var(--muted); font-size: 10px; }
.launch-feature-detail label:last-child { grid-column: 1 / -1; }
.launch-feature-detail textarea, .launch-custom-skill { width: 100%; box-sizing: border-box; border: 1px solid var(--border); border-radius: 8px; padding: 7px 8px; background: var(--surface); color: var(--text); font: inherit; }
.launch-link-danger { justify-self: end; border: 0; background: none; color: #b91c1c; cursor: pointer; }
.launch-card-actions { display: flex !important; justify-content: flex-end; gap: 6px; }
.launch-card-actions button { border: 0; border-radius: 7px; padding: 4px 7px; background: var(--surface); color: var(--muted); cursor: pointer; font-size: 10px; }
.launch-card-actions .launch-link-danger { color: #b91c1c; }
.launch-custom-add { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 7px; }
.launch-custom-add button { border: 1px solid var(--primary); border-radius: 8px; padding: 7px 11px; background: var(--primary); color: #fff; }
.project-launch-columns { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; padding: 14px 16px; }
.project-launch-columns section { padding: 10px; border: 1px solid var(--border); border-radius: 10px; }
.project-launch-columns h4,
.project-launch-columns p,
.project-launch-columns ul { margin: 0; }
.project-launch-columns ul { padding-left: 18px; }
.project-launch-summary-details { margin: 12px 16px; border: 1px solid var(--border); border-radius: 10px; }
.project-launch-summary-details > summary { cursor: pointer; padding: 10px 12px; font-weight: 750; }
.project-launch-summary-details .project-launch-columns { padding: 4px 12px 12px; }
.project-launch-summary-details .project-launch-decisions { margin: 0 12px 12px; }
.project-launch-decisions { margin: 0 16px 14px; }
.project-launch-decisions summary { cursor: pointer; font-weight: 700; }
.project-launch-decisions ul { display: grid; gap: 7px; list-style: none; padding: 8px 0 0; }
.project-launch-decisions li { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 3px 10px; padding: 9px; border: 1px solid var(--border); border-radius: 9px; }
.project-launch-decisions li p { grid-column: 1 / -1; margin: 0; color: var(--muted); }
.project-launch-decisions .decision-block { border-color: #fecaca; }
.project-launch-decisions .decision-unknown { border-color: #fde68a; }
.project-launch-brief-card > footer { display: flex; flex-wrap: wrap; gap: 8px 14px; padding: 10px 16px; border-top: 1px solid var(--border); color: var(--muted); font-size: 11px; }
.native-action-editor { display: grid; gap: 10px; padding: 14px 16px; border-top: 1px solid var(--border); }
.native-action-editor > label { display: grid; gap: 5px; font-size: 12px; font-weight: 700; }
.native-action-editor input:not([type="checkbox"]),
.native-action-editor textarea { width: 100%; box-sizing: border-box; border: 1px solid var(--border); border-radius: 9px; padding: 9px 10px; background: var(--surface); color: var(--text); font: inherit; font-weight: 400; }
.native-action-editor textarea { resize: vertical; }
.native-action-editor .native-action-check { display: flex; align-items: center; gap: 8px; }
.native-action-inline-fields { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.native-action-inline-fields label { display: grid; gap: 5px; }
.native-action-inline-fields select { border: 1px solid var(--border); border-radius: 9px; padding: 9px 10px; background: var(--surface); color: var(--text); }
.native-action-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.native-action-card > .launch-primary-action { display: flex; margin: 0 16px 14px auto; }
.native-action-card > .launch-receipt { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }
.native-action-card > .launch-receipt h4 { flex-basis: 100%; }
.native-action-controls { display: flex; justify-content: flex-end; gap: 8px; padding: 0 16px 14px; }
.native-action-controls .launch-primary-action { margin: 0; }
.native-action-controls .danger-button,
.native-action-controls .secondary-button { border-radius: 9px; padding: 9px 13px; font-weight: 700; }
.native-action-controls .secondary-button { border: 1px solid var(--border); background: var(--surface); color: var(--text); }
.native-action-controls .danger-button { border: 1px solid #fecaca; background: #fff1f2; color: #b91c1c; }
.assignment-proposal-item { padding: 14px 16px; border-top: 1px solid var(--border); display: grid; gap: 10px; }
.assignment-proposal-grid { display: grid; grid-template-columns: minmax(220px, 1.5fr) repeat(2, minmax(150px, .75fr)); gap: 10px; }
.assignment-proposal-grid label { display: grid; gap: 5px; color: var(--muted); font-size: 11px; font-weight: 700; }
.assignment-proposal-grid select,
.assignment-proposal-grid input { width: 100%; min-height: 38px; border: 1px solid var(--border); border-radius: 9px; padding: 7px 9px; background: var(--surface); color: var(--text); }
.assignment-facts { display: flex; flex-wrap: wrap; gap: 8px; }
.assignment-facts span { border-radius: 999px; padding: 5px 9px; background: var(--surface-soft); color: var(--muted); font-size: 11px; }
.assignment-proposal-card .native-action-controls { align-items: center; justify-content: space-between; padding-top: 12px; border-top: 1px solid var(--border); }
@media (max-width: 760px) { .assignment-proposal-grid { grid-template-columns: 1fr; } }
.native-action-closed { margin: 0 16px 14px; padding: 10px 12px; border-radius: 9px; background: var(--surface-muted); color: var(--text-muted); }
.safe-test-suite-list { display: grid; gap: 8px; margin: 0; padding: 14px 16px; list-style: none; }
.safe-test-suite-list li { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 9px 10px; border: 1px solid var(--border); border-radius: 9px; }
.safe-test-suite-list li div { display: grid; min-width: 0; }
.safe-test-suite-list small,
.safe-test-suite-list span { color: var(--muted); font-size: 11px; }
.safe-test-events { padding-top: 0; }
.safe-test-events .status-passed { border-color: #86efac; }
.safe-test-events .status-failed { border-color: #fca5a5; }
.safe-test-summary { margin: 0; padding: 0 16px 12px; }
.safe-test-confirm { margin: 0 16px 14px auto; display: flex; }
.project-launch-plan-card { border: 1px solid color-mix(in srgb, var(--primary) 40%, var(--border)); border-radius: 14px; background: var(--surface); overflow: hidden; }
.project-launch-plan-header { display: flex; justify-content: space-between; gap: 16px; padding: 14px 16px; background: color-mix(in srgb, var(--surface) 82%, var(--primary) 18%); }
.project-launch-plan-header h3,
.project-launch-plan-header p { margin: 3px 0 0; }
.project-launch-plan-header span { color: var(--muted); font-size: 11px; }
.launch-final-review-summary { display: grid; gap: 9px; padding: 12px 16px; border-top: 1px solid var(--border); background: color-mix(in srgb, var(--surface) 92%, var(--primary) 8%); }
.launch-final-review-summary > header { display: flex; justify-content: space-between; gap: 12px; }
.launch-final-review-summary > header small { color: #92400e; font-weight: 700; }
.launch-final-review-summary dl { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; margin: 0; }
.launch-final-review-summary dl > div { display: grid; gap: 3px; min-width: 0; padding: 8px; border: 1px solid var(--border); border-radius: 9px; background: var(--surface); }
.launch-final-review-summary dt { color: var(--muted); font-size: 10px; }
.launch-final-review-summary dd { margin: 0; overflow-wrap: anywhere; font-weight: 750; }
.launch-final-review-summary p { margin: 0; color: var(--muted); font-size: 11px; }
.launch-blocking-list,
.launch-warning-list { padding: 10px 16px; border-top: 1px solid var(--border); font-size: 12px; }
.launch-blocking-list { color: #b91c1c; background: #fef2f2; }
.launch-warning-list { color: #92400e; background: #fffbeb; }
.launch-blocking-list ul,
.launch-warning-list ul { margin: 5px 0 0; padding-left: 18px; }
.launch-plan-section,
.launch-receipt,
.launch-replan { padding: 14px 16px; border-top: 1px solid var(--border); }
.launch-plan-section > h4,
.launch-receipt > h4,
.launch-replan > h4 { margin: 0 0 10px; }
.launch-scenario-list { display: grid; gap: 10px; }
.launch-plan-customizer { margin-top: 10px; padding: 10px; border: 1px solid var(--border); border-radius: 10px; }
.launch-plan-customizer > summary { cursor: pointer; font-weight: 750; }
.launch-plan-customizer > p { color: var(--muted); font-size: 11px; }
.launch-staffing-editor { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.launch-staffing-editor article { display: grid; gap: 5px; padding: 9px; border: 1px solid var(--border); border-radius: 9px; }
.launch-staffing-editor article.rejected { opacity: .65; background: #fef2f2; }
.launch-staffing-editor label { display: flex; align-items: center; justify-content: space-between; gap: 8px; font-size: 11px; }
.launch-staffing-editor input[type="number"] { width: 78px; border: 1px solid var(--border); border-radius: 7px; padding: 5px 7px; }
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
.launch-sprint-editor { display: grid; grid-template-columns: 1fr 2fr 1fr 1fr; gap: 7px; margin: 9px 0; }
.launch-sprint-editor label, .launch-task-editor label { display: grid; gap: 3px; color: var(--muted); font-size: 10px; }
.launch-sprint-editor input, .launch-task-editor input, .launch-task-editor select { min-width: 0; border: 1px solid var(--border); border-radius: 7px; padding: 6px 7px; background: var(--surface); color: var(--text); }
.launch-task-editor { list-style: none; padding-left: 0 !important; }
.launch-task-editor li { display: grid; grid-template-columns: auto minmax(180px, 2fr) 80px minmax(150px, 1fr); gap: 7px; align-items: center; padding: 8px !important; border: 1px solid var(--border); border-radius: 8px; }
.launch-task-editor li small { grid-column: 2 / -1; }
.launch-editor-fieldset { min-width: 0; margin: 0; padding: 0; border: 0; }
.launch-editor-fieldset:disabled { opacity: .78; }
.launch-task-detail-editor { grid-column: 2 / -1; padding: 7px; border: 1px solid var(--border); border-radius: 8px; }
.launch-task-detail-editor > summary { cursor: pointer; color: var(--primary); }
.launch-task-detail-editor[open] { display: grid; gap: 8px; }
.launch-task-detail-editor textarea { width: 100%; min-width: 0; resize: vertical; border: 1px solid var(--border); border-radius: 7px; padding: 7px; background: var(--surface); color: var(--text); }
.launch-task-link-options { display: flex; flex-wrap: wrap; gap: 6px 12px; font-size: 11px; }
.launch-task-link-options strong { flex-basis: 100%; }
.launch-task-link-options label { display: flex; grid-auto-flow: column; justify-content: flex-start; }
.launch-task-order { grid-column: 2 / -1; display: flex; gap: 6px; }
.launch-task-order button, .launch-add-sprint { border: 1px solid var(--border); border-radius: 7px; padding: 5px 8px; background: var(--surface); color: var(--text); cursor: pointer; }
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
.launch-stepper--plan { padding: 10px 16px; border-top: 1px solid var(--border); }
.launch-receipt-links { display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0; }
.launch-receipt-links button { border: 1px solid var(--primary); border-radius: 8px; padding: 7px 10px; }
.launch-receipt details > summary { cursor: pointer; color: var(--muted); }
.launch-receipt details ul { display: grid; gap: 7px; padding: 0; list-style: none; }
.launch-replan { background: color-mix(in srgb, var(--surface) 90%, #f59e0b 10%); }
.launch-plan-actions { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 8px; padding: 12px 16px; border-top: 1px solid var(--border); }
.launch-plan-actions > div { display: grid; margin-right: auto; }
.launch-technical-details { margin-right: auto; color: var(--muted); font-size: 11px; }
.launch-technical-details summary { cursor: pointer; font-weight: 700; }
.launch-technical-details[open] { display: grid; gap: 3px; }
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
.assistant-free-answer input,
.assistant-free-answer textarea { min-width: 0; flex: 1; border: 1px solid var(--border); border-radius: 9px; background: var(--surface); color: var(--text); padding: 8px 10px; font: inherit; }
.assistant-free-answer textarea { resize: vertical; line-height: 1.45; }
.assistant-free-answer input:focus,
.assistant-free-answer textarea:focus { outline: 2px solid color-mix(in srgb, var(--primary) 35%, transparent); border-color: var(--primary); }
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

  .project-launch-columns,
  .project-launch-review-form,
  .project-launch-form-more-grid { grid-template-columns: 1fr; }
  .launch-stepper, .launch-template-grid, .launch-metric-grid, .launch-metric-simple-fields, .launch-metric-advanced > div, .launch-staffing-editor, .launch-sprint-editor, .launch-final-review-summary dl { grid-template-columns: 1fr; }
  .launch-simple-guide { grid-template-columns: 1fr; }
  .launch-simple-guide small { grid-column: 1; }
  .launch-task-editor li { grid-template-columns: auto minmax(0, 1fr); }
  .launch-task-editor li label, .launch-task-editor li small { grid-column: 2; }
  .project-launch-form-field--wide { grid-column: 1; }
  .project-launch-form-actions { align-items: stretch; flex-direction: column; }
  .project-launch-form-actions small { margin-right: 0; }
}
</style>
