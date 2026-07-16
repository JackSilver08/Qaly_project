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
  ownerId: string
  ownerName: string
  dueDateLabel: string
  completedTaskCount: number
  taskCount: number
  overdueTaskCount: number
  progressPercentage: number
  memberInitials: string[]
  code?: string
  memberCount?: number
  endDate?: string | null
  archivedAt?: string | null
}

export interface TaskListItemModel {
  id: string
  projectId: string
  title: string
  projectName: string
  assignedAtLabel: string
  priority: string
  dueDateLabel: string
  reporterName: string
  reporterInitials: string
  statusLabel: string
  isOverdue: boolean
}

export interface TeamMiniMemberModel {
  id: string
  name: string
  role: string
  focusArea: string
  capacityPercent: number
  initials: string
}
