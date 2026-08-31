import { isTaskOpen, isTaskOverdue } from './formatters'

export type TaskWorkflowConfiguration = {
  enableOnHold?: boolean | null
  enableInReview?: boolean | null
}

export type TaskFocus = 'all' | 'overdue' | 'dueSoon' | 'pinned' | 'high' | 'blocked'

export type TaskWorkspaceRecord = {
  title: string
  projectId: string
  projectName?: string | null
  projectCode?: string | null
  reporterName?: string | null
  assigneeName?: string | null
  status: string
  priority: string
  dueDate?: string | null
  isPinned?: boolean
}

export type TaskWorkspaceFilters = {
  project: string
  status: string
  priority: string
  focus: TaskFocus
  query: string
}

/** Build the board from the canonical workflow flags. Missing flags preserve the
 * historical five-column workflow; an explicit false removes that optional stage.
 */
export function taskStatusColumns(project: TaskWorkflowConfiguration | null | undefined) {
  const columns = ['Todo', 'InProgress']
  if (!project || project.enableOnHold !== false) columns.push('OnHold')
  if (!project || project.enableInReview !== false) columns.push('InReview')
  columns.push('Done')
  return columns
}

export function isTaskDueSoon(task: Pick<TaskWorkspaceRecord, 'status' | 'dueDate'>, now = Date.now()) {
  if (!task.dueDate || !isTaskOpen(task.status)) return false
  const dueAt = new Date(task.dueDate).getTime()
  return Number.isFinite(dueAt) && dueAt > now && dueAt - now <= 48 * 60 * 60 * 1000
}

export function matchesTaskFocus(task: TaskWorkspaceRecord, focus: TaskFocus, now = Date.now()) {
  if (focus === 'overdue') return isTaskOverdue(task, now)
  if (focus === 'dueSoon') return isTaskDueSoon(task, now)
  if (focus === 'pinned') return Boolean(task.isPinned)
  if (focus === 'high') return task.priority === 'High' || task.priority === 'Critical'
  if (focus === 'blocked') return task.status === 'Blocked' || task.status === 'OnHold'
  return true
}

export function matchesTaskFilters(task: TaskWorkspaceRecord, filters: TaskWorkspaceFilters, now = Date.now()) {
  if (filters.project !== 'all' && task.projectId !== filters.project) return false
  if (filters.status !== 'all' && task.status !== filters.status) return false
  if (filters.priority !== 'all' && task.priority !== filters.priority) return false
  if (!matchesTaskFocus(task, filters.focus, now)) return false

  const query = filters.query.trim().toLowerCase()
  if (!query) return true
  return [
    task.title,
    task.projectName,
    task.reporterName,
    task.assigneeName,
    task.status,
    task.priority,
    task.projectCode,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase()
    .includes(query)
}
