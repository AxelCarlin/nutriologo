import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import RegistroComidaView from './RegistroComidaView.vue'

// El catálogo real: kcal por 100g y porción declarada (Huevo = 1 pza = 50g).
const CATALOGO = [
  { nombre: 'Huevo', kcalPorBase: 155, baseGramos: 100, baseUnidad: 'G',
    porcion: { descripcion: '1 pieza', cantidad: 1, gramos: 50, unidad: 'PIEZA' } },
  { nombre: 'Arroz', kcalPorBase: 130, baseGramos: 100, baseUnidad: 'G',
    porcion: { descripcion: '1 taza', cantidad: 160, gramos: 160, unidad: 'G' } },
]

function mockFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation((url: string) => {
      let body: unknown = {}
      if (url.startsWith('/api/alimentos')) body = CATALOGO
      else if (url.startsWith('/api/unidades'))
        body = [{ codigo: 'G' }, { codigo: 'PIEZA' }]
      else if (url.startsWith('/api/panel'))
        body = { caloriasConsumidas: 400, metaCalorica: null, tieneObjetivo: false }
      return Promise.resolve({ ok: true, json: async () => body, status: 200, text: async () => '' })
    }),
  )
}

describe('RegistroComidaView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
  })

  it('muestra kcal en vivo por fila y total', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // El catálogo carga y precarga el primer alimento.
    expect(w.findAll('.linea')).toHaveLength(1)
    // 100 g de Huevo = 155 kcal.
    expect(w.find('.linea__kcal').text()).toContain('155')

    // El total vive en dot-matrix.
    expect(w.find('.total__valor').text()).toContain('155')
  })

  it('recalcula al cambiar la cantidad con el stepper', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Paso fijo de 1: 100 → 99 g. 155 * 0.99 = 153.45 → 153.
    await w.findAll('.stepper__btn')[0]!.trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.linea__kcal').text()).toContain('153')
    expect(w.find('.stepper__valor').element).toHaveProperty('value', '99')
  })

  it('el combobox filtra y precarga la unidad de la porción', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    await w.find('.combobox__input').setValue('arroz')
    await w.vm.$nextTick()

    const opciones = w.findAll('.combobox__opcion')
    expect(opciones).toHaveLength(1)
    expect(opciones[0]!.text()).toContain('Arroz')

    await opciones[0]!.trigger('click')
    await w.vm.$nextTick()
    // Arroz viene porcionado en G.
    expect((w.find('.select').element as HTMLSelectElement).value).toBe('G')
  })

  it('deshabilita REGISTRAR si una fila no es válida', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Vaciar la cantidad deja la fila inválida.
    await w.find('.stepper__valor').setValue(0)
    await w.vm.$nextTick()
    expect(w.find('.cta').attributes('disabled')).toBeDefined()
    expect(w.text()).toContain('CANTIDAD DEBE SER MAYOR A 0')
  })

  it('eliminar la última fila repone una vacía', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    await w.find('.remove').trigger('click')
    await w.vm.$nextTick()
    expect(w.findAll('.linea')).toHaveLength(1)
  })
})