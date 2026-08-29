import { describe, expect, it } from 'vitest'
import {
  AI_TIER_DESCRIPTIONS,
  PROJECT_ROLE_OPTIONS,
  PROJECT_ROLE_OWNER,
  fallbackProjectPermissions,
  groupedProjectRoles,
  projectRoleHint,
  projectRoleLabel,
} from '@/utils/project-roles'

describe('PROJECT_ROLE_OPTIONS', () => {
  it('exposes every assignable role exactly once', () => {
    const values = PROJECT_ROLE_OPTIONS.map((option) => option.value)
    expect(new Set(values).size).toBe(values.length)
    expect(values).toEqual([
      'Manager',
      'ScrumMaster',
      'Developer',
      'Tester',
      'Reviewer',
      'Member',
      'Viewer',
      'Customer',
    ])
  })

  it('never offers Owner through the membership picker', () => {
    expect(PROJECT_ROLE_OPTIONS.map((option) => option.value)).not.toContain(PROJECT_ROLE_OWNER)
  })

  it('gives every role a non-empty label and hint', () => {
    for (const option of PROJECT_ROLE_OPTIONS) {
      expect(option.label.trim()).not.toBe('')
      expect(option.hint.trim()).not.toBe('')
    }
  })
})

describe('projectRoleLabel', () => {
  it('defaults to "Thành viên" when no role is set', () => {
    expect(projectRoleLabel(null)).toBe('Thành viên')
    expect(projectRoleLabel('')).toBe('Thành viên')
  })

  it('maps both owner spellings to "Chủ dự án"', () => {
    expect(projectRoleLabel('Owner')).toBe('Chủ dự án')
    expect(projectRoleLabel('ProjectOwner')).toBe('Chủ dự án')
    expect(projectRoleLabel('project owner')).toBe('Chủ dự án')
  })

  it('is case- and whitespace-insensitive', () => {
    expect(projectRoleLabel('scrummaster')).toBe('Scrum Master')
    expect(projectRoleLabel('Scrum Master')).toBe('Scrum Master')
    expect(projectRoleLabel('  MANAGER ')).toBe('Quản lý dự án')
  })

  it('falls back to the raw value for roles the UI does not know', () => {
    expect(projectRoleLabel('Architect')).toBe('Architect')
  })
})

describe('projectRoleHint', () => {
  it('returns an empty hint for a missing role', () => {
    expect(projectRoleHint(null)).toBe('')
  })

  it('explains that the owner cannot be removed', () => {
    expect(projectRoleHint('Owner')).toContain('không thể bị gỡ')
  })

  it('returns the catalogue hint for known roles and empty for unknown ones', () => {
    expect(projectRoleHint('Viewer')).toBe('Chỉ đọc. Không ghi bất kỳ dữ liệu nào.')
    expect(projectRoleHint('Architect')).toBe('')
  })
})

describe('groupedProjectRoles', () => {
  it('preserves catalogue order and covers every role', () => {
    const groups = groupedProjectRoles()
    expect(groups.map((group) => group.group)).toEqual(['Quản lý', 'Chuyên môn', 'Cơ bản', 'Chỉ đọc'])
    expect(groups.flatMap((group) => group.options)).toHaveLength(PROJECT_ROLE_OPTIONS.length)
  })
})

describe('fallbackProjectPermissions', () => {
  const outsider = { role: null, isOwner: false, isSystemAdmin: false }

  it('grants nothing to a non-member', () => {
    const permissions = fallbackProjectPermissions(outsider)
    expect(permissions.role).toBe('')
    expect(permissions.roleLabel).toBe('Không thuộc dự án')
    expect(permissions.canManageProject).toBe(false)
    expect(permissions.canCreateTask).toBe(false)
    expect(permissions.canComment).toBe(false)
    expect(permissions.canReadInternalWiki).toBe(false)
    expect(permissions.aiTier).toBe('None')
  })

  it('gives the owner full control regardless of the role string', () => {
    const permissions = fallbackProjectPermissions({ role: 'Viewer', isOwner: true, isSystemAdmin: false })
    expect(permissions.role).toBe('Owner')
    expect(permissions.roleLabel).toBe('Chủ dự án')
    expect(permissions.canManageProject).toBe(true)
    expect(permissions.canManageMembers).toBe(true)
    expect(permissions.canManageIntegrations).toBe(true)
    expect(permissions.aiTier).toBe('Full')
  })

  it('gives a system admin full control without project membership', () => {
    const permissions = fallbackProjectPermissions({ role: null, isOwner: false, isSystemAdmin: true })
    expect(permissions.canManageProject).toBe(true)
    expect(permissions.aiTier).toBe('Full')
  })

  it.each(['Manager', 'PM', 'ProjectManager', 'ScrumMaster', 'Admin'])(
    'treats %s as a managing role',
    (role) => {
      const permissions = fallbackProjectPermissions({ role, isOwner: false, isSystemAdmin: false })
      expect(permissions.canManageProject).toBe(true)
      expect(permissions.canManageAllTasks).toBe(true)
      expect(permissions.canReviewEvidence).toBe(true)
      expect(permissions.aiTier).toBe('Full')
    },
  )

  it.each(['Developer', 'Tester', 'Reviewer'])('treats %s as a specialist', (role) => {
    const permissions = fallbackProjectPermissions({ role, isOwner: false, isSystemAdmin: false })
    expect(permissions.canManageProject).toBe(false)
    expect(permissions.canCreateTask).toBe(true)
    expect(permissions.canUpdateOwnTasks).toBe(true)
    expect(permissions.aiTier).toBe('Specialist')
  })

  it('lets a Tester or Reviewer review evidence but not a Developer', () => {
    const reviewer = fallbackProjectPermissions({ role: 'Reviewer', isOwner: false, isSystemAdmin: false })
    const tester = fallbackProjectPermissions({ role: 'Tester', isOwner: false, isSystemAdmin: false })
    const developer = fallbackProjectPermissions({ role: 'Developer', isOwner: false, isSystemAdmin: false })
    expect(reviewer.canReviewEvidence).toBe(true)
    expect(tester.canReviewEvidence).toBe(true)
    expect(developer.canReviewEvidence).toBe(false)
  })

  it('gives a plain Member contributor-level access without task creation', () => {
    const permissions = fallbackProjectPermissions({ role: 'Member', isOwner: false, isSystemAdmin: false })
    expect(permissions.canCreateTask).toBe(false)
    expect(permissions.canUpdateOwnTasks).toBe(true)
    expect(permissions.canComment).toBe(true)
    expect(permissions.canTrackTime).toBe(true)
    expect(permissions.canWriteWiki).toBe(true)
    expect(permissions.aiTier).toBe('Contributor')
  })

  it.each(['Viewer', 'Customer'])('keeps %s strictly read-only', (role) => {
    const permissions = fallbackProjectPermissions({ role, isOwner: false, isSystemAdmin: false })
    expect(permissions.canUpdateOwnTasks).toBe(false)
    expect(permissions.canComment).toBe(false)
    expect(permissions.canTrackTime).toBe(false)
    expect(permissions.canWriteWiki).toBe(false)
    expect(permissions.canManageProject).toBe(false)
    expect(permissions.aiTier).toBe('ReadOnly')
  })

  it('hides the internal wiki from a Customer but not from a Viewer', () => {
    expect(fallbackProjectPermissions({ role: 'Customer', isOwner: false, isSystemAdmin: false }).canReadInternalWiki).toBe(false)
    expect(fallbackProjectPermissions({ role: 'Viewer', isOwner: false, isSystemAdmin: false }).canReadInternalWiki).toBe(true)
  })

  it('always pairs the AI tier with its description', () => {
    for (const role of [null, 'Manager', 'Developer', 'Member', 'Viewer']) {
      const permissions = fallbackProjectPermissions({ role, isOwner: false, isSystemAdmin: false })
      expect(permissions.aiTierDescription).toBe(AI_TIER_DESCRIPTIONS[permissions.aiTier])
    }
  })
})
