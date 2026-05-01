export interface DashboardResponse {
  generatedAt: string
  stats: DashboardStats
  summary: string
  riskDigest: string
  projects: DashboardProject[]
  team: DashboardMember[]
  notifications: DashboardNotification[]
}

export interface DashboardStats {
  activeProjects: number
  totalTasks: number
  overdueTasks: number
  teamMembers: number
  completedTasks: number
  completionRate: number
  tasksAtRisk: number
}

export interface DashboardProject {
  id: string
  name: string
  description: string | null
  status: string
  ownerName: string
  memberCount: number
  taskCount: number
  completedTaskCount: number
  overdueTaskCount: number
  progressPercentage: number
  memberNames: string[]
  tasks: DashboardTask[]
  createdAt: string
  endDate: string | null
}

export interface DashboardTask {
  id: string
  title: string
  status: string
  priority: string
  dueDate: string | null
  assigneeName: string | null
  reporterName: string
  projectName: string
  isPrivate: boolean
  commentCount: number
  attachmentCount: number
}

export interface DashboardMember {
  id: string
  fullName: string
  role: string
  email: string
  isActive: boolean
  assignedTaskCount: number
  completedTaskCount: number
  inProgressTaskCount: number
  overdueTaskCount: number
  capacityPercent: number
  focusArea: string
}

export interface DashboardNotification {
  id: string
  title: string
  message: string
  tone: 'critical' | 'warning' | 'info'
  createdAt: string
}
