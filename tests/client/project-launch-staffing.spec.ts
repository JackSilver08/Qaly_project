import { describe, expect, it } from 'vitest'
import { editLaunchStaffing, normalizeLaunchStaffing, selectLaunchStaffing, type LaunchStaffingDraftMember } from '../../src/Qaly.Web/ClientApp/components/chat/project-launch-staffing'

const team = (): LaunchStaffingDraftMember[] => [
  { userId: 'old-manager', proposedRole: 'Manager', proposedHours: 20, included: true, manager: true },
  { userId: 'new-manager', proposedRole: 'Reviewer', proposedHours: 16, included: false, manager: false },
  { userId: 'developer', proposedRole: 'Developer', proposedHours: 32, included: true, manager: false },
]

describe('Project Launch staffing controls', () => {
  it('auto/select-all changes manager without submitting a stale Manager role', () => {
    const original = team()
    const selected = selectLaunchStaffing(original, new Set(original.map(item => item.userId)), 'new-manager')
    expect(selected[0]).toMatchObject({ proposedRole: 'Member', manager: false, included: true, proposedHours: 20 })
    expect(selected[1]).toMatchObject({ proposedRole: 'Manager', manager: true, included: true })
    expect(selected[2]).toEqual(original[2])
    expect(selected.filter(item => item.proposedRole === 'Manager')).toHaveLength(1)
    expect(original).toEqual(team())
  })

  it('manual manager radio includes the selected person and demotes the old manager', () => {
    const edited = editLaunchStaffing(team(), 'new-manager', 'manager', true)
    expect(edited[0]).toMatchObject({ proposedRole: 'Member', manager: false })
    expect(edited[1]).toMatchObject({ proposedRole: 'Manager', included: true, manager: true })
  })

  it('member checkbox can both include and exclude a person', () => {
    const included = editLaunchStaffing(team(), 'new-manager', 'included', true)
    expect(included[1]).toMatchObject({ included: true, manager: false, proposedRole: 'Reviewer' })
    const excluded = editLaunchStaffing(included, 'new-manager', 'included', false)
    expect(excluded[1].included).toBe(false)
  })

  it('deselecting a manager clears its role and does not silently select another manager', () => {
    const excluded = editLaunchStaffing(team(), 'old-manager', 'included', false)
    expect(excluded[0]).toMatchObject({ included: false, manager: false, proposedRole: 'Member' })
    expect(excluded.some(item => item.manager)).toBe(false)
    const includedAgain = editLaunchStaffing(excluded, 'old-manager', 'included', true)
    expect(includedAgain[0]).toMatchObject({ included: true, manager: false, proposedRole: 'Member' })
  })

  it('clear selection leaves no hidden manager and can be followed by auto selection', () => {
    const cleared = selectLaunchStaffing(team(), new Set())
    expect(cleared.every(item => !item.included && !item.manager && item.proposedRole !== 'Manager')).toBe(true)
    const selected = selectLaunchStaffing(cleared, new Set(['new-manager', 'developer']), 'new-manager')
    expect(selected.filter(item => item.manager).map(item => item.userId)).toEqual(['new-manager'])
    expect(normalizeLaunchStaffing(selected)).toEqual(selected)
  })

  it('role/hour edits preserve other members and cannot contradict the manager radio', () => {
    const edited = editLaunchStaffing(team(), 'developer', 'proposedRole', 'Tester')
    const hours = editLaunchStaffing(edited, 'developer', 'proposedHours', 24)
    expect(hours[2]).toMatchObject({ proposedRole: 'Tester', proposedHours: 24 })
    expect(hours[0]).toEqual(team()[0])
    expect(editLaunchStaffing(hours, 'old-manager', 'proposedRole', 'Developer')[0].proposedRole).toBe('Manager')
  })

  it('repairs a restored stale manager role without inventing a manager', () => {
    const stale = team().map(item => ({ ...item, manager: false }))
    const normalized = normalizeLaunchStaffing(stale)
    expect(normalized[0].proposedRole).toBe('Member')
    expect(normalized.some(item => item.manager)).toBe(false)
  })

  it.each(['Owner', 'Admin', 'UnknownRole'])('does not hide invalid %s input from server validation', role => {
    const members = [{ ...team()[2], proposedRole: role }]
    expect(normalizeLaunchStaffing(members)[0].proposedRole).toBe(role)
  })
})
