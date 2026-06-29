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
  prompt: string
  projectId: string | null
  projectLabel: string
  createdAt: string
  assistantSnippet?: string | null
  attachmentCount?: number
}

export const AI_MODEL_OPTIONS: AiModelOption[] = [
  {
    id: 'fast-current',
    label: 'Nhanh hiện tại',
    shortLabel: 'Fast',
    providerLabel: 'QALY hiện tại',
    description: 'Dùng endpoint phân tích hiện có của QALY. Không kích hoạt provider mới.',
    status: 'live',
    badge: 'Live',
    latencyHint: 'Ưu tiên phản hồi nhanh'
  },
  {
    id: 'mock',
    label: 'Mock an toàn',
    shortLabel: 'Mock',
    providerLabel: 'Fallback cục bộ',
    description: 'Dùng phản hồi hệ thống hoặc dữ liệu mô phỏng khi AI bên ngoài chưa sẵn sàng.',
    status: 'mock',
    badge: 'Mock'
  },
  {
    id: 'deep-planned',
    label: 'DeepSeek planned',
    shortLabel: 'Deep',
    providerLabel: 'Provider planned',
    description: 'Chỉ là tuỳ chọn dự kiến trong registry, chưa có backend live ở pass này.',
    status: 'planned',
    badge: 'Planned',
    disabled: true,
    tooltip: 'Chưa kích hoạt provider DeepSeek trong backend.'
  },
  {
    id: 'provider-planned',
    label: 'Provider registry',
    shortLabel: 'Provider',
    providerLabel: 'OpenAI/Gemini planned',
    description: 'Khung lựa chọn provider tương lai. Pass này không gọi API provider mới.',
    status: 'planned',
    badge: 'Planned',
    disabled: true,
    tooltip: 'Đang là placeholder cho registry provider sau này.'
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
