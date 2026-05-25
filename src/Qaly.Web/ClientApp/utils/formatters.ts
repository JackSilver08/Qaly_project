export function formatDate(value: string | null) {
  if (!value) return 'Chưa đặt ngày'

  return new Intl.DateTimeFormat('vi-VN', {
    day: 'numeric',
    month: 'short',
  }).format(new Date(value))
}

export function formatTime(value: string | null | undefined) {
  if (!value) return ''
  return new Intl.DateTimeFormat('vi-VN', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

export function formatTimeAgo(value: string | null | undefined) {
  if (!value) return ''
  const date = new Date(value)
  const seconds = Math.floor((new Date().getTime() - date.getTime()) / 1000)
  let interval = seconds / 31536000
  if (interval > 1) return Math.floor(interval) + ' năm trước'
  interval = seconds / 2592000
  if (interval > 1) return Math.floor(interval) + ' tháng trước'
  interval = seconds / 86400
  if (interval > 1) return Math.floor(interval) + ' ngày trước'
  interval = seconds / 3600
  if (interval > 1) return Math.floor(interval) + ' giờ trước'
  interval = seconds / 60
  if (interval > 1) return Math.floor(interval) + ' phút trước'
  return 'vừa xong'
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
      return 'Đang hoạt động'
    case 'InProgress':
      return 'Đang làm'
    case 'InReview':
      return 'Đang duyệt'
    case 'Done':
      return 'Hoàn thành'
    case 'Planned':
      return 'Đã lên kế hoạch'
    case 'Archived':
      return 'Đã lưu trữ'
    case 'Todo':
      return 'Cần làm'
    case 'Cancelled':
      return 'Đã hủy'
    default:
      return status || 'Không xác định'
  }
}

export function displayRole(role: string | null | undefined) {
  if (!role) return ''
  const labels: Record<string, string> = {
    Admin: 'Quản trị hệ thống',
    User: 'Người dùng',
    Owner: 'Chủ dự án',
    ProjectOwner: 'Chủ dự án',
    PM: 'Quản lý dự án',
    ScrumMaster: 'Scrum Master',
    Developer: 'Lập trình viên',
    Tester: 'Kiểm thử viên',
    Reviewer: 'Người duyệt',
    Member: 'Thành viên',
    Viewer: 'Người xem',
    Customer: 'Khách hàng',
  }
  return labels[role] ?? role
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
