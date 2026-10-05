import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import ObjetivoView from './ObjetivoView.vue'

// Respuesta real del backend (92kg, 175cm, 35a, 1.55, perder 10kg en 22 sem).
const RESPUESTA = {
  metabolismoBasal: 1843.75,
  gastoEnergeticoTotal: 2857.81,
  deficitDiarioRequerido: 500,
  metaCaloricaEstimada: 2357.81,
  ritmoSemanalKg: 0.4545,
  porcentajeCambio: 10.8696,
  // NivelRiesgo en DB: 1=MODERADO, 2=ALTO, 3=BAJO
  evaluacionObjetivo: { codigo: 'RITMO_LENTO', descripcion: 'Ritmo lento/seguro', idNivelRiesgo: 3 },
  evaluacionMetaCalorica: {
    codigo: 'SIN_RIESGO',
    descripcion: 'Sin riesgo destacado',
    idNivelRiesgo: 3,
    deficitCalculado: 500,
    porcentajeDeficit: 0.174959,
  },
}

const CATALOGOS = {
  sexos: [{ id: 1, nombre: 'Masculino' }, { id: 2, nombre: 'Femenino' }],
  actividad: [
    { id: 3, codigo: 'SEDENTARIO', nombre: 'Sedentario', factor: 1.2, descripcion: 'Poco o nada de ejercicio' },
    { id: 1, codigo: 'LIGERO', nombre: 'Ligero', factor: 1.375, descripcion: '1–2 días por semana' },
    { id: 2, codigo: 'MODERADO', nombre: 'Moderado', factor: 1.55, descripcion: '3–5 días por semana' },
    { id: 4, codigo: 'ACTIVO', nombre: 'Activo', factor: 1.725, descripcion: '6–7 días por semana' },
    { id: 5, codigo: 'MUY_ACTIVO', nombre: 'Muy activo', factor: 1.9, descripcion: 'Intenso diario' },
  ],
  tiposMedicion: [{ id: 1, codigo: 'PESO', nombre: 'Peso', unidad: 'g', min: 20, max: 400 }],
}

// Sin perfil por defecto: obliga a pedir los datos.
const SIN_PERFIL = null

function mockFetch(payload: unknown = RESPUESTA, perfil: unknown = SIN_PERFIL) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation((url: string) => {
      let body: unknown = payload
      if (url.startsWith('/api/catalogos')) body = CATALOGOS
      else if (url.startsWith('/api/perfil')) body = perfil
      return Promise.resolve({ ok: true, status: 200, json: async () => body, text: async () => '' })
    }),
  )
}

async function montar() {
  const pinia = createPinia()
  // Router mínimo: resuelve RouterLink sin montar rutas reales.
  const router = createRouter({
    history: createWebHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/perfil', component: { template: '<div />' } },
    ],
  })
  await router.push('/')
  await router.isReady()

  const w = mount(ObjetivoView, { global: { plugins: [pinia, router] } })
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
  await w.vm.$nextTick()
  return w
}

async function calcular(w: Awaited<ReturnType<typeof montar>>) {
  await w.find('form').trigger('submit')
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
}

describe('ObjetivoView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
  })

  it('actividad en lista vertical: 5 filas de 48px, sin pill deformado', async () => {
    const w = await montar()
    // Sexo (2) y tipo (3) siguen en pildora; actividad (5) va en lista.
    expect(w.findAll('.seg__btn')).toHaveLength(5)
    const filas = w.findAll('.seg__row')
    expect(filas).toHaveLength(5)
    expect(filas.map((f) => f.find('.seg__row-label').text())).toEqual([
      'Sedentario', 'Ligero', 'Moderado', 'Activo', 'Muy activo',
    ])
    // El factor es dato secundario en la fila, nunca campo editable.
    expect(w.findAll('input[type=number]').find((i) => i.attributes('id') === 'o-factor')).toBeUndefined()
  })

  it('la fila activa muestra factor y descripción, sin párrafo duplicado', async () => {
    const w = await montar()
    const activa = w.find('.seg__row[aria-checked="true"]')
    expect(activa.find('.seg__row-meta').text()).toBe('×1.55')
    expect(activa.find('.seg__row-hint').text()).toContain('3–5 días por semana')
    expect(w.find('.actividad-desc').exists()).toBe(false)
  })

  it('agrupa en tres bloques: DATOS, ACTIVIDAD, META', async () => {
    const w = await montar()
    const titulos = w.findAll('.bloque').map((b) => b.element.firstElementChild?.textContent?.trim())
    expect(titulos).toEqual(['DATOS', 'ACTIVIDAD', 'META'])
  })

  it('sin perfil: pide sexo y tipo de objetivo como segmented', async () => {
    const w = await montar()
    expect(w.findAll('select')).toHaveLength(0)
    expect(w.find('.heredado').exists()).toBe(false)
    expect(w.findAll('.seg').length).toBeGreaterThanOrEqual(2)
  })

  it('con perfil: hereda altura/edad/sexo y NO vuelve a pedirlos', async () => {
    mockFetch(RESPUESTA, {
      id: 1,
      idSexoBiologico: 1,
      idNivelActividadFisica: 2, // Moderado = 1.55
      fechaNacimiento: '1991-05-10T00:00:00',
      altura: 178,
      alturaUnidad: 'cm',
    })
    const w = await montar()
    const heredado = w.find('.heredado')
    expect(heredado.exists()).toBe(true)
    expect(heredado.text()).toContain('178 CM')
    expect(heredado.text()).toContain('Masculino')
    // Enlace a donde sí se edita.
    expect(heredado.find('a').attributes('href')).toBe('/perfil')
  })

  it('heredado compacto: una línea con enlace para móvil', async () => {
    mockFetch(RESPUESTA, {
      id: 1,
      idSexoBiologico: 1,
      idNivelActividadFisica: 2,
      fechaNacimiento: '1991-05-10T00:00:00',
      altura: 178,
      alturaUnidad: 'cm',
    })
    const w = await montar()
    const compacto = w.find('.heredado__compacto')
    expect(compacto.exists()).toBe(true)
    expect(compacto.text()).toContain('178 CM')
    expect(compacto.find('a').attributes('href')).toBe('/perfil')
  })

  it('muestra el ritmo en vivo antes de calcular', async () => {
    const w = await montar()
    expect(w.find('.ritmo-previo').text()).toContain('0.45 KG/SEM')
    expect(w.find('.ritmo-previo').text()).toContain('PÉRDIDA')
  })

  it('hero: kcal en dot-matrix y riesgo mapeado según NivelRiesgo', async () => {
    const w = await montar()
    await calcular(w)
    expect(w.find('.kcal').text()).toBe('2,358')
    // idNivelRiesgo 3 = BAJO → nivel 'bajo', NO rojo.
    expect(w.find('.riesgo__nivel').attributes('data-nivel')).toBe('bajo')
    expect(w.find('.riesgo__nivel').text()).toContain('Bajo')
  })

  it('riesgo alto se pinta rojo (idNivelRiesgo 2 = ALTO)', async () => {
    mockFetch({
      ...RESPUESTA,
      evaluacionObjetivo: { codigo: 'RITMO_AGRESIVO', descripcion: 'Ritmo agresivo', idNivelRiesgo: 2 },
    })
    const w = await montar()
    await calcular(w)
    expect(w.find('.riesgo__nivel').attributes('data-nivel')).toBe('alto')
  })

  it('bloquea CALCULAR si el peso objetivo es inválido', async () => {
    const w = await montar()
    // AppField usa type="text" con inputmode decimal y formatea en onInput.
    // setValue dispara input con value string: hay que ir por el evento real.
    const input = w.find('input[aria-label="PESO OBJETIVO"]')
    await input.setValue('92')
    await w.vm.$nextTick()

    expect(w.find('.cta').attributes('disabled')).toBeDefined()
    expect(w.text()).toContain('DEBE DIFERIR DEL PESO ACTUAL')
  })
})