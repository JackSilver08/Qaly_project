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

export interface ApiResult<T> {
  isSuccess: boolean
  data: T | null
  error: string | null
  statusCode: number
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface UserDto {
  id: string
  fullName: string
  email: string
  role: string
  isActive: boolean
  avatarUrl: string | null
  createdAt: string
}

export interface ProjectDto {
  id: string
  name: string
  description: string | null
  status: string
  startDate: string | null
  endDate: string | null
  ownerId: string
  ownerName: string
  memberCount: number
  taskCount: number
  createdAt: string
}

export interface TaskItemDto {
  id: string
  title: string
  description: string | null
  status: string
  priority: string
  dueDate: string | null
  estimatedHours: number | null
  actualHours: number | null
  isPrivate: boolean
  projectId: string
  projectName: string
  assigneeId: string | null
  assigneeName: string | null
  reporterId: string
  reporterName: string
  commentCount: number
  attachmentCount: number
  aiPrioritySuggestion: string | null
  createdAt: string
}

export interface CommentDto {
  id: string
  content: string
  taskItemId: string
  authorId: string
  authorName: string
  authorAvatarUrl: string | null
  createdAt: string
  updatedAt: string | null
}

export interface NotificationDto {
  id: string
  message: string
  type: string
  isRead: boolean
  relatedEntityId: string | null
  relatedEntityType: string | null
  createdAt: string
}

export interface AttachmentDto {
  id: string
  fileName: string
  filePath: string
  fileSize: number
  contentType: string | null
  taskItemId: string
  uploadedById: string
  uploadedByName: string
  uploadedAt: string
}
