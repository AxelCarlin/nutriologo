import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import PerfilView from './PerfilView.vue'

const CATALOGOS = {
  sexos: [{ id: 1, nombre: 'Masculino' }, { id: 2, nombre: 'Femenino' }],
  actividad: [
    { id: 3, codigo: 'SEDENTARIO', nombre: 'Sedentario', factor: 1.2, descripcion: 'Poco o nada de ejercicio' },
    { id: 4, codigo: 'LIGERO', nombre: 'Ligero', factor: 1.375, descripcion: '1–2 días por semana' },
    { id: 2, codigo: 'MODERADO', nombre: 'Moderado', factor: 1.55, descripcion: '3–5 días por semana' },
    { id: 1, codigo: 'ACTIVO', nombre: 'Activo', factor: 1.725, descripcion: '6–7 días por semana' },
    { id: 5, codigo: 'MUY_ACTIVO', nombre: 'Muy activo', factor: 1.9, descripcion: 'Intenso diario' },
  ],
  tiposMedicion: [{ id: 1, codigo: 'PESO', nombre: 'Peso', unidad: 'g', min: 20, max: 400 }],
}

const PERFIL = {
  id: 1,
  idSexoBiologico: 1,
  idNivelActividadFisica: 2,
  fechaNacimiento: '1991-05-10T00:00:00',
  altura: 175,
  alturaUnidad: 'cm',
}

const MEDICIONES = [{ id: 9, idTipoMedicionCorporal: 1, valor: 80, fechaMedicion: '2026-10-01T09:00:00' }]

function mockFetch(perfil: unknown = PERFIL, mediciones = MEDICIONES) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation((url: string) => {
      let body: unknown = {}
      if (url.startsWith('/api/catalogos')) body = CATALOGOS
      else if (url.startsWith('/api/perfil')) body = perfil
      else if (url.includes('/mediciones')) body = mediciones
      return Promise.resolve({ ok: true, status: 200, json: async () => body, text: async () => '' })
    }),
  )
}

async function montar() {
  const pinia = createPinia()
  const router = createRouter({
    history: createWebHistory(),
    routes: [
      { path: '/', component: { template: '<div />' } },
      { path: '/login', component: { template: '<div />' } },
    ],
  })
  await router.push('/')
  await router.isReady()
  const w = mount(PerfilView, { global: { plugins: [pinia, router] } })
  await new Promise((r) => setTimeout(r, 0))
  await w.vm.$nextTick()
  await w.vm.$nextTick()
  return w
}

describe('PerfilView', () => {
  beforeEach(() => {
    mockFetch()
    localStorage.setItem('nutriologo_token', 'tok')
    localStorage.setItem('nutriologo_nombre', 'axelcarlin')
  })

  it('nunca expone IDs de base de datos', async () => {
    const w = await montar()
    const texto = w.text()
    expect(texto).not.toContain('SEXO BIOLÓGICO (ID)')
    expect(texto).not.toContain('NIVEL DE ACTIVIDAD (ID)')
    expect(texto).not.toContain('(ID)')
  })

  it('identidad: avatar con inicial en dot-matrix', async () => {
    const w = await montar()
    expect(w.find('.avatar').text()).toBe('A')
    expect(w.find('.perfil__nombre').text()).toBe('AXELCARLIN')
  })

  it('datos derivados: edad, altura, IMC y TMB', async () => {
    const w = await montar()
    const d = w.find('.derivados').text()
    expect(d).toContain('años')
    expect(d.toUpperCase()).toContain('175 CM')
    expect(d).toContain('IMC')
    expect(d).toContain('TMB')
    // 80kg / 1.75m² = 26.1
    expect(d).toContain('26.1')
  })

  it('actividad en lista: nombre, factor y descripción por fila', async () => {
    const w = await montar()
    const filas = w.findAll('.seg__row')
    expect(filas).toHaveLength(5)
    expect(filas[0]!.text()).toContain('Sedentario')
    // La fila activa (Moderado ×1.55) lleva el punto y el factor.
    const activa = w.find('.seg__row[aria-checked="true"]')
    expect(activa.text()).toContain('Moderado')
    expect(activa.find('.seg__row-meta').text()).toBe('×1.55')
    expect(activa.find('.seg__row-hint').text()).toContain('3–5 días por semana')
  })

  it('GUARDAR deshabilitado hasta que haya cambios', async () => {
    const w = await montar()
    expect(w.find('.cta').attributes('disabled')).toBeDefined()
    expect(w.text()).not.toContain('SIN GUARDAR')

    // Cambiar actividad ensucia el formulario.
    await w.findAll('.seg__row').find((b) => b.text().includes('Muy activo'))!.trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.cta').attributes('disabled')).toBeUndefined()
    expect(w.text()).toContain('SIN GUARDAR')
  })

  it('cancelar revierte los cambios', async () => {
    const w = await montar()
    await w.findAll('.seg__row').find((b) => b.text().includes('Muy activo'))!.trigger('click')
    await w.vm.$nextTick()
    await w.find('.btn-ghost').trigger('click')
    await w.vm.$nextTick()
    expect(w.find('.cta').attributes('disabled')).toBeDefined()
  })

  it('fecha en formato DD MMM AAAA, no el ambiguo del navegador', async () => {
    const w = await montar()
    // 1991-05-10 → 10 MAY 1991
    expect(w.text()).toContain('10 MAY 1991')
  })

  it('zona de cuenta separada con cerrar sesión', async () => {
    const w = await montar()
    expect(w.find('.cuenta').exists()).toBe(true)
    expect(w.find('.cuenta__fila').text()).toContain('Cerrar sesión')
  })

  it('explica por qué se piden los datos', async () => {
    const w = await montar()
    expect(w.find('.nota').text()).toContain('gasto calórico')
  })
})