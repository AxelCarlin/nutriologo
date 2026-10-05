import { describe, expect, it } from 'vitest'

import { leerSse, type EventoSse } from './sse'
import { ETIQUETA_ESTADO, PATRONES, patronACeldas, type UiEstado } from './useDotMatrix'

/** Construye un `Response` cuyo cuerpo emite los frames indicados, en orden. */
function respuestaSse(frames: string[], delayMs = 0): Response {
  const cuerpo = new ReadableStream<Uint8Array>({
    async start(control) {
      const codificador = new TextEncoder()
      for (const f of frames) {
        if (delayMs) await new Promise((r) => setTimeout(r, delayMs))
        control.enqueue(codificador.encode(f))
      }
      control.close()
    },
  })

  return new Response(cuerpo as unknown as BodyInit, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}

const frame = (evento: string, datos: unknown) =>
  `event: ${evento}\ndata: ${JSON.stringify(datos)}\n\n`

async function recolectar(res: Response): Promise<EventoSse[]> {
  const salida: EventoSse[] = []
  for await (const ev of leerSse(res)) salida.push(ev)
  return salida
}

describe('leerSse', () => {
  it('traduce los frames del backend a eventos con nombre y datos', async () => {
    const res = respuestaSse([
      frame('paso', { nombre: 'interpretacion', estado: 'start', detalle: 'Interpretando tu mensaje' }),
      frame('paso', { nombre: 'interpretacion', estado: 'ok', detalle: 'Interpretando tu mensaje' }),
      frame('token', { texto: 'Hola' }),
      frame('result', { tipo: 'respuesta', texto: 'Hola', fallback: false }),
      frame('done', { ms: 4210 }),
    ])

    const evs = await recolectar(res)

    expect(evs.map(e => e.evento)).toEqual(['paso', 'paso', 'token', 'result', 'done'])
    expect(evs[0].datos.estado).toBe('start')
    expect(evs[2].datos.texto).toBe('Hola')
    expect(evs[3].datos.tipo).toBe('respuesta')
    expect(evs[4].datos.ms).toBe(4210)
  })

  it('reconstituye un frame partido entre varios chunks', async () => {
    const completo = frame('token', { texto: 'Llevas 1200 kcal' })
    const res = respuestaSse([completo.slice(0, 12), completo.slice(12)])

    const evs = await recolectar(res)

    expect(evs).toHaveLength(1)
    expect(evs[0].evento).toBe('token')
    expect(evs[0].datos.texto).toBe('Llevas 1200 kcal')
  })

  it('acepta CRLF y descarta comentarios de keep-alive', async () => {
    const res = respuestaSse([': keep-alive\r\n\r\n', 'event: done\r\ndata: {"ms":7}\r\n\r\n'])

    const evs = await recolectar(res)

    expect(evs).toHaveLength(1)
    expect(evs[0].evento).toBe('done')
    expect(evs[0].datos.ms).toBe(7)
  })

  it('lanza AbortError cuando se corta el stream', async () => {
    const ctrl = new AbortController()
    const res = respuestaSse([frame('token', { texto: 'parcial' })])
    const salida: EventoSse[] = []

    try {
      for await (const ev of leerSse(res, ctrl.signal)) {
        salida.push(ev)
        ctrl.abort()
      }
      throw new Error('debería haber lanzado')
    } catch (e) {
      expect((e as Error).name).toBe('AbortError')
    }

    expect(salida).toHaveLength(1)
  })
})

describe('useDotMatrix: patrones por estado', () => {
  it('devuelve 25 celdas por estado y tick', () => {
    const estados = Object.keys(PATRONES) as UiEstado[]
    for (const e of estados) {
      expect(patronACeldas(e, 0)).toHaveLength(25)
      expect(patronACeldas(e, 7)).toHaveLength(25)
    }
  })

  it('idle: solo el punto central, y parpadea', () => {
    const apagado = patronACeldas('idle', 0)
    expect(apagado[12]).toBe(true)
    expect(apagado.filter(Boolean)).toHaveLength(1)
    // (t >> 2) % 2 === 0 → se apaga en los ticks 4..7, no en el 8.
    expect(patronACeldas('idle', 4)[12]).toBe(false)
    expect(patronACeldas('idle', 8)[12]).toBe(true)
  })

  it('pensando: el anillo gira (4 celdas, siempre en el perímetro)', () => {
    const anillo = new Set([0, 1, 2, 3, 4, 9, 14, 19, 24, 23, 22, 21, 20, 15, 10, 5])
    for (const t of [0, 1, 5, 9, 15]) {
      const encendidas = patronACeldas('pensando', t)
        .map((on, i) => (on ? i : -1))
        .filter(i => i >= 0)
      expect(encendidas).toHaveLength(4)
      for (const i of encendidas) expect(anillo.has(i)).toBe(true)
    }
  })

  it('respondiendo: exactamente una fila encendida por tick', () => {
    for (const t of [0, 1, 4]) {
      const filas = new Set<number>()
      patronACeldas('respondiendo', t).forEach((on, i) => {
        if (on) filas.add(Math.floor(i / 5))
      })
      expect([...filas]).toEqual([t % 5])
    }
  })

  it('herramienta: barrido diagonal (banda de 5 celdas por tick)', () => {
    for (const t of [0, 3]) {
      const encendidas = patronACeldas('herramienta', t)
        .map((on, i) => (on ? i : -1))
        .filter(i => i >= 0)
      // (fila + columna) % 5 === t % 5 → una anti-diagonal de 5 celdas.
      expect(encendidas).toHaveLength(5)
      for (const i of encendidas) {
        expect((Math.floor(i / 5) + (i % 5)) % 5).toBe(t % 5)
      }
    }
    // Cada tick mueve la banda: no se repite el mismo dibujo.
    expect(patronACeldas('herramienta', 0)).not.toEqual(patronACeldas('herramienta', 1))
  })

  it('detenido: fila central fija, independiente del tick', () => {
    expect(patronACeldas('detenido', 0)).toEqual(patronACeldas('detenido', 13))
    expect(patronACeldas('detenido', 0).filter(Boolean)).toHaveLength(5)
  })

  it('error: solo la X parpadea', () => {
    const encendidas = patronACeldas('error', 0)
      .map((on, i) => (on ? i : -1))
      .filter(i => i >= 0)
    // Las dos diagonales del 5×5 se cruzan en el centro: 9 celdas, no 12.
    expect(encendidas).toEqual([0, 4, 6, 8, 12, 16, 18, 20, 24])
    // Se apaga en los ticks 4..7 ((t >> 2) % 2 === 1).
    expect(patronACeldas('error', 4).every(on => !on)).toBe(true)
    expect(patronACeldas('error', 8).filter(Boolean)).toHaveLength(9)
  })

  it('listo: el check se dibuja progresivamente y se reinicia', () => {
    const check = [10, 16, 12, 8, 4]
    for (const t of [0, 1, 3, 4]) {
      const encendidas = patronACeldas('listo', t)
        .map((on, i) => (on ? i : -1))
        .filter(i => i >= 0)
      // Mismo conjunto que el check truncado, sin importar el orden de dibujo.
      expect([...encendidas].sort((a, b) => a - b)).toEqual([...check.slice(0, t + 1)].sort((a, b) => a - b))
    }
    // Con las 5 celdas el dibujo queda completo y estable.
    expect(patronACeldas('listo', 5).filter(Boolean)).toHaveLength(5)
    expect(patronACeldas('listo', 5)).toEqual(patronACeldas('listo', 13))
  })

  it('la etiqueta es la misma en todos los estados y en español', () => {
    for (const clave of Object.keys(ETIQUETA_ESTADO) as UiEstado[]) {
      const texto = ETIQUETA_ESTADO[clave]
      expect(texto).toMatch(/^\[[A-ZÁÉÍÓÚÑ. ]+\]$/)
    }
    expect(ETIQUETA_ESTADO.idle).toBe('[IDLE]')
    expect(ETIQUETA_ESTADO.pensando).toBe('[PENSANDO...]')
    expect(ETIQUETA_ESTADO.detenido).toBe('[DETENIDO]')
    expect(ETIQUETA_ESTADO.herramienta).toBe('[USANDO HERRAMIENTA]')
  })
})