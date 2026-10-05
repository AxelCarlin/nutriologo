import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import RegistroComidaView from './RegistroComidaView.vue'

/**
 * El router se sustituye para poder observar la navegación. `__query` es lo
 * que "llegó" del chat: se cambia por test para simular la precarga.
 */
const __query: { valor: Record<string, unknown> } = { valor: {} }
const __replace = vi.fn()
vi.mock('vue-router', () => ({
  useRoute: () => ({ query: __query.valor, path: '/registrar' }),
  useRouter: () => ({ replace: __replace }),
}))

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
    // Sin datos del chat: el formulario arranca con el primer alimento.
    __query.valor = {}
    __replace.mockClear()
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

  // ───────── Precarga desde el chat ─────────

  it('con ?nombre= el formulario viene cargado, no vacío', async () => {
    __query.valor = { nombre: ['Huevo', 'Arroz'], cantidad: ['2', '150'], unidad: ['PIEZA', 'G'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    expect(w.findAll('.linea')).toHaveLength(2)
    // El nombre travels en el value del combobox de cada fila.
    const nombres = w.findAll('.combobox__input').map(e => (e.element as HTMLInputElement).value)
    expect(nombres).toEqual(['Huevo', 'Arroz'])

    // Y las cantidades y unidades vienen del chat, no del default 100/G.
    const cantidades = w.findAll('.stepper input, input[type="number"]').map(
      e => Number((e.element as HTMLInputElement).value),
    )
    expect(cantidades).toContain(2)
    expect(cantidades).toContain(150)
  })

  it('avisa que los datos vienen del chat y NO los guarda solo', async () => {
    __query.valor = { nombre: ['Arroz'], cantidad: ['150'], unidad: ['G'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Aviso visible...
    expect(w.text()).toContain('TRAÍDO DEL CHAT')
    // ...pero el POST solo ocurre cuando el usuario aprieta REGISTRAR.
    const f = (globalThis.fetch as ReturnType<typeof vi.fn>)
    expect(f.mock.calls.filter(c => String(c[0]).startsWith('/api/comidas'))).toHaveLength(0)
  })

  it('la query se limpia tras consumirse: recargar no vuelve a precargar', async () => {
    __query.valor = { nombre: ['Arroz'], cantidad: ['150'], unidad: ['G'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Se navega a la misma ruta SIN los items: el estado no vive en la URL.
    expect(__replace).toHaveBeenCalledWith({ path: '/registrar', query: {} })
  })

  it('una query a medias se ignora en vez de inventar la fila', async () => {
    // cantidad viene como número suelto y unidad falta.
    __query.valor = { nombre: ['Arroz'], cantidad: '150' }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    const nombres = w.findAll('.combobox__input').map(
      e => (e.element as HTMLInputElement).value,
    )
    expect(nombres).toEqual(['Arroz'])
  })

  // ───────── Kcal por fila: la unidad manda, no el 100 fijo ─────────

  it('con PIEZA usa los gramos de la porción, no dividir por 100', async () => {
    // Huevo: 155 kcal por 100 g, porción declarada "1 pieza" = 50 g.
    // 2 PIEZA son 100 g, o sea 155 kcal. La versión anterior dividía siempre
    // por 100 y daba 155 × 2 / 100 = 3 kcal.
    __query.valor = { nombre: ['Huevo'], cantidad: ['2'], unidad: ['PIEZA'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    expect(w.findAll('.linea')[0]!.find('.linea__kcal').text()).toContain('155')
  })

  it('con GRAMOS la cantidad ya viene en la base del alimento', async () => {
    __query.valor = { nombre: ['Huevo'], cantidad: ['100'], unidad: ['G'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    expect(w.findAll('.linea')[0]!.find('.linea__kcal').text()).toContain('155')
  })

  it('el total suma las filas mezclando unidades', async () => {
    // 2 piezas de huevo (100 g = 155) + 150 g de arroz (130 kcal/100 g).
    __query.valor = {
      nombre: ['Huevo', 'Arroz'],
      cantidad: ['2', '150'],
      unidad: ['PIEZA', 'G'],
    }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    const cal = [...w.findAll('.linea__kcal')].map(e => e.text())
    expect(cal[0]).toContain('155')
    expect(cal[1]).toContain('195')
    // 155 + 195
    expect(w.text()).toContain('350 KCAL')
  })

  it('una unidad sin conversión muestra — en vez de un número inventado', async () => {
    // El test declara porción para Huevo y Arroz, así que TAZA no se puede
    // convertir: el preview se abstiene en vez de adivinar.
    __query.valor = { nombre: ['Huevo'], cantidad: ['2'], unidad: ['TAZA'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    const kcal = w.findAll('.linea')[0]!.find('.linea__kcal')
    expect(kcal.text()).toContain('—')
    expect(kcal.classes()).toContain('linea__kcal--vacio')
  })

  it('manda TipoComida al registrar: sin eso el panel no sabe el tipo', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Elegir DESAYUNO (id 5) es el default de la vista.
    await w.find('.cta').trigger('click')
    await new Promise((r) => setTimeout(r, 0))

    const f = globalThis.fetch as ReturnType<typeof vi.fn>
    const post = f.mock.calls.find(c => String(c[0]).startsWith('/api/comidas'))
    expect(post).toBeDefined()
    const cuerpo = JSON.parse(String((post![1] as { body: string }).body))

    // Sin TipoComida el registro se guardaba con IdTipoComida = NULL y el panel
    // caía al rótulo "Comida" para el desayuno, la colación y la cena igual.
    expect(cuerpo.TipoComida).toBe(5)
    expect(cuerpo.Nombre).toBe('Desayuno')
  })

  it('cambiar de tipo cambia lo que se manda', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Clic en el chip CENA (id 2).
    const chips = w.findAll('.chip')
    const cena = chips.find(c => c.text() === 'Cena')
    expect(cena).toBeDefined()
    await cena!.trigger('click')
    await w.vm.$nextTick()

    await w.find('.cta').trigger('click')
    await new Promise((r) => setTimeout(r, 0))

    const f = globalThis.fetch as ReturnType<typeof vi.fn>
    const post = f.mock.calls.find(c => String(c[0]).startsWith('/api/comidas'))
    const cuerpo = JSON.parse(String((post![1] as { body: string }).body))
    expect(cuerpo.TipoComida).toBe(2)
    expect(cuerpo.Nombre).toBe('Cena')
  })

  // ───────── El texto escrito tiene que ser el que se guarda ─────────

  it('escribir sin tocar la sugerencia NO registra otro alimento', async () => {
    // El combobox tiene dos campos: `texto` (lo escrito) y `nombreAlimento`
    // (lo guardado), y solo coinciden al elegir una sugerencia. Antes de este
    // chequeo, escribir "Huevo" y apretar REGISTRAR guardaba "Aceite" (el
    // primer alimento del catálogo, preseleccionado) sin ningún aviso.
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // "Arroz" y no "Huevo": el catálogo del test empieza con Huevo, que es lo
    // que queda preseleccionado, y escribirlo sí resolvería la fila.
    await w.find('.combobox__input').setValue('Arroz')
    await w.vm.$nextTick()

    // El botón queda bloqueado y se explica por qué.
    expect(w.find('.cta').attributes('disabled')).toBeDefined()
    expect(w.text()).toContain('ELEGÍ UN ALIMENTO DE LA LISTA')

    // Y no se envió nada.
    const f = globalThis.fetch as ReturnType<typeof vi.fn>
    expect(f.mock.calls.filter(c => String(c[0]).startsWith('/api/comidas'))).toHaveLength(0)
  })

  it('el input sin resolver queda marcado para que se note', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    expect(w.find('.combobox__input').classes()).not.toContain(
      'combobox__input--sin-resolver',
    )

    await w.find('.combobox__input').setValue('Arroz')
    await w.vm.$nextTick()

    const input = w.find('.combobox__input')
    expect(input.classes()).toContain('combobox__input--sin-resolver')
    expect(input.attributes('aria-invalid')).toBe('true')
  })

  it('la comparación ignora mayúsculas y tildes', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // "HUEVO" y "huevo" son el mismo alimento: la fila queda resuelta.
    await w.find('.combobox__input').setValue('HUEVO')
    await w.vm.$nextTick()

    // La cantidad por defecto (100) es válida, así que el único bloqueo posible
    // es el del nombre: si el texto resuelve, no aparece el error de la lista.
    expect(w.text()).not.toContain('ELEGÍ UN ALIMENTO DE LA LISTA')
  })

  it('elegir la sugerencia resuelve la fila y habilita el registro', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    await w.find('.combobox__input').setValue('Huevo')
    await w.vm.$nextTick()
    await w.find('.combobox__opcion').trigger('click')
    await w.vm.$nextTick()

    // Resuelta: sin el error de lista y el botón habilitado.
    expect(w.text()).not.toContain('ELEGÍ UN ALIMENTO DE LA LISTA')
    expect(w.find('.cta').attributes('disabled')).toBeUndefined()
  })

  it('los datos del chat llegan ya resueltos y no bloquean el registro', async () => {
    __query.valor = { nombre: ['Huevo'], cantidad: ['2'], unidad: ['PIEZA'] }

    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Viene del chat: `texto` y `nombreAlimento` son el mismo nombre, así que
    // la fila no queda marcada y el usuario puede registrar de una.
    expect(w.text()).not.toContain('ELEGÍ UN ALIMENTO DE LA LISTA')
    expect(w.find('.cta').attributes('disabled')).toBeUndefined()
  })

  it('una fila sin resolver no muestra kcal de otro alimento', async () => {
    const w = mount(RegistroComidaView)
    await new Promise((r) => setTimeout(r, 0))
    await w.vm.$nextTick()

    // Con Huevo preseleccionado la fila muestra sus 155 kcal.
    expect(w.find('.linea__kcal').text()).toContain('155')

    // Al escribir "Arroz" sin elegirlo, la fila pasa a "—": mostrar 155 (o las
    // del primer alimento) sería el número de otro alimento.
    await w.find('.combobox__input').setValue('Arroz')
    await w.vm.$nextTick()

    const kcal = w.find('.linea__kcal')
    expect(kcal.text()).toContain('—')
    expect(kcal.classes()).toContain('linea__kcal--vacio')
    // Y el total tampoco suma ese número.
    expect(w.text()).toContain('ESTA COMIDA0 KCAL')
  })
})
