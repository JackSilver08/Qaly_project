export type AiModelStatus = 'live' | 'mock' | 'planned' | 'fallback' | 'budget' | 'privacy'

export type AnalyticsMiniTab =
  | 'insights'
  | 'metrics'
  | 'risks'
  | 'sources'
  | 'report'
  | 'actions'
  | 'model'
  | 'history'
  | 'settings'

export type AiToolbarBehavior = 'open-drawer' | 'fill-prompt'

export type AiModelOption = {
  id: string
  label: string
  shortLabel?: string
  providerLabel: string
  description: string
  status: AiModelStatus
  badge: string
  disabled?: boolean
  tooltip?: string
  latencyHint?: string
  privacyNote?: string
}

export type SourceRef = {
  type?: 'Project' | 'Task' | 'TimeEntry' | 'Comment' | 'Meeting' | 'Evidence' | 'Audit' | 'System' | string
  id?: string | null
  label: string
  url?: string | null
  evidence?: string | null
  timestamp?: string | null
  confidence?: number | null
}

export type AiAnswerMetadata = {
  confidence?: number | null
  confidenceReason?: string | null
  freshness?: string | null
  latencyMs?: number | null
  sourceCount?: number | null
  usedAi?: boolean | null
  modelLabel?: string | null
}

export type InsightCardAction = {
  key: string
  label: string
  prompt?: string
  tab?: AnalyticsMiniTab
  requiresConfirmation?: boolean
  disabled?: boolean
}

export type AiToolbarAction = {
  key: string
  label: string
  shortLabel?: string
  description?: string
  tab: AnalyticsMiniTab
  behavior: AiToolbarBehavior
  prompt?: string
  isWriteLike?: boolean
}

export type ConversationHistoryItem = {
  id: string
  sessionId: string
  title: string
  status: string
  version: number
  prompt: string
  projectId: string | null
  projectLabel: string
  createdAt: string
  assistantSnippet?: string | null
  attachmentCount?: number
  turnCount?: number
  archivedAt?: string | null
}

export const AI_MODEL_OPTIONS: AiModelOption[] = [
  {
    id: 'auto',
    label: 'Tự động',
    shortLabel: 'Auto',
    providerLabel: 'Qaly AI Router',
    description: 'Qaly chọn provider đang khả dụng và hiển thị model thực tế đã trả lời.',
    status: 'live',
    badge: 'Live',
    latencyHint: 'Có fallback minh bạch khi provider chính lỗi'
  },
  {
    id: 'ollama-local',
    label: 'Qwen 2.5 3B',
    shortLabel: 'Local',
    providerLabel: 'Ollama local',
    description: 'Chạy trên máy hiện tại, không gửi nội dung lên cloud.',
    status: 'live',
    badge: 'Local',
    privacyNote: 'Phù hợp dữ liệu cần xử lý cục bộ.'
  },
  {
    id: 'deepseek-v4-pro',
    label: 'DeepSeek V4 Pro',
    shortLabel: 'V4 Pro',
    providerLabel: 'DeepSeek Cloud',
    description: 'Phân tích chuyên sâu bằng DeepSeek V4 Pro qua API cloud.',
    status: 'live',
    badge: 'Cloud',
    privacyNote: 'Dữ liệu nhạy cảm cần policy và consent cho cloud.'
  },
  {
    id: 'provider-planned',
    label: 'Provider registry',
    shortLabel: 'Provider',
    providerLabel: 'OpenAI/Gemini',
    description: 'Các provider khác chưa được mở cho người dùng chọn trực tiếp.',
    status: 'planned',
    badge: 'Planned',
    disabled: true,
    tooltip: 'Chưa có cấu hình live cho provider này.'
  }
]

export function aiModelStatusLabel(status: AiModelStatus) {
  switch (status) {
    case 'live':
      return 'Live'
    case 'mock':
      return 'Mock'
    case 'planned':
      return 'Planned'
    case 'fallback':
      return 'Fallback'
    case 'budget':
      return 'Budget'
    case 'privacy':
      return 'Privacy'
    default:
      return 'Model'
  }
}

export function aiModelCompactLabel(option?: AiModelOption) {
  if (!option) return 'Model'
  return `${option.shortLabel || option.label} · ${option.badge || aiModelStatusLabel(option.status)}`
}
