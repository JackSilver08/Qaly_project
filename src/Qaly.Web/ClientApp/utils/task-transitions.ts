import type { DashboardTask, ProjectPermissionsDto } from '../types'

type Workflow = { enableInReview?: boolean; enableOnHold?: boolean; restrictTransitionsToAdmin?: boolean }
type Permissions = Pick<ProjectPermissionsDto, 'canManageAllTasks' | 'canUpdateOwnTasks' | 'canReviewEvidence'>
type Task = Pick<DashboardTask, 'status' | 'assigneeId' | 'reporterId' | 'reviewerId' | 'assigneeIds' | 'assignees' | 'isRestricted'>

export function canReviewTask(task: Task, permissions: Permissions | null | undefined, actorId: string | null | undefined) {
  if (!actorId || !permissions || task.isRestricted) return false
  if (permissions.canManageAllTasks) return true
  if (!permissions.canUpdateOwnTasks || isAssignee(task, actorId)) return false
  return permissions.canReviewEvidence || task.reviewerId === actorId
}

function isAssignee(task: Task, actorId: string) {
  return task.assigneeId === actorId || task.assigneeIds?.includes(actorId) || task.assignees?.some(a => a.userId === actorId)
}

/** UI guidance only: all mutation endpoints recheck current scope, role and evidence. */
export function taskNextStatuses(task: Task, project: Workflow | null | undefined, permissions: Permissions | null | undefined, actorId: string | null | undefined): string[] {
  if (!project || !permissions || !actorId || task.isRestricted || task.status === 'Done') return []
  const manages = permissions.canManageAllTasks
  const ownsTask = permissions.canUpdateOwnTasks && (task.reporterId === actorId || isAssignee(task, actorId))
  const reviews = canReviewTask(task, permissions, actorId)
  if (!manages && !ownsTask && !(task.status === 'InReview' && reviews)) return []

  const transitions: Record<string, string[]> = {
    Todo: ['InProgress', 'OnHold'],
    InProgress: ['InReview', 'OnHold', 'Done'],
    InReview: manages || ownsTask ? ['InProgress', 'Done', 'OnHold'] : ['InProgress', 'Done'],
    OnHold: ['Todo', 'InProgress'],
    Cancelled: ['Todo'],
  }
  return (transitions[task.status] ?? []).filter(status => {
    if (status === 'OnHold') return project.enableOnHold !== false
    if (status === 'InReview') return project.enableInReview !== false
    if (status === 'Done') return (project.restrictTransitionsToAdmin ? manages : reviews) &&
      (project.enableInReview === false || task.status === 'InReview')
    return true
  })
}

export function taskTransitionHint(status: string) {
  return status === 'Done'
    ? 'Nhiệm vụ đã hoàn thành; không thể chuyển về trạng thái trước.'
    : 'Gửi Đang duyệt trước khi hoàn thành. Người review hoặc quản lý xác nhận; minh chứng phải được duyệt nếu dự án yêu cầu.'
}
