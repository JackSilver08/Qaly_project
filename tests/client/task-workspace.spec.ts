import { describe, expect, it } from 'vitest'
import { isTaskDueSoon, matchesTaskFilters, taskStatusColumns } from '@/utils/task-workspace'

describe('taskStatusColumns', () => {
  it('keeps the complete workflow when configuration is absent', () => {
    expect(taskStatusColumns(null)).toEqual(['Todo', 'InProgress', 'OnHold', 'InReview', 'Done'])
  })

  it('hides OnHold only when the project explicitly disables it', () => {
    expect(taskStatusColumns({ enableOnHold: false, enableInReview: true }))
      .toEqual(['Todo', 'InProgress', 'InReview', 'Done'])
  })

  it('independently hides InReview without affecting OnHold', () => {
    expect(taskStatusColumns({ enableOnHold: true, enableInReview: false }))
      .toEqual(['Todo', 'InProgress', 'OnHold', 'Done'])
  })

  it('keeps the stable endpoints when both optional stages are disabled', () => {
    expect(taskStatusColumns({ enableOnHold: false, enableInReview: false }))
      .toEqual(['Todo', 'InProgress', 'Done'])
  })
})

describe('task workspace filters', () => {
  const now = new Date('2026-08-31T12:00:00Z').getTime()
  const task = {
    title: 'Hoàn thiện thanh toán', projectId: 'project-1', projectName: 'Qaly Release',
    projectCode: 'QALY', reporterName: 'Bảo', assigneeName: 'Chi', status: 'InProgress',
    priority: 'High', dueDate: '2026-09-01T12:00:00Z', isPinned: true,
  }

  it('matches search text across task, project and people fields', () => {
    const base = { project: 'all', status: 'all', priority: 'all', focus: 'all' as const }
    expect(matchesTaskFilters(task, { ...base, query: 'thanh toán' }, now)).toBe(true)
    expect(matchesTaskFilters(task, { ...base, query: 'qaly' }, now)).toBe(true)
    expect(matchesTaskFilters(task, { ...base, query: 'không có' }, now)).toBe(false)
  })

  it('combines project, status and priority filters instead of weakening one another', () => {
    expect(matchesTaskFilters(task, {
      project: 'project-1', status: 'InProgress', priority: 'High', focus: 'all', query: '',
    }, now)).toBe(true)
    expect(matchesTaskFilters(task, {
      project: 'project-2', status: 'InProgress', priority: 'High', focus: 'all', query: '',
    }, now)).toBe(false)
  })

  it('supports overdue, due-soon, pinned, high-priority and blocked focus modes', () => {
    const filter = (focus: 'overdue' | 'dueSoon' | 'pinned' | 'high' | 'blocked') =>
      matchesTaskFilters(task, { project: 'all', status: 'all', priority: 'all', focus, query: '' }, now)
    expect(filter('overdue')).toBe(false)
    expect(filter('dueSoon')).toBe(true)
    expect(filter('pinned')).toBe(true)
    expect(filter('high')).toBe(true)
    expect(filter('blocked')).toBe(false)
    expect(matchesTaskFilters(
      { ...task, dueDate: '2026-08-30T12:00:00Z' },
      { project: 'all', status: 'all', priority: 'all', focus: 'overdue', query: '' },
      now,
    )).toBe(true)
  })

  it('never reports a terminal task as due soon', () => {
    expect(isTaskDueSoon({ ...task, status: 'Cancelled' }, now)).toBe(false)
    expect(isTaskDueSoon({ ...task, status: 'Completed' }, now)).toBe(false)
  })
})
