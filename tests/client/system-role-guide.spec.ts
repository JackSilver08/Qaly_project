import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import SystemRoleGuideModal from '@/components/SystemRoleGuideModal.vue'

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  document.body.innerHTML = ''
})

function render(role: string, pages = ['Tổng quan', 'Dự án', 'Nhiệm vụ']) {
  wrapper = mount(SystemRoleGuideModal, {
    attachTo: document.body,
    props: { open: true, role, userName: 'Người dùng demo', pages },
  })
  return wrapper
}

describe('system role guide', () => {
  it.each([
    ['Admin', 'Điều hành toàn hệ thống', 'Cấp phạm vi hỗ trợ có thời hạn cho Moderator.'],
    ['Moderator', 'Hỗ trợ theo phạm vi được giao', 'Hỗ trợ hồ sơ vai trò nghề nghiệp và bằng chứng kỹ năng.'],
    ['Member', 'Thực hiện công việc được giao', 'Gửi nhiệm vụ sang duyệt kèm minh chứng nghiệm thu.'],
  ])('renders a positive, role-specific guide for %s', (role, heading, action) => {
    render(role)
    const modal = document.body.querySelector('[data-testid="system-role-guide"]')
    expect(modal).not.toBeNull()
    expect(modal!.textContent).toContain(heading)
    expect(modal!.textContent).toContain(action)
    expect(modal!.textContent).toContain('Trợ lý AI hỗ trợ')
  })

  it('lists only pages supplied by the permission-filtered sidebar', () => {
    render('Member', ['Tổng quan', 'Nhiệm vụ'])
    const text = document.body.textContent || ''
    expect(text).toContain('Tổng quan')
    expect(text).toContain('Nhiệm vụ')
    expect(text).not.toContain('Quản lý người dùng')
    expect(text).not.toContain('Ủy quyền Moderator')
  })

  it('closes without exposing disabled actions', async () => {
    const mounted = render('Member')
    document.querySelector<HTMLButtonElement>('button[aria-label="Đóng hướng dẫn"]')!.click()
    await nextTick()
    expect(mounted.emitted('close')).toHaveLength(1)
    expect(document.body.textContent).not.toContain('Tạo Project')
  })

  it('opens the assistant with a useful role-safe prompt', async () => {
    const prompts: string[] = []
    const listener = (event: Event) => prompts.push(String((event as CustomEvent).detail?.prompt || ''))
    window.addEventListener('qaly:open-ai-assistant', listener)
    const mounted = render('Member')
    const button = [...document.body.querySelectorAll<HTMLButtonElement>('.role-guide-prompts button')]
      .find(item => item.textContent?.includes('Tóm tắt kiến thức'))!
    button.click()
    await nextTick()
    window.removeEventListener('qaly:open-ai-assistant', listener)

    expect(prompts).toHaveLength(1)
    expect(prompts[0]).toContain('tiêu chí nghiệm thu')
    expect(mounted.emitted('close')).toHaveLength(1)
  })
})
