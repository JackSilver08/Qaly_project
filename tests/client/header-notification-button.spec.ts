import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import HeaderNotificationButton from '@/components/HeaderNotificationButton.vue'

describe('HeaderNotificationButton', () => {
  it('renders a compact empty state without a count', () => {
    const wrapper = mount(HeaderNotificationButton, {
      props: { count: 0, open: false },
    })

    const button = wrapper.get('button')
    expect(button.attributes('aria-label')).toBe('Thông báo')
    expect(button.attributes('aria-expanded')).toBe('false')
    expect(wrapper.find('.header-notification-button__count').exists()).toBe(false)
  })

  it('keeps the count inside the button and emits click', async () => {
    const wrapper = mount(HeaderNotificationButton, {
      props: { count: 6, open: false },
    })

    expect(wrapper.get('.header-notification-button__count').text()).toBe('6')
    expect(wrapper.get('button').attributes('aria-label')).toBe('Thông báo, 6 mục cần chú ý')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('click')).toHaveLength(1)
  })

  it('caps large counts and exposes the open state', () => {
    const wrapper = mount(HeaderNotificationButton, {
      props: { count: 125, open: true },
    })

    const button = wrapper.get('button')
    expect(wrapper.get('.header-notification-button__count').text()).toBe('99+')
    expect(button.classes()).toContain('is-open')
    expect(button.attributes('aria-expanded')).toBe('true')
  })
})
