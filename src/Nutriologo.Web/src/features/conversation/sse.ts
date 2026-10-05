/**
 * Lector de Server-Sent Events para el chat.
 *
 * No inventa nada: traduce el flujo real del backend a eventos con nombre.
 * Formato soportado (el que emite `ChatEndpoints`):
 *
 *   event: paso
 *   data: {"nombre":"respuesta","estado":"start","detalle":"Redactando tu respuesta"}
 *
 *   event: token
 *   data: {"texto":"Llevas "}
 *
 *   event: result
 *   data: {"tipo":"respuesta","texto":"...","fallback":false}
 *
 *   event: done
 *   data: {"ms":4210}
 */

export interface EventoSse<T = any> {
  /** Nombre del evento (`paso`, `token`, `result`, `error`, `done`). */
  evento: string
  /** `data:` ya parseado como JSON. `null` si venía vacío o no era JSON. */
  datos: T
}

/**
 * Itera los eventos de un `Response` con cuerpo de texto.
 * Lanza `AbortError` si se corta el stream (botón Detener / tecla Esc).
 */
export async function* leerSse<T = any>(
  respuesta: Response,
  signal?: AbortSignal
): AsyncGenerator<EventoSse<T>> {
  const cuerpo = respuesta.body
  if (!cuerpo) throw new Error('La respuesta no tiene cuerpo para leer')

  const lector = cuerpo.getReader()
  const decodificador = new TextDecoder()
  let buffer = ''

  try {
    while (true) {
      if (signal?.aborted) throw abortError()

      const { done, value } = await lector.read()
      if (done) break

      buffer += decodificador.decode(value, { stream: true })

      // Un frame SSE termina en línea vacía. Puede haber varias por chunk.
      let corte = buffer.indexOf('\n\n')
      while (corte !== -1) {
        const frame = buffer.slice(0, corte)
        buffer = buffer.slice(corte + 2)
        const emitido = parsearFrame<T>(frame)
        if (emitido) yield emitido
        corte = buffer.indexOf('\n\n')
      }
    }

    // Último frame sin línea vacía final (el backend cerró el stream).
    const ultimo = parsearFrame<T>(buffer)
    if (ultimo) yield ultimo
  } finally {
    lector.releaseLock?.()
  }
}

/** Construye el mismo error que produce `AbortController.abort()` en fetch. */
function abortError(): Error {
  const e = new Error('La operación fue abortada')
  e.name = 'AbortError'
  return e
}

/**
 * Convierte un frame en evento. `null` si no hay nada que emitir
 * (comentarios `:` o frames sin `data:`).
 */
function parsearFrame<T>(frame: string): EventoSse<T> | null {
  if (!frame.trim()) return null

  let evento = 'message'
  const lineas: string[] = []

  for (const cruda of frame.split('\n')) {
    const linea = cruda.replace(/\r$/, '')
    if (!linea || linea.startsWith(':')) continue // comentario / keep-alive

    const corte = linea.indexOf(':')
    const campo = corte === -1 ? linea : linea.slice(0, corte)
    // tras ':' hay un espacio opcional que NO pertenece al valor
    const valor = corte === -1 ? '' : linea.slice(corte + 1).replace(/^ /, '')

    if (campo === 'event') evento = valor
    else if (campo === 'data') lineas.push(valor)
  }

  if (lineas.length === 0) return null

  const crudo = lineas.join('\n')
  let datos: any = null
  try {
    datos = JSON.parse(crudo)
  } catch {
    datos = crudo
  }

  return { evento, datos }
}