import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import Orb from './Orb.vue'
import ConversacionView from '../../features/conversation/ConversacionView.vue'

describe('Orb', () => {
  it('expone el estado en data-state', () => {
    const w = mount(Orb, { props: { state: 'thinking' } })
    expect(w.find('.orb').attributes('data-state')).toBe('thinking')
  })
})

describe('ConversacionView', () => {
  it('renderiza orbe, historial y composer', () => {
    const w = mount(ConversacionView)
    expect(w.find('.orb-stage').exists()).toBe(true)
    expect(w.find('.composer').exists()).toBe(true)
    expect(w.find('.chip-model').text()).toContain('Nutriólogo')
  })
})
