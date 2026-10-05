/**
 * Markdown mínimo para las respuestas del asistente.
 *
 * Por qué no una librería: lo que llega son listas, negritas y saltos de línea.
 * Una dependencia entera para eso es más superficie de la que hace falta.
 *
 * Por qué NO usa `v-html`: el texto viene del modelo y se persiste en la base.
 * Renderizarlo como HTML abriría la puerta a que una respuesta traiga
 * `<script>` o un `onerror=`. Este módulo devuelve una ESTRUCTURA y la vista la
 * pinta con nodos de Vue, que escapan todo por definición.
 *
 * Lo que soporta:
 *   · Párrafos (línea en blanco = corte)
 *   · Listas con `-`, `*`, `•` y `1.`
 *   · **negrita** y *cursiva*
 *   · `código` en línea
 *
 * Lo que NO hace, a propósito: encabezados, tablas, HTML, enlaces. Si aparece
 * algo así, se muestra como texto literal en vez de inventar un formato.
 */

export type Nodo =
  | { tipo: 'parrafo'; inline: Inline[] }
  | { tipo: 'lista'; ordenada: boolean; items: Inline[][] }

export type Inline =
  | { tipo: 'texto'; valor: string }
  | { tipo: 'fuerte'; valor: string }
  | { tipo: 'em'; valor: string }
  | { tipo: 'codigo'; valor: string }

const BULLET = /^[-*•]\s+(.*)$/
const ORDENED = /^(\d+)[.)]\s+(.*)$/

/** Trocea el texto en párrafos y listas. */
export function parsearMarkdown(texto: string): Nodo[] {
  const lineas = texto.replace(/\r\n/g, '\n').split('\n')
  const nodos: Nodo[] = []
  let parrafo: string[] = []
  let lista: { ordenada: boolean; items: string[] } | null = null

  const cerrarParrafo = () => {
    if (!parrafo.length) return
    nodos.push({ tipo: 'parrafo', inline: parsearInline(parrafo.join(' ')) })
    parrafo = []
  }

  const cerrarLista = () => {
    if (!lista) return
    nodos.push({
      tipo: 'lista',
      ordenada: lista.ordenada,
      items: lista.items.map(i => parsearInline(i)),
    })
    lista = null
  }

  for (const cruda of lineas) {
    const linea = cruda.trim()

    if (!linea) {
      cerrarParrafo()
      cerrarLista()
      continue
    }

    const item = BULLET.exec(linea)
    const num = !item ? ORDENED.exec(linea) : null

    if (item || num) {
      cerrarParrafo()
      const contenido = item ? item[1]! : num![2]!
      const ordenada = Boolean(num)
      // Una lista numerada después de una de viñetas es una lista nueva.
      if (!lista || lista.ordenada !== ordenada) {
        cerrarLista()
        lista = { ordenada, items: [] }
      }
      lista.items.push(contenido)
      continue
    }

    // Un elemento de lista sin guion previo también cuenta como párrafo:
    // se acumula y no se inventa formato.
    cerrarLista()
    parrafo.push(linea)
  }

  cerrarParrafo()
  cerrarLista()
  return nodos
}

/** Resalta **fuerte**, *em* y `código` dentro de un mismo bloque. */
function parsearInline(texto: string): Inline[] {
  const salida: Inline[] = []
  // El orden importa: primero `código` (literal, sin interpretar dentro),
  // después **fuerte**, y *em* al final para no partir los asteriscos de la
  // negrita.
  const patron = /`([^`]+)`|\*\*([^*]+)\*\*|\*([^*]+)\*/g

  let ultimo = 0
  let m: RegExpExecArray | null

  while ((m = patron.exec(texto)) !== null) {
    if (m.index > ultimo) {
      salida.push({ tipo: 'texto', valor: texto.slice(ultimo, m.index) })
    }

    if (m[1] !== undefined) salida.push({ tipo: 'codigo', valor: m[1] })
    else if (m[2] !== undefined) salida.push({ tipo: 'fuerte', valor: m[2] })
    else if (m[3] !== undefined) salida.push({ tipo: 'em', valor: m[3] })

    ultimo = m.index + m[0].length
  }

  if (ultimo < texto.length) {
    salida.push({ tipo: 'texto', valor: texto.slice(ultimo) })
  }

  return salida.length ? salida : [{ tipo: 'texto', valor: texto }]
}