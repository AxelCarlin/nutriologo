import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import MedicionesView from './MedicionesView.vue'

const TIPOS = [
  { id: 1, codigo: 'PESO', nombre: 'Peso', unidad: 'g', min: 20, max: 400 },
  { id: 2, codigo: 'CINTURA', nombre: 'Cintura', unidad: 'cm', min: 30, max: 250 },
]

const MEDICIONES = [
  { id: 3, idTipoMedicionCorporal: 1, valor: 92.0, fechaMedicion: '2026-10-04T09:00:00', observacion: null },
  { id: 2, idTipoMedicionCorporal: 1, valor: 92.4, fechaMedicion: '2026-09-27T09:00:00', observacion: null },
  { id: 1, idTipoMedicionCorporal: 1, valor: 93.1, fechaMedicion: '2026-09-20T09:00:00', observacion: null },
]

function mockFetch(mediciones = MEDICIONES) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation((url: string) => {
      let body: unknown = []
      if (url.includes('/api/catalogos')) body = { sexos: [], actividad: [], tiposMedicion: TIPOS }
      else if (url.includes('/mediciones')) body = mediciones
      return Promise.resolve({ ok: true, status: 200, json: async () => body, text: async () => '' })
    }),
  )
}

async function montar() {
  const w = mount(MedicionesView, { global: { plugins: [createPinia()] } })
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
  return w
}

describe('MedicionesView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
  })

  it('tipo como chips con nombre, nunca ID', async () => {
    const w = await montar()
    const chips = w.findAll('.chips').at(0)!.findAll('.chip')
    expect(chips).toHaveLength(2)
    expect(chips[0]!.text()).toBe('Peso')
    expect(chips[1]!.text()).toBe('Cintura')
    // El texto "ID" no debe aparecer en ninguna parte.
    expect(w.text()).not.toContain('TIPO (ID)')
    expect(w.text()).not.toContain('IdTipo')
  })

  it('héroe: última medición en dot-matrix con delta', async () => {
    const w = await montar()
    expect(w.find('.heroe__valor').text()).toBe('92.0')
    // 92.0 - 92.4 = -0.4
    expect(w.find('.heroe__delta').text()).toContain('0.4')
  })

  it('sparkline con puntos y último en rojo', async () => {
    const w = await montar()
    const puntos = w.findAll('.spark__punto')
    expect(puntos).toHaveLength(3)
    expect(puntos.at(-1)!.classes()).toContain('spark__punto--ultimo')
    expect(w.find('.spark__line').attributes('points')).toBeTruthy()
  })

  it('historial con filas de altura fija y delta por fila', async () => {
    const w = await montar()
    const filas = w.findAll('.historial__fila')
    expect(filas).toHaveLength(3)
    expect(filas[0]!.text()).toContain('92.0')
    expect(w.find('.historial__delta').text()).toContain('0.4')
  })

  it('unidad dinámica según el tipo', async () => {
    const w = await montar()
    expect(w.find('.unidad').text()).toBe('g')
    // Cambiar a Cintura debe recargar y cambiar la unidad.
    await w.findAll('.chips').at(0)!.findAll('.chip')[1]!.trigger('click')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()
    expect(w.find('.unidad').text()).toBe('cm')
  })

  it('bloquea REGISTRAR fuera de rango y muestra el error en rojo', async () => {
    const w = await montar()
    await w.find('#m-valor').setValue(920)
    await w.vm.$nextTick()
    expect(w.find('.cta').attributes('disabled')).toBeDefined()
    expect(w.find('.input--error').exists()).toBe(true)
  })

  it('estado vacío explícito cuando no hay mediciones', async () => {
    mockFetch([])
    const w = await montar()
    expect(w.text()).toContain('SIN MEDICIONES')
    expect(w.text()).toContain('Registra la primera')
    expect(w.find('.historial').exists()).toBe(false)
    expect(w.find('.heroe').exists()).toBe(false)
  })
})