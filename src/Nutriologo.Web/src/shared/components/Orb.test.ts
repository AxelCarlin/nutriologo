import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import Orb from './Orb.vue'
import ConversacionView from '../../features/conversation/ConversacionView.vue'

describe('Orb', () => {
  it('expone el estado en data-state', () => {
    const w = mount(Orb, { props: { state: 'thinking' } })
    expect(w.find('.orb').attributes('data-state')).toBe('thinking')
  })
})

describe('ConversacionView', () => {
  it('estado vacío: orbe hero, título y sugerencias, sin wordmark duplicado', () => {
    const w = mount(ConversacionView)

    // Estado vacío: el orbe va en variante hero (centrado), no avatar.
    expect(w.find('.orb-stage--hero').exists()).toBe(true)
    expect(w.find('.chat-screen--vacio').exists()).toBe(true)
    expect(w.find('.vacio__titulo').text()).toBe('¿qué comiste hoy?')
    // 3 sugerencias como pills.
    expect(w.findAll('.chip')).toHaveLength(3)

    // El topbar muestra el ESTADO del agente, no repite "nutriólogo"
    // (el wordmark ya vive en el sidebar).
    expect(w.find('.topbar .orb-state').exists()).toBe(true)
    expect(w.text()).not.toContain('LLM · LOCAL')

    expect(w.find('.composer').exists()).toBe(true)
  })

  it('conversa: orbe avatar de 56px y etiqueta mono en el mensaje del agente', async () => {
    // El LLM no existe en tests: se mockea la respuesta del endpoint.
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ tipo: 'respuesta', texto: 'Anotado: dos huevos.' }),
      }),
    )
    localStorage.setItem('nutriologo_token', 'tok')

    const w = mount(ConversacionView)

    await w.find('input').setValue('Comí dos huevos')
    await w.find('form.composer').trigger('submit.prevent')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Con mensajes el orbe baja a avatar y aparece la etiqueta NUTRIÓLOGO.
    expect(w.find('.orb-stage--avatar').exists()).toBe(true)
    expect(w.find('.msg--user').exists()).toBe(true)
    expect(w.find('.msg__meta').text()).toBe('NUTRIÓLOGO')
    expect(w.text()).toContain('Anotado: dos huevos.')

    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('aclaracion del backend NO se trata como error de conexión', async () => {
    // El backend emite tipo="aclaracion". El front comparaba contra "pregunta"
    // (que nunca emite) -> caía al else -> "Respuesta inesperada" + "SIN CONEXIÓN".
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          tipo: 'aclaracion',
          pregunta: '¿Qué te gustaría registrar o consultar?',
          motivo: 'intención desconocida',
        }),
      }),
    )
    localStorage.setItem('nutriologo_token', 'tok')

    const w = mount(ConversacionView)

    await w.find('input').setValue('hola')
    await w.find('form.composer').trigger('submit.prevent')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    expect(w.text()).toContain('¿Qué te gustaría registrar o consultar?')
    expect(w.text()).not.toContain('Respuesta inesperada')
    // El LLM respondió bien: no puede marcarse caído.
    expect(w.find('.llm--down').exists()).toBe(false)
    expect(w.find('.msg-status--error').exists()).toBe(false)

    vi.unstubAllGlobals()
    localStorage.clear()
  })
})
