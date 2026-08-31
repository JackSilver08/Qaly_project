import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  displayRole,
  displayStatus,
  formatDate,
  formatFileSize,
  formatTime,
  formatTimeAgo,
  initials,
  isTaskOpen,
  isTaskOverdue,
  statusTone,
} from '@/utils/formatters'

afterEach(() => {
  vi.useRealTimers()
})

describe('formatDate', () => {
  it('returns the empty-state label when no date is supplied', () => {
    expect(formatDate(null)).toBe('Chưa đặt ngày')
    expect(formatDate('')).toBe('Chưa đặt ngày')
  })

  it('formats an ISO date with the vi-VN day/short-month pattern', () => {
    const formatted = formatDate('2026-03-14T00:00:00.000Z')
    expect(formatted).toMatch(/14/)
    expect(formatted).toMatch(/3/)
  })
})

describe('formatTime', () => {
  it('returns an empty string for missing values', () => {
    expect(formatTime(null)).toBe('')
    expect(formatTime(undefined)).toBe('')
    expect(formatTime('')).toBe('')
  })

  it('renders hour and 2-digit minute', () => {
    expect(formatTime('2026-03-14T09:05:00.000Z')).toMatch(/\d{1,2}:\d{2}/)
  })
})

describe('formatTimeAgo', () => {
  const now = new Date('2026-03-14T12:00:00.000Z')

  function ago(seconds: number) {
    vi.useFakeTimers()
    vi.setSystemTime(now)
    return formatTimeAgo(new Date(now.getTime() - seconds * 1000).toISOString())
  }

  it('returns an empty string for missing values', () => {
    expect(formatTimeAgo(null)).toBe('')
    expect(formatTimeAgo(undefined)).toBe('')
  })

  it('reports sub-minute gaps as "vừa xong"', () => {
    expect(ago(5)).toBe('vừa xong')
    expect(ago(59)).toBe('vừa xong')
  })

  it('escalates through minute, hour, day, month and year buckets', () => {
    expect(ago(61 * 1)).toBe('1 phút trước')
    expect(ago(60 * 45)).toBe('45 phút trước')
    expect(ago(3600 * 5)).toBe('5 giờ trước')
    expect(ago(86400 * 3)).toBe('3 ngày trước')
    expect(ago(2592000 * 2)).toBe('2 tháng trước')
    expect(ago(31536000 * 2)).toBe('2 năm trước')
  })

  it('treats an exact 60-second gap as "vừa xong" because the bucket test is strictly greater than 1', () => {
    // Documents the boundary rather than asserting a nicer behaviour that does not exist.
    expect(ago(60)).toBe('vừa xong')
  })
})

describe('formatFileSize', () => {
  it('uses bytes below 1 KiB', () => {
    expect(formatFileSize(0)).toBe('0 B')
    expect(formatFileSize(1023)).toBe('1023 B')
  })

  it('uses rounded kilobytes below 1 MiB', () => {
    expect(formatFileSize(1024)).toBe('1 KB')
    expect(formatFileSize(1536)).toBe('2 KB')
  })

  it('uses one decimal megabytes at or above 1 MiB', () => {
    expect(formatFileSize(1024 * 1024)).toBe('1.0 MB')
    expect(formatFileSize(1024 * 1024 * 2.5)).toBe('2.5 MB')
  })
})

describe('initials', () => {
  it('falls back to "?" when the name is missing', () => {
    expect(initials(null)).toBe('?')
    expect(initials(undefined)).toBe('?')
    expect(initials('')).toBe('?')
  })

  it('takes at most the first two word initials, uppercased', () => {
    expect(initials('Trần Quang Tuấn')).toBe('TQ')
    expect(initials('bao ngoc')).toBe('BN')
    expect(initials('Qaly')).toBe('Q')
  })

  it('ignores repeated whitespace', () => {
    expect(initials('  Nguyen    Van  ')).toBe('NV')
  })
})

describe('isTaskOpen', () => {
  it.each(['Todo', 'InProgress', 'InReview', 'OnHold', 'todo', ' inprogress '])(
    'accepts canonical open status %s',
    (status) => expect(isTaskOpen(status)).toBe(true),
  )

  it.each(['Done', 'Cancelled', 'Canceled', 'Completed', 'Closed', 'Unknown', '', null, undefined])(
    'rejects terminal or unsupported status %s',
    (status) => expect(isTaskOpen(status)).toBe(false),
  )
})

describe('isTaskOverdue', () => {
  it('is false without a due date', () => {
    expect(isTaskOverdue({ dueDate: null, status: 'Todo' })).toBe(false)
    expect(isTaskOverdue({ dueDate: undefined, status: 'Todo' })).toBe(false)
  })

  it('is true for a past due date on an unfinished task', () => {
    expect(isTaskOverdue({ dueDate: '2000-01-01T00:00:00.000Z', status: 'InProgress' })).toBe(true)
  })

  it('is false for a future due date', () => {
    expect(isTaskOverdue({ dueDate: '2999-01-01T00:00:00.000Z', status: 'InProgress' })).toBe(false)
  })

  it('is false once the task is Done', () => {
    expect(isTaskOverdue({ dueDate: '2000-01-01T00:00:00.000Z', status: 'Done' })).toBe(false)
  })

  it('is false once the task is Cancelled', () => {
    // QALY-UI-01: the server counts overdue only among Todo/InProgress/InReview/OnHold, so a
    // cancelled past-due task must not inflate the client-side overdue badge.
    expect(isTaskOverdue({ dueDate: '2000-01-01T00:00:00.000Z', status: 'Cancelled' })).toBe(false)
  })

  it.each(['Canceled', 'Completed', 'Closed', 'Unknown'])(
    'does not count legacy or unsupported status %s as overdue',
    (status) => expect(isTaskOverdue({ dueDate: '2000-01-01T00:00:00.000Z', status })).toBe(false),
  )

  it('is false for an unparsable due date instead of reporting NaN as overdue', () => {
    expect(isTaskOverdue({ dueDate: 'not-a-date', status: 'Todo' })).toBe(false)
  })

  it('still tracks OnHold work as overdue', () => {
    expect(isTaskOverdue({ dueDate: '2000-01-01T00:00:00.000Z', status: 'OnHold' })).toBe(true)
  })
})

describe('displayStatus', () => {
  it.each([
    ['Active', 'Đang hoạt động'],
    ['InProgress', 'Đang làm'],
    ['InReview', 'Đang duyệt'],
    ['Done', 'Hoàn thành'],
    ['Planned', 'Đã lên kế hoạch'],
    ['Archived', 'Đã lưu trữ'],
    ['Todo', 'Cần làm'],
    ['Cancelled', 'Đã hủy'],
  ])('maps %s to its Vietnamese label', (status, label) => {
    expect(displayStatus(status)).toBe(label)
  })

  it('passes unknown statuses through and labels blanks', () => {
    expect(displayStatus('Blocked')).toBe('Blocked')
    expect(displayStatus(null)).toBe('Không xác định')
    expect(displayStatus('')).toBe('Không xác định')
  })
})

describe('displayRole', () => {
  it('maps known roles', () => {
    expect(displayRole('Admin')).toBe('Quản trị hệ thống')
    expect(displayRole('ProjectOwner')).toBe('Chủ dự án')
    expect(displayRole('ScrumMaster')).toBe('Scrum Master')
  })

  it('returns the raw value for unknown roles and empty for blanks', () => {
    expect(displayRole('Architect')).toBe('Architect')
    expect(displayRole(null)).toBe('')
  })
})

describe('statusTone', () => {
  it('collapses statuses into the four presentation tones', () => {
    expect(statusTone('Active')).toBe('active')
    expect(statusTone('InProgress')).toBe('active')
    expect(statusTone('Planned')).toBe('planned')
    expect(statusTone('Archived')).toBe('archived')
    expect(statusTone('Done')).toBe('neutral')
    expect(statusTone(null)).toBe('neutral')
  })
})
