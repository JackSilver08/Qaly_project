import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import MemberProfessionalProfileDrawer from '../../src/Qaly.Web/ClientApp/components/MemberProfessionalProfileDrawer.vue'

const { apiResult, apiCommand } = vi.hoisted(() => ({
  apiResult: vi.fn(),
  apiCommand: vi.fn(),
}))

vi.mock('../../src/Qaly.Web/ClientApp/utils/api-client', () => ({
  apiResult,
  apiCommand,
  errorMessage: (_error:unknown, fallback:string) => fallback,
}))
vi.mock('../../src/Qaly.Web/ClientApp/composables/use-toast', () => ({
  showError: vi.fn(),
  showSuccess: vi.fn(),
}))

const definition = {
  id:'profile-1', organizationId:'org-1', key:'backend-engineer', name:'Backend Engineer',
  description:'Thiết kế API và domain logic.', category:'Engineering', isSystemSeed:true,
  isActive:true, rowVersion:'definition-version',
}

describe('MemberProfessionalProfileDrawer', () => {
  beforeEach(() => {
    apiResult.mockReset()
    apiCommand.mockReset()
  })

  it('separates access role from professional profile and locks manager-verified self records', async () => {
    apiResult
      .mockResolvedValueOnce([definition])
      .mockResolvedValueOnce({
        organizationId:'org-1', userId:'user-1', memberName:'Lan', accessRole:'Member',
        isSelf:true, canManage:false,
        authorizationNotice:'Hồ sơ nghề nghiệp hỗ trợ staffing; không cấp quyền truy cập hệ thống.',
        profiles:[{
          assignmentId:'assignment-1', definitionId:'profile-1', key:'backend-engineer',
          name:'Backend Engineer', category:'Engineering', description:definition.description,
          proficiency:'Proficient', verificationStatus:'Verified', source:'ManagerConfirmed',
          effectiveFrom:'2026-01-01T00:00:00Z', effectiveTo:null, verifiedByUserId:'manager-1',
          verifiedByName:'Manager', verifiedAt:'2026-01-02T00:00:00Z', note:null,
          rowVersion:'assignment-version', canEdit:false,
        }],
      })

    const wrapper = mount(MemberProfessionalProfileDrawer, {
      props:{ organizationId:'org-1', memberId:'user-1', memberName:'Lan' },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Vai trò truy cập hiện tại: Member')
    expect(wrapper.text()).toContain('không cấp quyền truy cập')
    expect(wrapper.text()).toContain('Thiết kế API và domain logic.')
    expect(wrapper.text()).toContain('Bản ghi đã được người quản lý xác minh')
    expect(wrapper.get('input[type="checkbox"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Đề cử theo bối cảnh')
    expect(wrapper.text()).toContain('Chọn tất cả')
  })
})
