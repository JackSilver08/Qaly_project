export function formatDate(value: string | null) {
  if (!value) return 'No date'

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value))
}

export function formatTime(value: string | null | undefined) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

export function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export function initials(name: string | null | undefined) {
  if (!name) return '?'
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

export function isTaskOverdue(task: { dueDate: string | null | undefined; status: string | null | undefined }) {
  return Boolean(task.dueDate) && new Date(task.dueDate as string).getTime() < Date.now() && task.status !== 'Done'
}

export function displayStatus(status: string | null | undefined) {
  switch (status) {
    case 'Active':
      return 'Active'
    case 'InProgress':
      return 'In progress'
    case 'InReview':
      return 'In review'
    case 'Done':
      return 'Done'
    case 'Planned':
      return 'Planned'
    case 'Archived':
      return 'Archived'
    case 'Todo':
      return 'Todo'
    case 'Cancelled':
      return 'Cancelled'
    default:
      return status || 'Unknown'
  }
}

export function displayRole(role: string | null | undefined) {
  return role === 'Admin' ? 'Admin' : role === 'Member' ? 'Member' : role || ''
}

export function statusTone(status: string | null | undefined) {
  switch (status) {
    case 'Active':
    case 'InProgress':
      return 'active'
    case 'Planned':
      return 'planned'
    case 'Archived':
      return 'archived'
    default:
      return 'neutral'
  }
}