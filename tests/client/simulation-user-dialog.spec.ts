import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import SimulationUserDialog from '@/components/SimulationUserDialog.vue'

const users = [
  { id: 'admin-1', fullName: 'Quản trị viên Qaly', role: 'Admin' },
  { id: 'member-1', fullName: 'Trần Bảo Ngọc', role: 'Member' },
  { id: 'manager-1', fullName: 'Nguyễn Minh Anh', role: 'Manager' },
]

afterEach(() => {
  document.body.innerHTML = ''
})

describe('SimulationUserDialog', () => {
  function mountDialog(open = true) {
    return mount(SimulationUserDialog, {
      attachTo: document.body,
      props: { open, users },
    })
  }

  it('stays out of the dashboard layout while closed', () => {
    mountDialog(false)

    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('shows non-admin accounts and explains the read-only boundary', () => {
    mountDialog()
    const dialog = document.querySelector<HTMLElement>('[role="dialog"]')

    expect(dialog?.textContent).toContain('Xem với vai trò người dùng')
    expect(dialog?.textContent).toContain('Chế độ chỉ đọc')
    expect(dialog?.textContent).toContain('Trần Bảo Ngọc')
    expect(dialog?.textContent).toContain('Nguyễn Minh Anh')
    expect(dialog?.textContent).not.toContain('Quản trị viên Qaly')
  })

  it('filters users and emits the selected account', async () => {
    const wrapper = mountDialog()
    const search = document.querySelector<HTMLInputElement>('input[type="search"]')!

    search.value = 'Minh Anh'
    search.dispatchEvent(new Event('input', { bubbles: true }))
    await wrapper.vm.$nextTick()

    const choices = Array.from(document.querySelectorAll<HTMLButtonElement>('.simulation-user'))
    expect(choices).toHaveLength(1)
    expect(choices[0].textContent).toContain('Nguyễn Minh Anh')

    choices[0].click()
    expect(wrapper.emitted('select')).toEqual([[users[2]]])
  })

  it('closes on Escape', async () => {
    const wrapper = mountDialog()
    const backdrop = document.querySelector<HTMLElement>('.simulation-dialog-backdrop')!

    backdrop.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
