import { describe, expect, it } from 'vitest'
import { canReviewTask, taskNextStatuses } from '@/utils/task-transitions'

const member = { canManageAllTasks: false, canUpdateOwnTasks: true, canReviewEvidence: false }
const reviewer = { ...member, canReviewEvidence: true }
const manager = { ...reviewer, canManageAllTasks: true }
const task = { status: 'InProgress', assigneeId: 'member', reporterId: 'manager', isRestricted: false }
const workflow = { enableInReview: true, enableOnHold: true }

describe('task status review boundary', () => {
  it('allows the assignee to submit but never to complete their own task', () => {
    expect(taskNextStatuses(task, workflow, member, 'member')).toEqual(['InReview', 'OnHold'])
    expect(taskNextStatuses({ ...task, status: 'InReview' }, workflow, member, 'member')).not.toContain('Done')
    expect(taskNextStatuses(task, { enableInReview: false }, member, 'member')).not.toContain('Done')
  })
  it('does not let managers skip an enabled review stage', () => {
    expect(taskNextStatuses(task, workflow, manager, 'manager')).not.toContain('Done')
    expect(taskNextStatuses({ ...task, status: 'InReview' }, workflow, manager, 'manager')).toContain('Done')
    expect(taskNextStatuses(task, { enableInReview: false }, manager, 'manager')).toContain('Done')
  })
  it('allows an independent reviewer to approve or return submitted work only', () => {
    expect(taskNextStatuses(task, workflow, reviewer, 'reviewer')).toEqual([])
    expect(taskNextStatuses({ ...task, status: 'InReview' }, workflow, reviewer, 'reviewer')).toEqual(['InProgress', 'Done'])
    expect(taskNextStatuses({ ...task, status: 'InReview' }, { restrictTransitionsToAdmin: true }, reviewer, 'reviewer')).toEqual(['InProgress'])
  })
  it('supports delegated reviewers without letting an assignee approve themselves', () => {
    expect(canReviewTask({ ...task, reviewerId: 'delegate' }, member, 'delegate')).toBe(true)
    expect(canReviewTask({ ...task, reviewerId: 'member' }, reviewer, 'member')).toBe(false)
    expect(canReviewTask({ ...task, assigneeIds: ['reviewer'] }, reviewer, 'reviewer')).toBe(false)
    expect(canReviewTask({ ...task, assignees: [{ userId: 'reviewer' }] }, reviewer, 'reviewer')).toBe(false)
  })
  it.each([member, reviewer, manager])('makes Done terminal for every role', permissions => {
    expect(taskNextStatuses({ ...task, status: 'Done' }, workflow, permissions, 'member')).toEqual([])
  })
  it('fails closed for missing permissions, restricted objects and nonparticipants', () => {
    expect(taskNextStatuses(task, workflow, null, 'member')).toEqual([])
    expect(taskNextStatuses({ ...task, isRestricted: true }, workflow, manager, 'manager')).toEqual([])
    expect(taskNextStatuses(task, workflow, member, 'unassigned')).toEqual([])
    expect(taskNextStatuses(task, workflow, { ...member, canUpdateOwnTasks: false }, 'member')).toEqual([])
  })
  it('honors disabled workflow columns without creating new completion authority', () => {
    expect(taskNextStatuses(task, { enableInReview: false, enableOnHold: false }, member, 'member')).toEqual([])
  })
})
