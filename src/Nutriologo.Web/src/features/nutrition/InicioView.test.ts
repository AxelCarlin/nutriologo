import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createRouter, createWebHistory } from 'vue-router'
import Inicio from './Inicio.vue'

// El backend emite wall-clock UTC sin offset ("2026-10-05T01:33:29").
// El contenedor de test corre en CST-0600, igual que un teléfono en México:
// 01:33Z son las 19:33 locales del día anterior.
const PANEL = {
  fecha: '2026-10-05T00:00:00Z',
  caloriasConsumidas: 1179.1,
  metaCalorica: null,
  tieneObjetivo: false,
  tienePerfil: true,
  peso: null,
  fechaPeso: null,
  macros: { PROTEINA: 14.2, GRASA: 101.3, CARBOHIDRATO: 58.1 },
  comidas: [
    { id: 127, nombre: 'Desayuno', caloriasEstimadas: 295.1, tipo: null, hora: '2026-10-05T01:33:25' },
    { id: 128, nombre: 'Desayuno', caloriasEstimadas: 884, tipo: null, hora: '2026-10-05T01:33:29' },
  ],
  serie: [
    { dia: '2026-09-29T00:00:00Z', kcal: 0 },
    { dia: '2026-09-30T00:00:00Z', kcal: 0 },
    { dia: '2026-10-01T00:00:00Z', kcal: 0 },
    { dia: '2026-10-02T00:00:00Z', kcal: 0 },
    { dia: '2026-10-03T00:00:00Z', kcal: 0 },
    { dia: '2026-10-04T00:00:00Z', kcal: 0 },
    { dia: '2026-10-05T00:00:00Z', kcal: 1179.1 },
  ],
}

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
      { path: '/objetivo', component: { template: '<div />' } },
    ],
  })
  await router.push('/')
  await router.isReady()
  const w = mount(Inicio, { global: { plugins: [router] } })
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
  await w.vm.$nextTick()
  return w
}

describe('InicioView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
  })

  it('héroe redondeado: 1179.1 → 1,179, sin decimal colgando', async () => {
    const w = await montar()
    expect(w.find('.hero__value').text()).toBe('1,179')
  })

  it('comidas con kcal enteras: 295.1 → 295, 884 → 884', async () => {
    const w = await montar()
    const valores = w.findAll('.rows__valor').map((v) => v.text())
    expect(valores).toEqual(['295', '884'])
  })

  it('hora en hora local, no UTC crudo', async () => {
    const w = await montar()
    // 01:33Z = 19:33 en CST-0600. Sin la Z se mostraba 01:33.
    const horas = w.findAll('.rows__hora').map((h) => h.text())
    expect(horas).toEqual(['19:33', '19:33'])
  })

  it('títulos de tarjeta usan .label (mono 11px)', async () => {
    // happy-dom no resuelve custom properties en getComputedStyle, así que se
    // verifica el contrato estructural: la regla `.card__head h2 {font:inherit}`
    // (0,1,1) ganaba a `.label` (0,1,0). Sin ella, .label aplica.
    const w = await montar()
    const titulos = w.findAll('.card__head h2')
    expect(titulos.length).toBeGreaterThan(0)
    for (const t of titulos) {
      expect(t.classes()).toContain('label')
    }
  })

  it('SIN META es enlace a Objetivo', async () => {
    const w = await montar()
    const accion = w.find('.hero__meta-accion')
    expect(accion.exists()).toBe(true)
    expect(accion.attributes('href')).toBe('/objetivo')
    expect(accion.text()).toContain('DEFINIR META')
  })

  it('días vacíos: línea base, no caja; el día con dato conserva la barra', async () => {
    const w = await montar()
    const vacias = w.findAll('.bars__track--vacia')
    expect(vacias).toHaveLength(6)
    // Sin barra dentro: solo la línea base.
    expect(vacias[0]!.find('.bars__bar').exists()).toBe(false)
    expect(w.findAll('.bars__track:not(.bars__track--vacia)')).toHaveLength(1)
  })

  it('nota de escala corta con el detalle en tooltip', async () => {
    const w = await montar()
    const pie = w.find('.bars__pie .label')
    expect(pie.text()).toBe('ESCALA RELATIVA')
    expect(pie.attributes('title')).toContain('normaliza')
  })

  it('labels de la serie con contraste suficiente', async () => {
    const w = await montar()
    const color = getComputedStyle(w.find('.bars__label').element).color
    // --nt-ink-2 (~#A8A8A8), no --nt-ink-3 (~#666).
    expect(color).not.toBe('rgb(102, 102, 102)')
  })
})