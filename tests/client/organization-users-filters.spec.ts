import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import OrganizationUsersPage from '@/pages/OrganizationUsersPage.vue'

const mocks = vi.hoisted(() => ({ apiResult: vi.fn(), apiCommand: vi.fn(), showError: vi.fn() }))
vi.mock('vue-router', () => ({ useRoute: () => ({ query: {} }) }))
vi.mock('@/utils/api-client', () => ({
  apiResult: mocks.apiResult, apiCommand: mocks.apiCommand,
  errorMessage: (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback,
}))
vi.mock('@/composables/use-toast', () => ({ showError: mocks.showError, showSuccess: vi.fn() }))
vi.mock('@/composables/use-confirm-dialog', () => ({ confirmDialog: vi.fn() }))

const member = (userId: string, fullName: string, role: string, joinedAt: string, extras: Record<string, unknown> = {}) => ({
  userId, fullName, role, joinedAt, email: `${userId}@qaly.test`,
  projects: null, skills: null, weeklyCapacityHours: null, capacityState: null,
  ...extras,
})
const initialMembers = () => [
  member('owner', 'Chủ sở hữu', 'Owner', '2026-01-01T12:00:00Z'),
  member('admin', 'Quản trị A', 'Admin', '2026-09-03T12:00:00Z'),
  member('manager', 'Quản trị B', 'Manager', '2026-08-20T12:00:00Z'),
  member('developer', 'Đặng Gia Khang', 'Member', '2026-09-02T12:00:00Z'),
  member('older', 'Đặng Văn Cũ', 'Member', '2026-08-01T12:00:00Z'),
  member('privacy', 'Phụ trách dữ liệu', 'PrivacyOperator', '2026-09-01T12:00:00Z'),
  member('billing', 'Phụ trách hóa đơn', 'BillingAdmin', '2026-07-01T12:00:00Z'),
]
const nextMembers = () => [member('other', 'Người tổ chức khác', 'Member', '2026-09-01T12:00:00Z')]
let wrapper: VueWrapper
let actor = { id: 'owner', role: 'User' }
let loadUsers: (id: string) => Promise<ReturnType<typeof initialMembers>>

beforeEach(() => {
  vi.clearAllMocks()
  vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-09-04T12:00:00Z'))
  actor = { id: 'owner', role: 'User' }
  loadUsers = async id => id === 'org-a' ? initialMembers() : nextMembers()
  mocks.apiResult.mockImplementation(async (url: string) => {
    if (url === '/api/auth/me') return actor
    if (url.startsWith('/api/organizations?')) return { items: [
      { id: 'org-a', name: 'Tổ chức A', code: 'A', ownerId: 'owner', isActive: true, memberCount: 7, projectCount: 2 },
      { id: 'org-b', name: 'Tổ chức B', code: 'B', ownerId: 'someone-else', isActive: true, memberCount: 1, projectCount: 1 },
    ], totalCount: 2 }
    if (url.endsWith('/moderator-capabilities')) return []
    const match = url.match(/^\/api\/organizations\/([^/]+)\/users$/)
    if (match) return loadUsers(match[1])
    throw new Error(`Unexpected request: ${url}`)
  })
})

afterEach(() => wrapper?.unmount())

async function render() {
  wrapper = mount(OrganizationUsersPage, { global: { stubs: {
    MemberSkillEvidenceDrawer: true,
    MemberProfessionalProfileDrawer: true,
    RouterLink: { props: ['to'], template: '<a><slot /></a>' },
  } } })
  await flushPromises()
  return wrapper
}
const roleSelect = () => wrapper.get('select[aria-label="Lọc theo vai trò tổ chức"]')
const dateSelect = () => wrapper.get('select[aria-label="Lọc theo ngày tham gia"]')
const searchInput = () => wrapper.get('input[type="search"]')
const organizationSelect = () => wrapper.get('select[aria-label="Chọn tổ chức"]')
const rows = () => wrapper.findAll('tbody tr')

describe('organization member filters', () => {
  it('shows all members, role counts and treats legacy Admin/Manager as organization admins', async () => {
    await render()
    expect(rows()).toHaveLength(7)
    expect(wrapper.get('.filter-summary').text()).toContain('Hiển thị 7 / 7 thành viên')
    expect(roleSelect().text()).toContain('Quản trị tổ chức (2)')
    await roleSelect().setValue('OrganizationAdmin')
    expect(rows()).toHaveLength(2)
    expect(rows().map(row => row.text()).join(' ')).toContain('Quản trị A')
    expect(rows().map(row => row.text()).join(' ')).toContain('Quản trị B')
    expect((rows()[0].get('select').element as HTMLSelectElement).value).toBe('OrganizationAdmin')
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })

  it('combines role, recent membership and accent-insensitive name/email search without a new request', async () => {
    await render()
    const requests = mocks.apiResult.mock.calls.length
    await roleSelect().setValue('Member')
    await searchInput().setValue('  DANG  ')
    expect(rows()).toHaveLength(2)
    await dateSelect().setValue('7')
    expect(rows()).toHaveLength(1)
    expect(rows()[0].text()).toContain('Đặng Gia Khang')
    await searchInput().setValue('DEVELOPER@QALY.TEST')
    expect(rows()).toHaveLength(1)
    expect(mocks.apiResult).toHaveBeenCalledTimes(requests)
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })

  it('explains zero matches and clears all filters without changing organization', async () => {
    await render()
    await roleSelect().setValue('Owner')
    await dateSelect().setValue('7')
    await searchInput().setValue('khong-co')
    expect(rows()).toHaveLength(0)
    expect(wrapper.text()).toContain('Thử thay đổi từ khóa hoặc xóa bộ lọc hiện tại.')
    expect(wrapper.text()).not.toContain('Tổ chức này chưa có thành viên nào.')
    await wrapper.get('.clear-filters').trigger('click')
    expect(rows()).toHaveLength(7)
    expect((organizationSelect().element as HTMLSelectElement).value).toBe('org-a')
    expect((roleSelect().element as HTMLSelectElement).value).toBe('')
    expect((dateSelect().element as HTMLSelectElement).value).toBe('')
    expect((searchInput().element as HTMLInputElement).value).toBe('')
  })

  it('clears filters when changing organization and ignores a late response from the old organization', async () => {
    await render()
    await roleSelect().setValue('Owner')
    await dateSelect().setValue('30')
    let resolveOld!: (data: ReturnType<typeof initialMembers>) => void
    loadUsers = id => id === 'org-a'
      ? new Promise(resolve => { resolveOld = resolve })
      : Promise.resolve(nextMembers())
    await wrapper.get('button[aria-label="Tải lại danh sách thành viên"]').trigger('click')
    await organizationSelect().setValue('org-b')
    await flushPromises()
    expect((roleSelect().element as HTMLSelectElement).value).toBe('')
    expect((dateSelect().element as HTMLSelectElement).value).toBe('')
    expect(rows()).toHaveLength(1)
    resolveOld(initialMembers())
    await flushPromises()
    expect(rows()[0].text()).toContain('Người tổ chức khác')
    expect(wrapper.get('.filter-summary').text()).toContain('1 / 1')
  })

  it('preserves filters on refresh and distinguishes request failure from an empty result', async () => {
    await render()
    await roleSelect().setValue('PrivacyOperator')
    loadUsers = async () => { throw new Error('Mất kết nối máy chủ') }
    await wrapper.get('button[aria-label="Tải lại danh sách thành viên"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Mất kết nối máy chủ')
    expect(wrapper.text()).not.toContain('Không tìm thấy thành viên')
    expect((roleSelect().element as HTMLSelectElement).value).toBe('PrivacyOperator')
    loadUsers = async () => initialMembers()
    await wrapper.get('[role="alert"] button').trigger('click')
    await flushPromises()
    expect(rows()).toHaveLength(1)
    expect(rows()[0].text()).toContain('Phụ trách dữ liệu')
  })

  it('does not change effective permissions when the actor is filtered out', async () => {
    await render()
    await roleSelect().setValue('Member')
    expect(wrapper.find('button[aria-label="Thêm thành viên tổ chức"]').exists()).toBe(true)
    expect(rows()[0].find('select').exists()).toBe(true)
    wrapper.unmount()
    actor = { id: 'developer', role: 'User' }
    await render()
    await roleSelect().setValue('OrganizationAdmin')
    expect(rows()).toHaveLength(2)
    expect(wrapper.find('button[aria-label="Thêm thành viên tổ chức"]').exists()).toBe(false)
    expect(rows()[0].find('select').exists()).toBe(false)
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })

  it('filters date boundaries and omits invalid/future dates only when a date filter is active', async () => {
    loadUsers = async () => [
      member('boundary', 'Đúng bảy ngày', 'Member', '2026-08-28T12:00:00Z'),
      member('before', 'Ngoài bảy ngày', 'Member', '2026-08-28T11:59:59Z'),
      member('invalid', 'Chưa có ngày', 'Member', ''),
      member('future', 'Tương lai', 'Member', '2026-09-05T12:00:00Z'),
    ]
    await render()
    expect(rows()).toHaveLength(4)
    await dateSelect().setValue('7')
    expect(rows()).toHaveLength(1)
    expect(rows()[0].text()).toContain('Đúng bảy ngày')
  })

  it('keeps dense project and skill data compact with accessible disclosures', async () => {
    loadUsers = async () => [member('dense', 'Thành viên nhiều dữ liệu', 'Member', '2026-09-01T12:00:00Z', {
      projects: [
        { projectId: 'p1', projectName: 'Project Một', projectCode: 'P1', role: 'Member' },
        { projectId: 'p2', projectName: 'Project Hai', projectCode: 'P2', role: 'Developer' },
        { projectId: 'p3', projectName: 'Project Ba', projectCode: 'P3', role: 'Reviewer' },
      ],
      skills: ['Vue', 'Playwright', 'Accessibility'],
      weeklyCapacityHours: 32,
    })]
    await render()
    const row = rows()[0]
    expect(row.get('summary[aria-label*="Project"]').text()).toContain('3dự án')
    expect(row.findAll('.project-memberships .compact-popover a')).toHaveLength(3)
    expect(row.get('.project-memberships .compact-popover').text()).toContain('Project Một')
    expect(row.get('summary[aria-label*="vai trò"]').text()).toContain('3vai trò')
    expect(row.findAll('.project-roles .compact-popover > span')).toHaveLength(3)
    expect(row.get('.project-roles .compact-popover').text()).toContain('Thành viên')
    expect(row.get('summary[aria-label*="kỹ năng"]').text()).toContain('3kỹ năng')
    expect(row.findAll('.people-signals .compact-popover span')).toHaveLength(3)
    expect(row.text()).toContain('32h/tuần')
  })
})
