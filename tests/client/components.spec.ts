import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PageStatePanel from '@/components/PageStatePanel.vue'
import TaskItem from '@/components/TaskItem.vue'
import ToastContainer from '@/components/ToastContainer.vue'
import type { TaskListItemModel } from '@/components/dashboard-models'
import { dismissToast, showError, showSuccess, useToast } from '@/composables/use-toast'

/** The toast queue is a module singleton shared with the app, so drain it between cases. */
function clearToasts() {
  const { toasts } = useToast()
  toasts.value.map((toast) => toast.id).forEach(dismissToast)
}

afterEach(() => {
  clearToasts()
  document.body.innerHTML = ''
  vi.useRealTimers()
})

describe('PageStatePanel', () => {
  const base = { title: 'Chưa có dự án', message: 'Hãy tạo dự án đầu tiên của bạn.' }

  it('renders the title and message for the default empty variant', () => {
    const wrapper = mount(PageStatePanel, { props: base })

    expect(wrapper.find('h2').text()).toBe('Chưa có dự án')
    expect(wrapper.find('p').text()).toBe('Hãy tạo dự án đầu tiên của bạn.')
    expect(wrapper.classes()).toContain('page-state-panel--empty')
    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-busy')).toBeUndefined()
  })

  it('announces the error variant to assistive tech with role="alert"', () => {
    const wrapper = mount(PageStatePanel, { props: { ...base, variant: 'error' } })

    expect(wrapper.attributes('role')).toBe('alert')
    expect(wrapper.classes()).toContain('page-state-panel--error')
  })

  it('swaps the copy for a skeleton and marks the region busy while loading', () => {
    const wrapper = mount(PageStatePanel, { props: { ...base, variant: 'loading' } })

    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.findAll('.page-state-panel__skeleton')).toHaveLength(4)
    expect(wrapper.find('.page-state-panel__loading').attributes('aria-hidden')).toBe('true')
  })

  it('honours a custom skeleton row count', () => {
    const wrapper = mount(PageStatePanel, {
      props: { ...base, variant: 'loading', skeletonRows: 7 },
    })

    expect(wrapper.findAll('.page-state-panel__skeleton')).toHaveLength(7)
  })

  it('only renders the icon and action wrappers when those slots are filled', () => {
    const bare = mount(PageStatePanel, { props: base })
    expect(bare.find('.page-state-panel__icon').exists()).toBe(false)
    expect(bare.find('.page-state-panel__actions').exists()).toBe(false)

    const filled = mount(PageStatePanel, {
      props: base,
      slots: { icon: '<i class="test-icon" />', actions: '<button>Tạo dự án</button>' },
    })
    expect(filled.find('.page-state-panel__icon .test-icon').exists()).toBe(true)
    expect(filled.find('.page-state-panel__actions button').text()).toBe('Tạo dự án')
  })

  it('hides slot content while loading so the skeleton is the only state shown', () => {
    const wrapper = mount(PageStatePanel, {
      props: { ...base, variant: 'loading' },
      slots: { actions: '<button>Thử lại</button>' },
    })

    expect(wrapper.find('.page-state-panel__actions').exists()).toBe(false)
  })
})

describe('TaskItem', () => {
  const task: TaskListItemModel = {
    id: 'task-1',
    projectId: 'project-9',
    title: 'Hoàn thiện API thanh toán',
    projectName: 'Qaly Release 4.0',
    assignedAtLabel: '14 thg 3',
    priority: 'High',
    dueDateLabel: '20 thg 3',
    reporterName: 'Bảo Ngọc',
    reporterInitials: 'BN',
    statusLabel: 'Đang làm',
    isOverdue: false,
  }

  it('renders the task summary fields', () => {
    const wrapper = mount(TaskItem, { props: { task } })
    const text = wrapper.text()

    expect(text).toContain('Hoàn thiện API thanh toán')
    expect(text).toContain('Qaly Release 4.0')
    expect(text).toContain('Bảo Ngọc')
    expect(text).toContain('Đang làm')
    expect(text).toContain('Deadline 20 thg 3')
  })

  it('lowercases the priority into its modifier class', () => {
    const wrapper = mount(TaskItem, { props: { task } })
    expect(wrapper.find('.priority').classes()).toContain('priority--high')
  })

  it('marks the deadline as risky only when the task is overdue', () => {
    const onTime = mount(TaskItem, { props: { task } })
    expect(onTime.find('.project-risk').exists()).toBe(false)

    const late = mount(TaskItem, { props: { task: { ...task, isOverdue: true } } })
    expect(late.find('.project-risk').text()).toContain('Deadline')
  })

  it('emits view with the project and task ids on click', async () => {
    const wrapper = mount(TaskItem, { props: { task } })

    await wrapper.find('.task-list-item').trigger('click')

    expect(wrapper.emitted('view')).toEqual([['project-9', 'task-1']])
  })

  it('is reachable and activatable from the keyboard', async () => {
    // QALY-UI-02: the row is a clickable <article>, so it needs an explicit button role,
    // a tab stop and Enter/Space handlers to stay usable without a mouse.
    const wrapper = mount(TaskItem, { props: { task } })
    const row = wrapper.find('.task-list-item')

    expect(row.attributes('role')).toBe('button')
    expect(row.attributes('tabindex')).toBe('0')
    expect(row.attributes('aria-label')).toContain('Hoàn thiện API thanh toán')

    await row.trigger('keydown.enter')
    await row.trigger('keydown.space')

    expect(wrapper.emitted('view')).toHaveLength(2)
  })
})

describe('ToastContainer', () => {
  function mountToasts() {
    return mount(ToastContainer, { attachTo: document.body })
  }

  it('renders nothing while the queue is empty', () => {
    mountToasts()
    expect(document.querySelectorAll('.toast-card')).toHaveLength(0)
  })

  it('renders a queued toast with its type class and default title', async () => {
    const wrapper = mountToasts()
    showSuccess('Đã lưu dự án')
    await wrapper.vm.$nextTick()

    const card = document.querySelector('.toast-card')
    expect(card).not.toBeNull()
    expect(card?.classList.contains('toast-card--success')).toBe(true)
    expect(card?.querySelector('strong')?.textContent).toBe('Thành công')
    expect(card?.querySelector('p')?.textContent).toBe('Đã lưu dự án')
  })

  it('prefers an explicit title over the type default', async () => {
    const wrapper = mountToasts()
    showError('Máy chủ không phản hồi', { title: 'Lỗi mạng' })
    await wrapper.vm.$nextTick()

    expect(document.querySelector('.toast-card strong')?.textContent).toBe('Lỗi mạng')
  })

  it('gives error toasts role="alert" and other toasts role="status"', async () => {
    const wrapper = mountToasts()
    showError('Thất bại')
    await wrapper.vm.$nextTick()
    expect(document.querySelector('.toast-card')?.getAttribute('role')).toBe('alert')

    showSuccess('Thành công')
    await wrapper.vm.$nextTick()
    expect(document.querySelector('.toast-card')?.getAttribute('role')).toBe('status')
  })

  it('removes the toast when the close button is pressed', async () => {
    const wrapper = mountToasts()
    showSuccess('Đã lưu dự án')
    await wrapper.vm.$nextTick()

    const close = document.querySelector<HTMLButtonElement>('.toast-card__close')
    expect(close?.getAttribute('aria-label')).toBe('Đóng thông báo')
    close?.click()
    await wrapper.vm.$nextTick()

    expect(document.querySelectorAll('.toast-card')).toHaveLength(0)
  })

  it('drives the progress bar from the toast duration', async () => {
    const wrapper = mountToasts()
    showSuccess('Đã lưu dự án', { duration: 7000 })
    await wrapper.vm.$nextTick()

    const progress = document.querySelector<HTMLElement>('.toast-card__progress')
    expect(progress?.style.animationDuration).toBe('7000ms')
  })
})
