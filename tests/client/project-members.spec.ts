import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/api-client', () => ({
  apiResult: vi.fn().mockResolvedValue([]),
}))

import ProjectMembersTab from '@/components/ProjectMembersTab.vue'
import { apiResult } from '@/utils/api-client'

beforeEach(() => {
  vi.mocked(apiResult).mockResolvedValue([])
})

const members = [
  {
    id: 'owner-1',
    fullName: 'Project Owner',
    email: 'owner@qaly.dev',
    initials: 'PO',
    role: 'Owner',
    canViewProjectTimeline: true,
    canViewTaskRisk: true,
    canNudgeAssignee: true,
    canViewUnseenTaskSignal: true,
  },
  {
    id: 'member-2',
    fullName: 'Project Member',
    email: 'member@qaly.dev',
    initials: 'PM',
    role: 'Member',
    canViewProjectTimeline: true,
    canViewTaskRisk: true,
    canNudgeAssignee: true,
    canViewUnseenTaskSignal: true,
  },
]

function mountMembers(isAdmin: boolean) {
  return mount(ProjectMembersTab, {
    props: {
      projectId: 'project-1',
      members,
      users: [],
      isAdmin,
    },
    global: {
      stubs: {
        ProjectRoleCapabilityCard: true,
        RoleHistoryModal: true,
        AssignRoleOverlapModal: true,
        AssignRoleSystemConflictModal: true,
        ProjectRoleManagerPanel: true,
      },
    },
  })
}

describe('ProjectMembersTab RBAC controls', () => {
  it('hides every membership mutation control from a read-only user', () => {
    const wrapper = mountMembers(false)

    expect(wrapper.get('.panel-heading').find('.primary-button').exists()).toBe(false)
    expect(wrapper.findAll('.role-selector')).toHaveLength(0)
    expect(wrapper.findAll('.member-remove-button')).toHaveLength(0)
  })

  it('lets a project manager edit ordinary members but never the Owner', () => {
    const wrapper = mountMembers(true)
    const rows = wrapper.findAll('.member-item')

    expect(rows).toHaveLength(2)
    expect(rows[0].text()).toContain('Project Owner')
    expect(rows[0].find('.role-selector').exists()).toBe(false)
    expect(rows[0].find('.member-remove-button').exists()).toBe(false)

    expect(rows[1].text()).toContain('Project Member')
    expect(rows[1].find('.role-selector').exists()).toBe(true)
    expect(rows[1].get('.member-remove-button').attributes('aria-label')).toBe(
      'Xóa Project Member khỏi dự án',
    )
  })
})
