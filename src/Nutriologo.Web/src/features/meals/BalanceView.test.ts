import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createRouter, createWebHistory } from 'vue-router'
import BalanceView from './BalanceView.vue'

const PANEL = {
  fecha: '2026-10-05T00:00:00',
  caloriasConsumidas: 1179.1,
  macros: { PROTEINA: 68.4, GRASA: 42.1, CARBOHIDRATO: 130.7 },
  comidas: [
    {
      id: 1,
      nombre: 'Desayuno',
      caloriasEstimadas: 420,
      tipo: 'DESAYUNO',
      hora: '2026-10-05T08:30:00',
      items: [
        { alimento: 'Huevo', cantidad: 100, unidad: 'G', gramos: 100, kcal: 155 },
        { alimento: 'Tortilla', cantidad: 2, unidad: 'PIEZA', gramos: 60, kcal: 131 },
      ],
    },
    {
      id: 2,
      nombre: 'Comida',
      caloriasEstimadas: 759.1,
      tipo: 'COMIDA',
      hora: '2026-10-05T13:20:00',
      items: [],
    },
  ],
}

const VACIO = { fecha: '2026-10-03T00:00:00', caloriasConsumidas: 0, macros: {}, comidas: [] }

function mockFetch(panel: unknown = PANEL) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => panel,
      text: async () => '',
    }),
  )
}

async function montar() {
  const router = createRouter({
    history: createWebHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/registrar', component: { template: '<div />' } },
    ],
  })
  await router.push('/')
  await router.isReady()
  const w = mount(BalanceView, { global: { plugins: [router] } })
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
  await w.vm.$nextTick()
  return w
}

describe('BalanceView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
  })

  it('título dot-matrix del sistema, no h2 Grotesk', async () => {
    const w = await montar()
    expect(w.find('h1.titulo').text()).toBe('balance')
  })

  it('héroe redondeado y formateado igual que Inicio', async () => {
    const w = await montar()
    // 1179.1 crudo del endpoint → 1,179. Mismo redondeo que Inicio.
    expect(w.find('.hero__valor').text()).toBe('1,179')
    expect(w.find('.hero__valor').text()).not.toContain('.')
  })

  it('la tarjeta indica la fecha del dato en formato explícito', async () => {
    const w = await montar()
    // DD MMM AAAA, nunca el ambiguo 10/05/2026 del input nativo.
    expect(w.find('.hero').text()).toMatch(/CALORÍAS · \d{2} [A-Z]{3} \d{4}/)
    expect(w.find('.hero').text()).not.toMatch(/\d{2}\/\d{2}\/\d{4}/)
  })

  it('navegación por stepping, sin popover de calendario (§9)', async () => {
    const w = await montar()
    expect(w.findAll('input[type=date]')).toHaveLength(0)
    expect(w.find('[aria-label="Día anterior"]').exists()).toBe(true)
    expect(w.find('[aria-label="Día siguiente"]').exists()).toBe(true)
  })

  it('el día siguiente se deshabilita en hoy', async () => {
    const w = await montar()
    expect(w.find('[aria-label="Día siguiente"]').attributes('disabled')).toBeDefined()
  })

  it('desglose por comida: el número deja de ser opaco', async () => {
    const w = await montar()
    const filas = w.findAll('.rows__item')
    expect(filas).toHaveLength(2)
    expect(filas[0]!.text()).toContain('Desayuno')
    expect(filas[0]!.text()).toContain('420')
    // El mock trae wall-clock UTC sin offset: 08:30Z = 02:30 en CST-0600.
    expect(filas[0]!.text()).toContain('02:30')
  })

  it('macros del día desde /panel, ordenados por gramos', async () => {
    const w = await montar()
    // 130.7 / 68.4 / 42.1 → CARBOHIDRATO, PROTEÍNA, GRASA.
    const etiquetas = w.findAll('.macros__label').map((l) => l.text())
    expect(etiquetas).toEqual(['CARBOHIDRATO', 'PROTEÍNA', 'GRASA'])
    expect(w.find('.macros__value').text()).toBe('131')
  })

  it('día vacío: estado explícito con enlace a registrar', async () => {
    mockFetch(VACIO)
    const w = await montar()
    expect(w.find('.hero__valor').text()).toBe('0')
    expect(w.text()).toContain('Sin registros este día')
    expect(w.find('a[href="/registrar"]').exists()).toBe(true)
  })

  it('desglose expandible: qué contenía cada comida', async () => {
    const w = await montar()
    // Cerrado por defecto: progressive disclosure, no ruido.
    expect(w.find('.subrows').exists()).toBe(false)

    await w.find('.rows__item--btn').trigger('click')
    await w.vm.$nextTick()

    const items = w.findAll('.subrows__item:not(.subrows__accion)')
    expect(items).toHaveLength(2)
    expect(items[0]!.text()).toContain('Huevo')
    expect(items[0]!.text()).toContain('100 g')
    expect(items[0]!.text()).toContain('155')
    expect(items[1]!.text()).toContain('Tortilla')
    expect(items[1]!.text()).toContain('2 pieza')

    // Segundo click cierra.
    await w.find('.rows__item--btn').trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.subrows').exists()).toBe(false)
  })

  it('comida sin items muestra estado explícito, no vacío', async () => {
    const w = await montar()
    const botones = w.findAll('.rows__item--btn')
    await botones[1]!.trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.subrows').text()).toContain('Sin detalle de alimentos')
  })

  it('marca con punto rojo la comida fuera de rango (>3000 kcal)', async () => {
    mockFetch({
      ...PANEL,
      caloriasConsumidas: 7750,
      comidas: [
        {
          id: 59,
          nombre: 'Desayuno',
          caloriasEstimadas: 7750,
          tipo: null,
          hora: '2026-10-04T23:09:17',
          items: [{ alimento: 'Huevo', cantidad: 100, unidad: 'PIEZA', gramos: 5000, kcal: 7750 }],
        },
      ],
    })
    const w = await montar()
    const alerta = w.find('.rows__alerta')
    expect(alerta.exists()).toBe(true)
    expect(alerta.attributes('aria-label')).toContain('revisar')
  })

  it('eliminar pide confirmación y llama DELETE', async () => {
    const fetch = vi.fn().mockImplementation((_url: string, init?: RequestInit) => {
      if (init?.method === 'DELETE') {
        return Promise.resolve({ ok: true, status: 204, json: async () => null, text: async () => '' })
      }
      return Promise.resolve({ ok: true, status: 200, json: async () => PANEL, text: async () => '' })
    })
    vi.stubGlobal('fetch', fetch)
    const w = await montar()

    // Paso 1: expandir y pedir eliminar.
    await w.find('.rows__item--btn').trigger('click')
    await w.vm.$nextTick()
    await w.find('.remove').trigger('click')
    await w.vm.$nextTick()
    // Paso 2: todavía no se llamó a DELETE, solo se muestra la confirmación.
    expect(fetch.mock.calls.some((c) => (c[1] as RequestInit)?.method === 'DELETE')).toBe(false)
    expect(w.text()).toContain('¿ELIMINAR ESTA COMIDA?')

    // Paso 3: confirmar.
    await w.find('.remove--danger').trigger('click')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()
    const del = fetch.mock.calls.find((c) => (c[1] as RequestInit)?.method === 'DELETE')
    expect(del?.[0]).toContain('/api/comidas/1')
    expect(w.find('.aviso').text()).toContain('ELIMINADA')
  })

  it('cancelar no llama a DELETE', async () => {
    const w = await montar()
    await w.find('.rows__item--btn').trigger('click')
    await w.vm.$nextTick()
    await w.find('.remove').trigger('click')
    await w.vm.$nextTick()
    const cancelar = w.findAll('.remove').find((b) => b.text().includes('CANCELAR'))!
    await cancelar.trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.remove--danger').exists()).toBe(false)
  })

  it('cambiar de día cierra la fila abierta', async () => {
    const w = await montar()
    await w.find('.rows__item--btn').trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.subrows').exists()).toBe(true)

    await w.find('[aria-label="Día anterior"]').trigger('click')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()
    expect(w.find('.subrows').exists()).toBe(false)
  })

  it('retroceder un día recarga con la fecha desplazada', async () => {
    const fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => VACIO,
      text: async () => '',
    })
    vi.stubGlobal('fetch', fetch)
    const w = await montar()
    await w.find('[aria-label="Día anterior"]').trigger('click')
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()
    const urls = fetch.mock.calls.map((c) => String(c[0]))
    expect(urls.some((u) => u.includes('/panel?fecha='))).toBe(true)
    expect(urls.length).toBeGreaterThan(1)
  })
})