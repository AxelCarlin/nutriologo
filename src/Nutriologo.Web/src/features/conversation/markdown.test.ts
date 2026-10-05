import { describe, expect, it } from 'vitest'
import { parsearMarkdown, type Nodo } from './markdown'

/** Acorta al párrafo: la lista tiene otra forma y el test debe decirlo. */
function parrafo(n: Nodo) {
  if (n.tipo !== 'parrafo') throw new Error(`esperaba párrafo, llegó ${n.tipo}`)
  return n.inline
}

describe('parsearMarkdown', () => {
  it('texto plano: un solo párrafo', () => {
    const n = parsearMarkdown('Llevás 400 kcal hoy.')
    expect(n).toHaveLength(1)
    expect(n[0]!.tipo).toBe('parrafo')
  })

  it('viñetas con guion, asterisco y punto se vuelven lista', () => {
    for (const marca of ['-', '*', '•']) {
      const n = parsearMarkdown(`${marca} uno\n${marca} dos`)
      expect(n).toHaveLength(1)
      expect(n[0]!.tipo).toBe('lista')
      if (n[0]!.tipo === 'lista') {
        expect(n[0]!.ordenada).toBe(false)
        expect(n[0]!.items).toHaveLength(2)
      }
    }
  })

  it('numeración da lista ordenada', () => {
    const n = parsearMarkdown('1. primero\n2. segundo')
    expect(n[0]!.tipo).toBe('lista')
    if (n[0]!.tipo === 'lista') {
      expect(n[0]!.ordenada).toBe(true)
      expect(n[0]!.items).toHaveLength(2)
    }
  })

  it('una lista entre párrafos no los se come', () => {
    const n = parsearMarkdown('Primero esto.\n\n- uno\n- dos\n\nY después esto.')
    expect(n.map(x => x.tipo)).toEqual(['parrafo', 'lista', 'parrafo'])
  })

  it('línea en blanco corta el párrafo', () => {
    const n = parsearMarkdown('uno\n\ndos')
    expect(n.map(x => x.tipo)).toEqual(['parrafo', 'parrafo'])
  })

  it('detecta **fuerte**, *em* y `código`', () => {
    const inline = parrafo(parsearMarkdown('**proteína** y *fibra* y `300g`')[0]!)
    expect(inline.some(i => i.tipo === 'fuerte' && i.valor === 'proteína')).toBe(true)
    expect(inline.some(i => i.tipo === 'em' && i.valor === 'fibra')).toBe(true)
    expect(inline.some(i => i.tipo === 'codigo' && i.valor === '300g')).toBe(true)
  })

  it('la negrita no se parte en cursiva', () => {
    const inline = parrafo(parsearMarkdown('**a b c**')[0]!)
    expect(inline).toHaveLength(1)
    expect(inline[0]).toEqual({ tipo: 'fuerte', valor: 'a b c' })
  })

  it('el HTML llega como texto, nunca como etiqueta', () => {
    // Es lo importante: el texto viene del modelo y se persiste.
    const n = parsearMarkdown('<script>alert(1)</script> y <img src=x onerror=alert(1)>')
    const todo = JSON.stringify(n)
    expect(todo).toContain('script')
    // Lo que se parsea es texto plano: no existe ningún nodo que lo convierta
    // en elemento, y el render usa nodos de Vue, no v-html.
    expect(n.every(x => x.tipo === 'parrafo' || x.tipo === 'lista')).toBe(true)
  })

  it('un guion suelto que no es lista no inventa formato', () => {
    // "3-4矫" arranca con dígito y no es "3." ni "3)": es texto.
    const n = parsearMarkdown('3-4orpora')
    expect(n[0]!.tipo).toBe('parrafo')
  })

  it('texto vacío no produce nodos', () => {
    expect(parsearMarkdown('')).toEqual([])
    expect(parsearMarkdown('   \n\n  ')).toEqual([])
  })

  it('una lista muy larga no se trunca', () => {
    const items = Array.from({ length: 50 }, (_, i) => `- item ${i}`).join('\n')
    const n = parsearMarkdown(items)
    if (n[0]!.tipo === 'lista') expect(n[0]!.items).toHaveLength(50)
  })
})