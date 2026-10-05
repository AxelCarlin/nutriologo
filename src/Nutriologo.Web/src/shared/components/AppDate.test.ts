import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import AppDate from './AppDate.vue'

/**
 * Regresión del bug reportado: "elegí la fecha pero dice FECHA REQUERIDA".
 *
 * El componente leía las otras dos partes del date desde `modelValue`. Como
 * solo emitía cuando las tres estaban completas, la selección parcial se
 * perdía: elegir día → null, elegir mes → null, elegir año → `dia` seguía
 * vacío para siempre.
 */
describe('AppDate', () => {
  const seleccionar = async (w: ReturnType<typeof montar>, campo: string, valor: string) => {
    await w.find(`select[aria-label$="${campo}"]`).setValue(valor)
    await w.vm.$nextTick()
  }
  const montar = () => mount(AppDate, { props: { modelValue: null, label: 'NACIMIENTO' } })

  it('acumula selección parcial sin emitir null', async () => {
    const w = montar()

    await seleccionar(w, 'día', '10')
    expect(w.emitted('update:modelValue')).toBeUndefined()

    await seleccionar(w, 'mes', '05')
    expect(w.emitted('update:modelValue')).toBeUndefined()

    // La tercera parte cierra la fecha.
    await seleccionar(w, 'año', '1991')
    const emissions = w.emitted('update:modelValue')
    expect(emissions).toHaveLength(1)
    expect(emissions![0]![0]).toBe('1991-05-10')
  })

  it('muestra el resumen DD MMM AAAA', async () => {
    const w = montar()
    await seleccionar(w, 'día', '10')
    await seleccionar(w, 'mes', '05')
    await seleccionar(w, 'año', '1991')
    expect(w.find('.fecha__res').text()).toBe('10 MAY 1991')
  })

  it('hidrata desde modelValue', () => {
    const w = mount(AppDate, { props: { modelValue: '1991-05-10', label: 'NACIMIENTO' } })
    expect((w.find('select[aria-label$=\"día\"]').element as HTMLSelectElement).value).toBe('10')
    expect((w.find('select[aria-label$=\"mes\"]').element as HTMLSelectElement).value).toBe('05')
    expect((w.find('select[aria-label$=\"año\"]').element as HTMLSelectElement).value).toBe('1991')
    expect(w.find('.fecha__res').text()).toBe('10 MAY 1991')
  })

  it('padStart correcto para días y meses de un dígito', async () => {
    const w = montar()
    await seleccionar(w, 'día', '05')
    await seleccionar(w, 'mes', '03')
    await seleccionar(w, 'año', '2000')
    expect(w.emitted('update:modelValue')![0]![0]).toBe('2000-03-05')
  })

  it('febrero limita a 28/29 días según año bisiesto', async () => {
    const w = montar()
    await seleccionar(w, 'mes', '02')
    await seleccionar(w, 'año', '2024') // bisiesto
    const opciones = w.findAll('select[aria-label$="día"] option').length - 1 // sin "DD"
    expect(opciones).toBe(29)
  })

  it('reset externo limpia la selección local', async () => {
    const w = montar()
    await seleccionar(w, 'día', '10')
    await seleccionar(w, 'mes', '05')
    await seleccionar(w, 'año', '1991')
    // El padre recibe lo emitido (como haría v-model) y después lo resetea.
    await w.setProps({ modelValue: '1991-05-10' })
    await w.vm.$nextTick()
    expect((w.find('select[aria-label$=\"día\"]').element as HTMLSelectElement).value).toBe('10')

    await w.setProps({ modelValue: null })
    await w.vm.$nextTick()
    expect((w.find('select[aria-label$=\"día\"]').element as HTMLSelectElement).value).toBe('')
    // Sin selección no hay resumen: el elemento desaparece en vez de mentir.
    expect(w.find('.fecha__res').exists()).toBe(false)
  })
})