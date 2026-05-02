export interface SummaryCardModel {
  key: 'projects' | 'tasks' | 'team'
  label: string
  value: string
  detail: string
  tone: 'blue' | 'mint' | 'violet'
}

export interface ProjectCardModel {
  id: string
  name: string
  description: string
  status: string
  statusLabel: string
  statusTone: string
  ownerName: string
  dueDateLabel: string
  completedTaskCount: number
  taskCount: number
  overdueTaskCount: number
  progressPercentage: number
  memberInitials: string[]
}

export interface TeamMiniMemberModel {
  id: string
  name: string
  role: string
  focusArea: string
  capacityPercent: number
  initials: string
}
