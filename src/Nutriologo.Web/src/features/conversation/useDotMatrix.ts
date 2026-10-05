/**
 * Indicador de estado 5×5 (Nothing).
 *
 * Reglas duras del diseño:
 * - Una sola instancia de la animación, un solo `setInterval`, creado al montar
 *   el componente y destruido al desmontarlo. Nunca uno por mensaje.
 * - El patrón depende EXCLUSIVAMENTE del estado. No hay temporizadores que
 *   avancen pasos ni porcentajes inventados.
 * - Se pausa con la pestaña oculta y no anima nada con `prefers-reduced-motion`.
 */

import { computed, onMounted, onUnmounted, ref, type Ref } from 'vue'

export type UiEstado =
  | 'idle'
  | 'escuchando'
  | 'pensando'
  | 'herramienta'
  | 'respondiendo'
  | 'listo'
  | 'detenido'
  | 'error'

/** Etiquetas: única fuente de verdad del texto del estado. */
export const ETIQUETA_ESTADO: Record<UiEstado, string> = {
  idle: '[IDLE]',
  escuchando: '[ESCUCHANDO...]',
  pensando: '[PENSANDO...]',
  herramienta: '[USANDO HERRAMIENTA]',
  respondiendo: '[RESPONDIENDO]',
  listo: '[LISTO]',
  detenido: '[DETENIDO]',
  error: '[ERROR]',
}

/** Anillo perimetral en sentido horario desde la esquina superior izquierda. */
const RING = [0, 1, 2, 3, 4, 9, 14, 19, 24, 23, 22, 21, 20, 15, 10, 5]
/** Checkmark progresivo. */
const CHK = [10, 16, 12, 8, 4]

export type Patron = (t: number, i: number) => boolean

/** Patrones puros, uno por estado. `t` = tick, `i` = índice 0..24 (fila-major). */
export const PATRONES: Record<UiEstado, Patron> = {
  // Punto central con parpadeo lento.
  idle: (t, i) => i === 12 && (t >> 2) % 2 === 0,

  // Ecualizador: onda vertical por columnas.
  escuchando: (t, i) => {
    const c = i % 5
    const r = (i / 5) | 0
    return 4 - r < Math.round(2.5 + 2 * Math.sin(t * 0.9 + c * 1.9))
  },

  // Anillo con cola de 4 segmentos.
  pensando: (t, i) => {
    const p = RING.indexOf(i)
    return p >= 0 && ((((p - t) % 16) + 16) % 16) < 4
  },

  // Barrido diagonal.
  herramienta: (t, i) => ((((i / 5) | 0) + (i % 5)) % 5) === t % 5,

  // Escaneo de filas.
  respondiendo: (t, i) => ((i / 5) | 0) === t % 5,

  // Check progresivo.
  listo: (t, i) => {
    const p = CHK.indexOf(i)
    return p >= 0 && p <= t % 14
  },

  // Fila central fija (el estado no avanza con el tiempo).
  detenido: (_t, i) => i >= 10 && i < 15,

  // X parpadeante.
  error: (t, i) => {
    const fila = (i / 5) | 0
    const col = i % 5
    const enX = fila === col || fila + col === 4
    return enX && (t >> 2) % 2 === 0
  },
}

export const CELDAS = 25

/** Vector booleano de 25 para el estado y tick dados. */
export function patronACeldas(estado: UiEstado, tick: number): boolean[] {
  const fn = PATRONES[estado] ?? PATRONES.idle
  const out: boolean[] = new Array(CELDAS)
  for (let i = 0; i < CELDAS; i++) out[i] = fn(tick, i)
  return out
}

export function movimientoReducido(): boolean {
  return typeof window !== 'undefined'
    && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true
}

/**
 * Dot-matrix 5×5 con UN interval global.
 *
 * Devuelve `celdas` (25 booleanos reactivos), `tick` (para cronómetros que
 * quieren un solo timer) y `pausado`.
 */
export function useDotMatrix(estado: Ref<UiEstado>, intervaloMs = 130) {
  const tick = ref(0)
  const pausado = ref(false)
  let timer: ReturnType<typeof setInterval> | null = null

  const celdas = computed(() => patronACeldas(estado.value, tick.value))

  function tickear() {
    // Con la pestaña oculta no se anima: el navegador no está mostrando nada.
    if (typeof document !== 'undefined' && document.hidden) return
    tick.value++
  }

  function alCambiarVisibilidad() {
    pausado.value = typeof document !== 'undefined' && document.hidden
  }

  onMounted(() => {
    // prefers-reduced-motion: ni siquiera se crea el timer. El patrón queda
    // congelado en su frame 0 (estático pero legible).
    if (movimientoReducido()) return

    document.addEventListener('visibilitychange', alCambiarVisibilidad)
    alCambiarVisibilidad()
    timer = setInterval(tickear, intervaloMs)
  })

  onUnmounted(() => {
    if (timer) clearInterval(timer)
    timer = null
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', alCambiarVisibilidad)
    }
  })

  return { celdas, tick, pausado }
}