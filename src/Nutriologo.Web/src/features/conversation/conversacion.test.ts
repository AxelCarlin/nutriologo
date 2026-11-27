import { mount, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import ConversacionView from './ConversacionView.vue'

/**
 * El router se sustituye para poder observar la navegación sin montar la app
 * entera. `__push` registra exactamente a dónde y con qué query fue el botón.
 */
const __push = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: __push }),
  useRoute: () => ({ query: {}, path: '/chat' }),
}))

/** Frame SSE tal como lo emite `ChatEndpoints`. */
const frame = (evento: string, datos: unknown) =>
  `event: ${evento}\ndata: ${JSON.stringify(datos)}\n\n`

/** El error que produce abortar un fetch real: DOMException AbortError. */
function errorAbortado(): Error {
  const e = new Error('The operation was aborted')
  e.name = 'AbortError'
  return e
}

/**
 * Respuesta con cuerpo de stream, como la del endpoint real.
 * Respeta `init.signal`: al abortar, el read pendiente falla con AbortError,
 * que es exactamente lo que hace `fetch` cuando se corta la conexión.
 */
function sse(frames: string[], delayMs = 0, init?: RequestInit, neverClose = false): Response {
  const cuerpo = new ReadableStream<Uint8Array>({
    async start(control) {
      const enc = new TextEncoder()
      const cortar = () => {
        try {
          control.error(errorAbortado())
        } catch {
          /* el stream ya estaba cerrado */
        }
      }
      init?.signal?.addEventListener('abort', cortar, { once: true })

      for (const f of frames) {
        if (delayMs) await new Promise(r => setTimeout(r, delayMs))
        if (init?.signal?.aborted) return cortar()
        try {
          control.enqueue(enc.encode(f))
        } catch {
          return // abortado a mitad
        }
      }

      // Sin cerrar: el modelo sigue "hablando" hasta que el cliente corta.
      if (neverClose) return
      control.close()
    },
  })

  return new Response(cuerpo as unknown as BodyInit, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}

/** Stream de una respuesta completa del respondedor. */
function streamRespuesta(texto: string, extra: string[] = []) {
  return sse([
    frame('paso', { nombre: 'interpretacion', estado: 'start', detalle: 'Interpretando tu mensaje' }),
    frame('paso', { nombre: 'interpretacion', estado: 'ok', detalle: 'Interpretando tu mensaje' }),
    frame('paso', { nombre: 'respuesta', estado: 'start', detalle: 'Redactando tu respuesta' }),
    ...extra,
    ...texto.split(/(?<=\s)/).filter(Boolean).map(t => frame('token', { texto: t })),
    frame('paso', { nombre: 'respuesta', estado: 'ok', detalle: 'Redactando tu respuesta' }),
    frame('result', { tipo: 'respuesta', texto, fallback: false }),
    frame('done', { ms: 1234 }),
  ])
}

async function enviar(w: ReturnType<typeof mount>, texto = '¿cómo voy hoy?') {
  await w.find('textarea').setValue(texto)
  await w.find('form.composer').trigger('submit.prevent')
}

async function montarCon(frames: string[], delayMs = 0, neverClose = false) {
  const fetchMock = vi.fn((_url: string, init?: RequestInit) =>
    Promise.resolve(sse(frames, delayMs, init, neverClose)),
  )
  vi.stubGlobal('fetch', fetchMock)
  localStorage.setItem('nutriologo_token', 'tok')
  const w = mount(ConversacionView)
  return { w, fetchMock }
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.useRealTimers()
  localStorage.clear()
  __push.mockClear()
})

describe('ConversacionView — estructura', () => {
  it('estado vacío: título, sugerencias y un único indicador en el encabezado', () => {
    const w = mount(ConversacionView)

    expect(w.find('.vacio__titulo').text()).toBe('¿Qué comiste hoy?')
    expect(w.findAll('.vacio .chip')).toHaveLength(3)

    // Un solo indicador, dentro del encabezado, con 25 puntos.
    expect(w.findAll('.dotmatrix')).toHaveLength(1)
    expect(w.find('.dotmatrix').element.parentElement?.classList.contains('estado')).toBe(true)
    expect(w.findAll('.dotmatrix__punto')).toHaveLength(25)

    // Una sola etiqueta de estado en toda la vista.
    expect(w.findAll('.estado__etiqueta')).toHaveLength(1)
    expect(w.find('.estado__etiqueta').text()).toBe('[IDLE]')
    expect(w.text()).not.toContain('SPEAKING')

    expect(w.find('form.composer').exists()).toBe(true)
  })


  it('las sugerencias viven SOLO en el estado vacío, no sobre el input', () => {
    const w = mount(ConversacionView)

    // Un solo juego de sugerencias en toda la pantalla. La versión anterior
    // tenía seis píldoras en dos filas: `¿cómo voy hoy?` repetido, y
    // `¿qué ceno?` preguntando lo mismo que `¿qué puedo cenar?`.
    expect(w.findAll('.chip')).toHaveLength(3)
    expect(w.find('.composer__chips').exists()).toBe(false)
    expect(w.text().match(/cómo voy hoy/gi)).toHaveLength(1)

    // El compositor queda con el campo y la línea de atajos. Nada más.
    const hijos = Array.from(w.find('form.composer').element.children).map(c => c.className)
    expect(hijos).toHaveLength(2)
    expect(hijos[0]).toContain('composer__envoltura')
    expect(hijos[1]).toContain('composer__ayuda')
  })

  it('las tres sugerencias son tres intenciones distintas', () => {
    const w = mount(ConversacionView)
    const textos = w.findAll('.vacio .chip').map(c => c.text().toLowerCase())
    expect(textos).toEqual(['armar mi desayuno', '¿cómo voy hoy?', '¿qué puedo cenar?'])
    expect(new Set(textos).size).toBe(3)
  })

  it('el encabezado alterna Nueva y Detener: un solo control del turno', async () => {
    const { w } = await montarCon([frame('token', { texto: 'sigo' })], 30, true)
    expect(w.find('.chat__cabecera .fantasma').text()).toBe('[NUEVA]')

    await enviar(w)
    await new Promise(r => setTimeout(r, 60))

    // Mientras redacta, el encabezado ofrece Detener y no Nueva.
    expect(w.find('.chat__cabecera .fantasma').text()).toBe('[DETENER]')
    expect(w.findAll('.chat__cabecera .fantasma')).toHaveLength(1)

    // Y el botón de enviar no es un segundo control del turno: está
    // deshabilitado y fuera de la ruta de tabulación.
    const btn = w.find('.composer__enviar')
    expect(btn.attributes('disabled')).toBeDefined()
    expect(btn.attributes('aria-hidden')).toBe('true')
    expect(btn.attributes('tabindex')).toBe('-1')
  })

  it('el botón de enviar se arma solo con texto', async () => {
    const w = mount(ConversacionView)
    expect(w.find('.composer__enviar').attributes('disabled')).toBeDefined()
    expect(w.find('.composer__area').attributes('placeholder')).toBe('Escribe tu mensaje')

    await w.find('textarea').setValue('hola')
    expect(w.find('.composer__enviar').attributes('disabled')).toBeUndefined()
  })

  it('el botón [BAJAR] está fuera del hilo para no tapar el texto', async () => {
    const { w } = await montarCon([
      frame('result', { tipo: 'respuesta', texto: 'ok', fallback: false }),
      frame('done', { ms: 50 }),
    ])
    await enviar(w)
    await flushPromises()

    // Con el hilo arriba, el botón tiene que aparecer.
    const hilo = w.find('.chat__hilo')
    Object.defineProperty(hilo.element, 'scrollHeight', { value: 5000, configurable: true })
    Object.defineProperty(hilo.element, 'clientHeight', { value: 400, configurable: true })
    await hilo.trigger('scroll')
    await flushPromises()

    expect(w.find('.bajar').exists()).toBe(true)
    // El criterio: no es descendiente del hilo (no queda en su flujo).
    expect(w.find('.chat__hilo .bajar').exists()).toBe(false)
  })

  it('el markdown del asistente se renderiza como lista, no como guiones', async () => {
    const texto = 'Para hoy:\n- 120 g de proteína\n- 30 g de fibra'
    const { w } = await montarCon([
      frame('result', { tipo: 'respuesta', texto, fallback: false }),
      frame('done', { ms: 50 }),
    ])
    await enviar(w)
    await flushPromises()

    const agente = w.findAll('.msg--agent').at(-1)!
    expect(agente.findAll('.md__lista')).toHaveLength(1)
    expect(agente.findAll('.md__item')).toHaveLength(2)
    // El guion crudo ya no está en el texto: lo aporta el marcador CSS.
    expect(agente.find('.md__parrafo').text()).toBe('Para hoy:')
  })
})

describe('ConversacionView — ciclo de vida de una corrida', () => {
  it('pide el endpoint de stream con POST, historial y token', async () => {
    const { w, fetchMock } = await montarCon([
      frame('result', { tipo: 'respuesta', texto: 'Hola', fallback: false }),
      frame('done', { ms: 5 }),
    ])

    await enviar(w)
    await flushPromises()

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/conversacion/stream')
    expect(init.method).toBe('POST')
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer tok')
    expect(JSON.parse(init.body as string).texto).toBe('¿cómo voy hoy?')
  })

  it('la respuesta completa deja el mensaje con texto, meta y acciones', async () => {
    const { w } = await montarCon([])
    ;(globalThis.fetch as any).mockResolvedValueOnce(streamRespuesta('Llevas 1200 kcal de 2200 hoy.'))

    await enviar(w)
    await flushPromises()

    // Texto del asistente: presente.
    expect(w.text()).toContain('Llevas 1200 kcal de 2200 hoy.')

    // El mensaje del asistente NO es una caja; el del usuario sí.
    const agente = w.findAll('.msg--agent').at(-1)!
    expect(agente.find('.msg__burbuja').exists()).toBe(false)
    expect(agente.find('.msg__texto').exists()).toBe(true)
    expect(w.find('.msg--user .msg__burbuja').exists()).toBe(true)

    // Meta con valores medidos + las cuatro acciones.
    expect(agente.find('.msg__meta-linea').text()).toMatch(/^\[\d+\.\dS · \d+ TOKENS\]$/)
    const acciones = agente.findAll('.msg__acciones .fantasma').map(b => b.text())
    expect(acciones).toEqual(['COPIAR', 'REGENERAR', 'ÚTIL', 'NO ÚTIL'])

    // Estado final: listo, y el composer desbloqueado.
    expect(w.find('.estado__etiqueta').text()).toBe('[LISTO]')
    expect(w.find('.chat__hilo').attributes('aria-busy')).toBe('false')
  })

  it('vuelve a IDLE después de LISTO, sin temporizadores colgados', async () => {
    vi.useFakeTimers()
    const { w } = await montarCon([])
    ;(globalThis.fetch as any).mockResolvedValueOnce(
      streamRespuesta('Listo.', []),
    )

    await w.find('textarea').setValue('hola')
    await w.find('form.composer').trigger('submit.prevent')
    await vi.advanceTimersByTimeAsync(50)
    await flushPromises()

    expect(w.find('.estado__etiqueta').text()).toBe('[LISTO]')

    await vi.advanceTimersByTimeAsync(2000)

    expect(w.find('.estado__etiqueta').text()).toBe('[IDLE]')
    // El mensaje sigue en el hilo: no se borra al volver a idle.
    expect(w.text()).toContain('Listo.')
  })

  it('no deja mensaje vivo ni pasos sueltos cuando el turno termina', async () => {
    const { w } = await montarCon([])
    ;(globalThis.fetch as any).mockResolvedValueOnce(streamRespuesta('Sin pendientes.'))

    await enviar(w)
    await flushPromises()

    expect(w.find('.msg--viva').exists()).toBe(false)
    expect(w.find('.msg__error').exists()).toBe(false)
    // Nada de "procesando" colgando.
    expect(w.text()).not.toContain('[PENSANDO...]')
  })
})

describe('ConversacionView — pasos reales', () => {
  it('muestra un paso por fase real y lo colapsa al llegar el primer token', async () => {
    const { w } = await montarCon([])
    ;(globalThis.fetch as any).mockResolvedValueOnce(streamRespuesta('Respuesta corta.'))

    await enviar(w)
    await flushPromises()

    const agente = w.findAll('.msg--agent').at(-1)!
    const pasos = agente.findAll('.paso')
    // Una fila por fase REAL, no una por evento: el `start` y el `ok` de
    // "respuesta" son la misma fase. Dos fases = dos pasos.
    expect(pasos).toHaveLength(2)
    expect(pasos.map(p => p.find('.paso__nombre').text())).toEqual([
      'Interpretando tu mensaje',
      'Redactando tu respuesta',
    ])
    expect(agente.find('.paso[data-estado="fallo"]').exists()).toBe(false)
    // Ambas cerraron: ninguna queda "en curso" colgando.
    expect(agente.findAll('.paso[data-estado="corriendo"]')).toHaveLength(0)

    // El resumen lleva SOLO la cuenta de pasos. El tiempo total ya está en la
    // meta del mensaje: repetirlo mostraba dos cronómetros del mismo turno con
    // valores distintos (9.4s de pasos contra 9.5s de meta).
    expect(agente.find('.pasos__resumen').text()).toBe('2 pasos')

    // Y la duración de cada paso sí se conserva: es información distinta.
    const duraciones = agente.findAll('.paso__ms').map(e => e.text())
    expect(duraciones).toHaveLength(2)
    expect(duraciones.every(d => /\d+\.\d+s$/.test(d))).toBe(true)
  })

  it('la meta lleva el tiempo total una sola vez', async () => {
    const { w } = await montarCon([
      frame('paso', { nombre: 'interpretacion', estado: 'start', detalle: 'Interpretando tu mensaje' }),
      frame('paso', { nombre: 'interpretacion', estado: 'ok', detalle: 'Interpretando tu mensaje' }),
      frame('paso', { nombre: 'respuesta', estado: 'start', detalle: 'Redactando tu respuesta' }),
      frame('paso', { nombre: 'respuesta', estado: 'ok', detalle: 'Redactando tu respuesta' }),
      frame('result', { tipo: 'respuesta', texto: 'Hola.', fallback: false }),
      frame('done', { ms: 900 }),
    ])

    await enviar(w, 'hola')
    await flushPromises()

    const agente = w.findAll('.msg--agent').at(-1)!
    // Un solo reloj por mensaje: el de la meta.
    expect(agente.find('.msg__meta-linea').text()).toMatch(/^\[\d+\.\dS( · \d+ TOKENS)?\]$/)
    expect(agente.find('.pasos__resumen').text()).not.toMatch(/\d+\.\d/)
  })

  it('no muestra bloque de pasos si el turno no reportó ninguno', async () => {
    const { w } = await montarCon([
      frame('result', { tipo: 'respuesta', texto: 'Directo.', fallback: false }),
      frame('done', { ms: 3 }),
    ])

    await enviar(w)
    await flushPromises()

    expect(w.find('.pasos').exists()).toBe(false)
  })
})

describe('ConversacionView — interrupciones', () => {
  it('un fallo de red deja el mensaje de error y el estado ERROR, sin el composer bloqueado', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('sin red', { status: 500 })))
    localStorage.setItem('nutriologo_token', 'tok')
    const w = mount(ConversacionView)

    await enviar(w)
    await flushPromises()

    expect(w.find('.estado__etiqueta').text()).toBe('[ERROR]')
    expect(w.find('.chat').attributes('data-estado')).toBe('error')
    // Desbloqueado: se puede volver a enviar.
    expect(w.find('.chat__hilo').attributes('aria-busy')).toBe('false')
    expect(w.find('.composer__envoltura').attributes('data-bloqueado')).toBe('false')
  })

  it('abort() marca DETENIDO, conserva el texto parcial y vuelve a IDLE', async () => {
    const { w } = await montarCon([
      frame('paso', { nombre: 'respuesta', estado: 'start', detalle: 'Redactando tu respuesta' }),
      frame('token', { texto: 'Empiezo la respuesta y' }),
    ], 40, true) // el stream sigue abierto: hay algo que cortar

    await enviar(w)
    // Se deja llegar el token (40ms por frame) antes de cortar.
    await new Promise(r => setTimeout(r, 110))
    expect(w.text()).toContain('Empiezo la respuesta y')
    expect(w.find('.estado__etiqueta').text()).toBe('[RESPONDIENDO]')

    // Cortar mientras sigue llegando. Detener vive en el encabezado, que es
    // donde está el estado del turno.
    await w.find('.chat__cabecera .fantasma').trigger('click')
    await flushPromises()

    expect(w.find('.estado__etiqueta').text()).toBe('[DETENIDO]')
    // El texto parcial NO se pierde.
    expect(w.text()).toContain('Empiezo la respuesta y')
    expect(w.text()).toContain('[RESPUESTA DETENIDA]')

    await new Promise(r => setTimeout(r, 1700))
    expect(w.find('.estado__etiqueta').text()).toBe('[IDLE]')
  })

  it('Esc también detiene la corrida', async () => {
    const { w } = await montarCon([frame('token', { texto: 'sigo' })], 30, true)

    await enviar(w)
    await new Promise(r => setTimeout(r, 60))

    await w.find('textarea').trigger('keydown.esc')
    await flushPromises()

    expect(w.find('.estado__etiqueta').text()).toBe('[DETENIDO]')
  })

  it('ignora los eventos de una corrida vieja', async () => {
    // Corrida 1: el stream sigue llegando después de empezar la 2.
    const fetchMock = vi.fn((_url: string, init?: RequestInit) =>
      Promise.resolve(sse([frame('token', { texto: 'VIEJO' })], 60, init)),
    )
    vi.stubGlobal('fetch', fetchMock)
    localStorage.setItem('nutriologo_token', 'tok')
    const w = mount(ConversacionView)

    await enviar(w, 'primero')
    await new Promise(r => setTimeout(r, 10))

    // [DETENER] corta la corrida 1; recién entonces aparece [NUEVA], que es la
    // que además invalida el runId.
    await w.find('.chat__cabecera .fantasma').trigger('click')
    await flushPromises()
    await w.find('.chat__cabecera .fantasma').trigger('click')

    // Corrida 2: su resultado es el único que queda en el hilo.
    fetchMock.mockImplementation((_url: string, init?: RequestInit) =>
      Promise.resolve(sse([
        frame('token', { texto: 'nuevo' }),
        frame('result', { tipo: 'respuesta', texto: 'nuevo', fallback: false }),
        frame('done', { ms: 1 }),
      ], 0, init)),
    )
    await enviar(w, 'segundo')
    await new Promise(r => setTimeout(r, 80))
    await flushPromises()

    expect(w.text()).toContain('nuevo')
    expect(w.text()).not.toContain('VIEJO')
    // La corrida vieja no dejó pasos ni texto colgando.
    expect(w.find('.msg--viva').exists()).toBe(false)
  })

  it('un evento error del backend se muestra como [ERROR]', async () => {
    const { w } = await montarCon([
      frame('error', { message: 'El modelo está procesando la petición.' }),
    ])

    await enviar(w)
    await flushPromises()

    expect(w.find('.msg__error').text()).toContain('[ERROR]')
    expect(w.find('.msg__error').text()).toContain('El modelo está procesando la petición.')
    expect(w.find('.estado__etiqueta').text()).toBe('[ERROR]')
  })
})

describe('ConversacionView — resultado del backend', () => {
  it('una aclaración NO se trata como error de conexión', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'aclaracion',
        pregunta: '¿Qué te gustaría registrar o consultar?',
        motivo: 'intención no soportada aún',
        fallback: false,
      }),
      frame('done', { ms: 8 }),
    ])

    await enviar(w, 'xyz')
    await flushPromises()

    expect(w.text()).toContain('¿Qué te gustaría registrar o consultar?')
    expect(w.find('.msg__error').exists()).toBe(false)
    expect(w.find('.estado__etiqueta').text()).toBe('[LISTO]')
  })

  it('una respuesta de respaldo se marca como tal', async () => {
    const { w } = await montarCon([
      frame('result', { tipo: 'respuesta', texto: 'Llevas 4035 kcal hoy.', fallback: true }),
      frame('done', { ms: 9 }),
    ])

    await enviar(w, '¿cuántas calorías llevo?')
    await flushPromises()

    expect(w.text()).toContain('RESPUESTA DE RESPALDO')
  })

  it('el bot no registra: devuelve los datos y el botón a la vista', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'redirigir',
        texto: 'Listo: 2 alimentos. Revisá y guardá vos en el formulario.',
        redireccion: {
          vista: 'registrar',
          etiqueta: 'IR A REGISTRAR',
          items: [
            { nombre: 'Huevo', cantidad: 2, unidad: 'U' },
            { nombre: 'Tostada', cantidad: 2, unidad: 'U' },
          ],
        },
      }),
      frame('done', { ms: 210 }),
    ])

    await enviar(w, 'comí dos huevos y dos tostadas')
    await flushPromises()

    const btn = w.find('.redirige')
    expect(btn.exists()).toBe(true)
    expect(btn.text()).toBe('[IR A REGISTRAR]')
    // Los datos que se llevan son los reales, no un rótulo genérico.
    // Con espacio entre cantidad y unidad: "2U" pegado se leía como dos cifras.
    expect(w.find('.redirige__detalle').text()).toBe('Huevo 2 U · Tostada 2 U')
    // El bot nunca dice que registró: eso lo hace la vista, con el botón.
    expect(w.text()).not.toContain('registrada')
  })

  it('el detalle de la redirección formatea decimales sin ruido', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'redirigir',
        texto: 'Listo.',
        redireccion: {
          vista: 'registrar',
          etiqueta: 'IR A REGISTRAR',
          items: [
            { nombre: 'Arroz', cantidad: 150, unidad: 'G' },
            { nombre: 'Aceite', cantidad: 0.5, unidad: 'CUCHARA' },
          ],
        },
      }),
      frame('done', { ms: 90 }),
    ])

    await enviar(w, 'arroz y media cucharada de aceite')
    await flushPromises()

    // Entero sin decimales ("150", no "150,00") y real con los justos ("0,5").
    const d = w.find('.redirige__detalle').text()
    expect(d).toContain('Arroz 150 G')
    expect(d).toContain('Aceite 0.5 CUCHARA')
    expect(d).not.toContain('150.00')
  })

  it('el botón navega a la vista con los items en la query', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'redirigir',
        texto: 'Listo.',
        redireccion: {
          vista: 'registrar',
          etiqueta: 'IR A REGISTRAR',
          items: [{ nombre: 'Arroz', cantidad: 150, unidad: 'G' }],
        },
      }),
      frame('done', { ms: 100 }),
    ])

    await enviar(w, 'comí arroz')
    await flushPromises()

    await w.find('.redirige').trigger('click')
    await flushPromises()

    expect(__push).toHaveBeenCalledWith({
      path: '/registrar',
      query: { nombre: ['Arroz'], cantidad: ['150'], unidad: ['G'] },
    })
  })

  it('una redirección sin items (objetivo, mediciones) no inventa datos', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'redirigir',
        texto: 'Te llevo a la pantalla para que la confirmes vos.',
        redireccion: { vista: 'objetivo', etiqueta: 'CALCULAR MI OBJETIVO', items: [] },
      }),
      frame('done', { ms: 80 }),
    ])

    await enviar(w, 'calculá mi meta')
    await flushPromises()

    expect(w.find('.redirige').exists()).toBe(true)
    expect(w.find('.redirige__detalle').exists()).toBe(false)
  })

  it('una vista desconocida no navega a ningún lado', async () => {
    const { w } = await montarCon([
      frame('result', {
        tipo: 'redirigir',
        texto: 'x',
        redireccion: { vista: '../../admin', etiqueta: 'IR', items: [] },
      }),
      frame('done', { ms: 50 }),
    ])

    await enviar(w, 'algo')
    await flushPromises()
    await w.find('.redirige').trigger('click')
    await flushPromises()

    expect(__push).not.toHaveBeenCalled()
  })
})

describe('ConversacionView — interacción', () => {
  it('las sugerencias se deshabilitan mientras hay una respuesta en curso', async () => {
    const { w } = await montarCon([
      frame('result', { tipo: 'respuesta', texto: 'ok', fallback: false }),
      frame('done', { ms: 1 }),
    ], 30)

    await enviar(w)
    await new Promise(r => setTimeout(r, 5))

    for (const chip of w.findAll('.chip')) {
      expect(chip.attributes('disabled')).toBeDefined()
    }

    await new Promise(r => setTimeout(r, 80))
    await flushPromises()

    expect(w.findAll('.chip').every(c => c.attributes('disabled') === undefined)).toBe(true)
  })

  it('una segunda acción mientras corre no duplica mensajes', async () => {
    const fetchMock = vi.fn().mockImplementation(() => sse([
      frame('token', { texto: 'lento' }),
    ], 50))
    vi.stubGlobal('fetch', fetchMock)
    localStorage.setItem('nutriologo_token', 'tok')
    const w = mount(ConversacionView)

    await enviar(w, 'uno')
    await new Promise(r => setTimeout(r, 10))
    await w.find('form.composer').trigger('submit.prevent')
    await w.find('form.composer').trigger('submit.prevent')
    await flushPromises()

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(w.findAll('.msg--user')).toHaveLength(1)

    await new Promise(r => setTimeout(r, 120))
  })
})