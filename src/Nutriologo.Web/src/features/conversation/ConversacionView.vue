<template>
  <div class="chat" :data-estado="uiState">
    <!-- ══════ ENCABEZADO: único indicador + única etiqueta de estado ══════ -->
    <header class="chat__cabecera">
      <div class="col chat__cabecera-int">
        <!-- role="status": el ÚNICO live region del chat -->
        <div class="estado" role="status" :aria-label="`Estado del asistente: ${etiqueta}`">
          <span class="dotmatrix" aria-hidden="true">
            <span
              v-for="(on, i) in celdas"
              :key="i"
              class="dotmatrix__punto"
              :class="{ 'dotmatrix__punto--on': on }"
            />
          </span>
          <span class="estado__etiqueta">{{ etiqueta }}</span>
        </div>

        <button class="fantasma" type="button" @click="nueva" aria-label="Nueva conversación">
          [NUEVA]
        </button>
      </div>
    </header>

    <!-- ══════ HILO ══════ -->
    <main ref="hiloRef" class="col chat__hilo" role="log" :aria-busy="enviando" @scroll.passive="onScroll">
      <!-- Estado vacío -->
      <div v-if="!hayMensajes && !msgViva" class="vacio">
        <h1 class="vacio__titulo">¿qué comiste hoy?</h1>
        <p class="vacio__sub">contame qué comiste y te llevo al formulario, o preguntame lo que quieras</p>
        <div class="chips" role="group" aria-label="Sugerencias">
          <button
            v-for="s in sugerencias"
            :key="s.texto"
            type="button"
            class="chip"
            :disabled="enviando"
            @click="enviarTexto(s.envio)"
          >
            {{ s.texto }}
          </button>
        </div>
      </div>

      <ol v-else class="hilo">
        <li
          v-for="m in mensajes"
          :key="m.id"
          class="msg"
          :class="m.rol === 'user' ? 'msg--user' : 'msg--agent'"
        >
          <!-- ───────── AGENTE: texto plano, sin caja ───────── -->
          <template v-if="m.rol !== 'user'">
            <p class="msg__firma">
              NUTRIÓLOGO<span v-if="m.fallback" class="msg__falla"> · RESPUESTA DE RESPALDO</span>
            </p>

            <!-- Pasos reales. Dentro del mensaje, nunca sueltos en el hilo. -->
            <details v-if="m.pasos.length" class="pasos" :open="!m.pasosColapsados">
              <summary class="pasos__resumen">{{ resumenPasos(m) }}</summary>
              <ul class="pasos__lista">
                <li v-for="p in m.pasos" :key="p.nombre" class="paso" :data-estado="p.estado">
                  <span class="paso__marca" aria-hidden="true" />
                  <span class="paso__nombre">{{ p.etiqueta }}</span>
                  <span class="paso__estado">{{ marcaEstado(p.estado) }}</span>
                  <span v-if="p.ms !== undefined" class="paso__ms">{{ (p.ms / 1000).toFixed(1) }}s</span>
                </li>
              </ul>
            </details>

            <div class="msg__texto"><MdTexto :texto="m.texto" /></div>

            <!-- El bot no escribe: deja el botón a la vista donde se guarda. -->
            <div v-if="m.redireccion" class="msg__redirige">
              <button class="redirige" type="button" @click="irA(m.redireccion)">
                {{ `[${m.redireccion.etiqueta}]` }}
              </button>
              <span v-if="m.redireccion.items.length" class="redirige__detalle">
                {{ resumenItems(m.redireccion.items) }}
              </span>
            </div>

            <!-- Error persistido: el aviso no desaparece al cerrar el mensaje vivo. -->
            <p v-if="m.error" class="msg__error">[ERROR] {{ m.error }}</p>

            <!-- Meta y acciones: solo valores medidos -->
            <div v-if="m.meta" class="msg__pie">
              <span class="msg__meta-linea">{{ m.meta }}</span>
              <span class="msg__acciones">
                <button class="fantasma" type="button" @click="copiar(m.texto)">COPIAR</button>
                <button class="fantasma" type="button" @click="regenerar(m)">REGENERAR</button>
                <button
                  class="fantasma"
                  type="button"
                  :aria-pressed="m.feedback === true"
                  @click="marcar(m, true)"
                >ÚTIL</button>
                <button
                  class="fantasma"
                  type="button"
                  :aria-pressed="m.feedback === false"
                  @click="marcar(m, false)"
                >NO ÚTIL</button>
              </span>
            </div>
          </template>

          <!-- ───────── USUARIO: la única caja ───────── -->
          <template v-else>
            <p class="msg__burbuja">{{ m.texto }}</p>
          </template>
        </li>

        <!-- ───────── MENSAJE EN CURSO: dueño de sus pasos, meta y cursor ───────── -->
        <li v-if="msgViva" class="msg msg--agent msg--viva" :aria-label="'Respuesta en curso'">
          <p class="msg__firma">NUTRIÓLOGO</p>

          <details v-if="msgViva.pasos.length" class="pasos" :open="!msgViva.pasosColapsados">
            <summary class="pasos__resumen">{{ resumenPasos(msgViva) }}</summary>
            <ul class="pasos__lista">
              <li v-for="p in msgViva.pasos" :key="p.nombre" class="paso" :data-estado="p.estado">
                <span class="paso__marca" aria-hidden="true" />
                <span class="paso__nombre">{{ p.etiqueta }}</span>
                <span class="paso__estado">{{ marcaEstado(p.estado) }}</span>
                <span v-if="p.ms !== undefined" class="paso__ms">{{ (p.ms / 1000).toFixed(1) }}s</span>
              </li>
            </ul>
          </details>

          <!-- Línea honesta mientras no llega el primer token -->
          <p v-if="!msgViva.texto" class="msg__texto msg__texto--espera">
            [PENSANDO...] {{ transcurrido.toFixed(1) }}S
          </p>

          <div v-else class="msg__texto">
            <MdTexto :texto="msgViva.texto" />
            <span class="cursor" aria-hidden="true">█</span>
          </div>

          <!-- Sin role="alert": el único live region del chat es la etiqueta
               de estado, que ya pasa a [ERROR] y se anuncia. -->
          <p v-if="msgViva.error" class="msg__error">
            [ERROR] {{ msgViva.error }}
          </p>
        </li>
      </ol>

      </main>

    <!-- ══════ IR ABAJO: fuera del hilo, en su propia capa ══════
         Dentro del <main> ocupaba alto en flujo y se superponía al texto. -->
    <button
      v-if="mostrarBajar"
      class="bajar"
      type="button"
      aria-label="Ir a los mensajes nuevos"
      @click="alFinal(true)"
    >[BAJAR]</button>

    <!-- ══════ COMPOSER: misma columna, mismo ancho, mismo padding ══════ -->
    <footer class="chat__pie">
      <form class="col composer" @submit.prevent="enviar()">
        <!-- Chips ARRIBA del input, como en la referencia: funcionan como
             atajos de entrada, no como un bloque aparte bajo el compositor. -->
        <div class="composer__chips" role="group" aria-label="Sugerencias rápidas">
          <button
            v-for="s in chips"
            :key="s"
            type="button"
            class="chip"
            :disabled="enviando"
            @click="usarChip(s)"
          >{{ s }}</button>
        </div>

        <div class="composer__envoltura" :data-bloqueado="enviando">
          <textarea
            ref="areaRef"
            v-model="texto"
            class="composer__area"
            rows="1"
            :placeholder="placeholder"
            aria-label="Mensaje al asistente nutricional"
            @input="crecerArea"
            @keydown.enter.exact.prevent="enviar()"
            @keydown.shift.enter.stop
            @keydown.esc.prevent="detener()"
          />

          <button
            v-if="enviando"
            class="composer__enviar"
            type="button"
            aria-label="Detener la respuesta"
            @click="detener()"
          >■</button>
          <button
            v-else
            class="composer__enviar"
            type="submit"
            :disabled="!texto.trim()"
            aria-label="Enviar mensaje"
          >↑</button>
        </div>

        <p class="composer__ayuda">ENTER envía · SHIFT+ENTER salta de línea · ESC detiene</p>
      </form>
    </footer>
  </div>
</template>

<script setup lang="ts">
import {
  computed,
  nextTick,
  onMounted,
  onUnmounted,
  ref,
} from 'vue'

import MdTexto from './MdTexto.vue'
import { leerSse } from './sse'
import { useRouter } from 'vue-router'
import {
  ETIQUETA_ESTADO,
  useDotMatrix,
  type UiEstado,
} from './useDotMatrix'

// ════════════════════════════════════════════════════════════
// TIPOS
// ════════════════════════════════════════════════════════════

type EstadoPaso = 'corriendo' | 'hecho' | 'fallo'

interface Paso {
  /** Clave estable para emparejar start/ok/fail del mismo paso. */
  nombre: string
  /** Etiqueta legible que envía el backend. */
  etiqueta: string
  estado: EstadoPaso
  /** Duración medida en el cliente entre start y el cierre. */
  ms?: number
}

interface Mensaje {
  id: number
  rol: 'user' | 'assistant'
  texto: string
  pasos: Paso[]
  pasosColapsados: boolean
  meta?: string
  fallback?: boolean
  feedback?: boolean | null
  error?: string
  /** Marca de respuesta detenida (texto parcial conservado). */
  detenido?: boolean
  /** Botón a la vista donde el usuario guarda lo que el chat entendió. */
  redireccion?: Redireccion
}

interface TurnoHistorial {
  rol: string
  texto: string
}

/** Un alimento entendido por el chat, listo para precargar el formulario. */
interface ItemRedirigido {
  nombre: string
  cantidad: number
  unidad: string
}

/**
 * El bot no escribe: entrega los datos y la vista donde se guardan.
 * `vista` es el nombre de la ruta del cliente, nunca una URL absoluta.
 */
interface Redireccion {
  vista: string
  etiqueta: string
  items: ItemRedirigido[]
}

interface ResultadoBackend {
  tipo?: string
  texto?: string | null
  pregunta?: string | null
  mensaje?: string | null
  fallback?: boolean
  redireccion?: Redireccion | null
}

// ════════════════════════════════════════════════════════════
// ESTADO DE UI: una sola fuente de verdad
// ════════════════════════════════════════════════════════════

const uiState = ref<UiEstado>('idle')

/** Único punto de escritura del estado. Nada más asigna `uiState.value`. */
function setState(k: UiEstado) {
  uiState.value = k
}

const etiqueta = computed(() => ETIQUETA_ESTADO[uiState.value])

// Único indicador 5×5 y único setInterval de la vista.
const { celdas, tick } = useDotMatrix(uiState)

// ════════════════════════════════════════════════════════════
// DATOS DE LA VISTA
// ════════════════════════════════════════════════════════════

const mensajes = ref<Mensaje[]>([])
const msgViva = ref<Mensaje | null>(null)
const texto = ref('')
const enviando = ref(false)
const mostrarBajar = ref(false)

const areaRef = ref<HTMLTextAreaElement | null>(null)
const hiloRef = ref<HTMLElement | null>(null)

const placeholder = 'escribe tu mensaje'

const router = useRouter()

const sugerencias = [
  { texto: 'ARMAR MI DESAYUNO', envio: 'comí dos huevos y dos tostadas' },
  { texto: '¿CÓMO VOY HOY?', envio: '¿cómo voy hoy?' },
  { texto: '¿QUÉ COMO DE CENA?', envio: '¿qué puedo cenar hoy?' },
]

const chips = ['¿cómo voy hoy?', '¿qué ceno?', '¿qué comí?']

// ════════════════════════════════════════════════════════════
// CONTROL DE CONCURRENCIA
// ════════════════════════════════════════════════════════════

/** Id incremental: cualquier evento de una corrida vieja se ignora. */
let runId = 0
let ctrl: AbortController | null = null
let idleTimer: ReturnType<typeof setTimeout> | null = null
let seq = 1
let t0 = 0

/** Cronómetro real. Reusa el tick global del dot-matrix: sin timer propio. */
const transcurrido = computed(() => {
  void tick.value // depende del único intervalo de la vista
  if (!t0) return 0
  return (performance.now() - t0) / 1000
})

const hayMensajes = computed(() => mensajes.value.length > 0)

function token(): string {
  return localStorage.getItem('nutriologo_token') ?? ''
}

// ════════════════════════════════════════════════════════════
// REDIRECCIÓN: el bot no guarda, lleva
// ════════════════════════════════════════════════════════════

/**
 * Vistas a las que el chat puede enviar. Es una lista cerrada a propósito: el
 * backend manda un nombre de vista, no una URL, y el cliente decide si existe.
 * Un nombre desconocido no navega a ningún lado.
 */
const VISTAS: Record<string, string> = {
  registrar: '/registrar',
  mediciones: '/mediciones',
  objetivo: '/objetivo',
  perfil: '/perfil',
  balance: '/balance',
}

/**
 * Resumen legible de lo que va al formulario: "2 PIEZA · 150 G".
 *
 * Sin un espacio entre cantidad y unidad salía "2PIEZA", que se leía como un
 * número pegado a otro. La cantidad se formatea sin decimales si es entera:
 * "2" y no "2,0".
 */
function resumenItems(items: ItemRedirigido[]): string {
  return items
    .map((i) => {
      const n = Number(i.cantidad)
      const cant = Number.isFinite(n) ? String(Number(n.toFixed(2))) : String(i.cantidad)
      return `${i.nombre} ${cant} ${i.unidad}`
    })
    .join(' · ')
}

/**
 * Arma la query de la vista destino. Los datos van en la URL y no en el store
 * para que la vista se pueda recargar y el enlace sea reproducible.
 */
function irA(red: Redireccion) {
  const ruta = VISTAS[red.vista]
  if (!ruta) return

  const query: Record<string, string[]> = {}
  if (red.items.length) {
    query.nombre = red.items.map((i) => i.nombre)
    query.cantidad = red.items.map((i) => String(i.cantidad))
    query.unidad = red.items.map((i) => i.unidad)
  }
  void router.push({ path: ruta, query })
}

// ════════════════════════════════════════════════════════════
// PASOS REALES
// ════════════════════════════════════════════════════════════

/**
 * Instante real de arranque de cada paso, medido en el cliente entre el evento
 * `start` y su cierre. Vive aparte del paso para no ensuciar el tipo que se
 * muestra en pantalla.
 */
const inicios = new Map<string, number>()

/** Aplica un evento `paso` del backend sobre el mensaje vivo. */
function aplicarPaso(msg: Mensaje, nombre: string, estado: string, detalle: string | null) {
  const mapa: Record<string, EstadoPaso> = { start: 'corriendo', ok: 'hecho', fail: 'fallo' }
  const nuevo = mapa[estado] ?? 'corriendo'
  const existente = msg.pasos.find(p => p.nombre === nombre)

  if (nuevo === 'corriendo') {
    inicios.set(nombre, performance.now())
    if (!existente) {
      msg.pasos.push({ nombre, etiqueta: detalle || nombre, estado: 'corriendo' })
    }
    return
  }

  if (!existente) {
    // Cierre sin arranque visto (evento perdido): se registra con lo que hay.
    msg.pasos.push({ nombre, etiqueta: detalle || nombre, estado: nuevo })
    return
  }

  const inicio = inicios.get(nombre)
  if (inicio !== undefined) {
    existente.ms = Math.round(performance.now() - inicio)
    inicios.delete(nombre)
  }
  existente.estado = nuevo
  if (detalle) existente.etiqueta = detalle
}

function marcaEstado(e: EstadoPaso): string {
  return e === 'corriendo' ? 'en curso' : e === 'hecho' ? 'hecho' : 'falló'
}

/**
 * Resumen del desplegable de pasos: SOLO la cantidad.
 *
 * El tiempo total ya está en la meta del mensaje (`[17.4S · 70 TOKENS]`).
 * Repetirlo acá mostraba dos cronómetros del mismo turno con valores distintos
 * (9.4s de pasos contra 9.5s de meta) y parecía que eran dos mediciones.
 * Dentro de la lista cada paso conserva su duración propia: esa sí es info
 * distinta y sirve para ver dónde se fue el tiempo.
 */
function resumenPasos(m: Mensaje): string {
  const n = m.pasos.length
  return `${n} ${n === 1 ? 'paso' : 'pasos'}`
}

// ════════════════════════════════════════════════════════════
// SCROLL
// ════════════════════════════════════════════════════════════

let pegadoAbajo = true

function alFinal(forzar = false) {
  const el = hiloRef.value
  if (!el) return
  if (!forzar && !pegadoAbajo) return
  el.scrollTop = el.scrollHeight
  mostrarBajar.value = false
  pegadoAbajo = true
}

function onScroll() {
  const el = hiloRef.value
  if (!el) return
  const distancia = el.scrollHeight - el.scrollTop - el.clientHeight
  pegadoAbajo = distancia < 48
  mostrarBajar.value = !pegadoAbajo
}

// ════════════════════════════════════════════════════════════
// ACCIONES DE LA VISTA
// ════════════════════════════════════════════════════════════

function usarChip(t: string) {
  texto.value = t
  areaRef.value?.focus()
  void enviar()
}

async function copiar(t: string) {
  try {
    await navigator.clipboard.writeText(t)
  } catch {
    /* sin permiso de portapapeles: no es motivo para romper el chat */
  }
}

function marcar(m: Mensaje, util: boolean) {
  m.feedback = m.feedback === util ? null : util
  // Preferencia local: no existe endpoint de feedback en el backend, y no se
  // finge uno. Se guarda para poder evaluarla después.
  try {
    localStorage.setItem(
      'nutriologo_feedback',
      JSON.stringify([{ util, en: Date.now() }])
    )
  } catch {
    /* almacenamiento lleno o bloqueado */
  }
}

function regenerar(m: Mensaje) {
  const i = mensajes.value.findIndex(x => x.id === m.id)
  if (i < 0) return
  // Buscar el mensaje del usuario queоловó esta respuesta.
  let usuario: Mensaje | null = null
  for (let k = i - 1; k >= 0; k--) {
    if (mensajes.value[k].rol === 'user') {
      usuario = mensajes.value[k]
      break
    }
  }
  if (!usuario) return
  mensajes.value = mensajes.value.slice(0, i - 1)
  void enviarTexto(usuario.texto)
}

function nueva() {
  ctrl?.abort()
  ++runId // invalida cualquier evento en vuelo
  if (idleTimer) clearTimeout(idleTimer)
  idleTimer = null
  mensajes.value = []
  msgViva.value = null
  enviando.value = false
  setState('idle')
  void nextTick(() => areaRef.value?.focus())
}

function detener() {
  ctrl?.abort()
}

function crecerArea() {
  const ta = areaRef.value
  if (!ta) return
  ta.style.height = 'auto'
  ta.style.height = `${Math.min(ta.scrollHeight, 132)}px` // ~5 líneas
}

// ════════════════════════════════════════════════════════════
// ENVÍO: toda la máquina de estados vive acá
// ════════════════════════════════════════════════════════════

function enviar() {
  const q = texto.value.trim()
  if (!q || enviando.value) return
  texto.value = ''
  void enviarTexto(q)
}

async function enviarTexto(q: string) {
  if (enviando.value) return

  // 1. Corrida nueva: cancela la anterior e invalida sus eventos.
  ctrl?.abort()
  if (idleTimer) clearTimeout(idleTimer)
  inicios.clear()
  const id = ++runId
  const miCtrl = new AbortController()
  ctrl = miCtrl

  const historial: TurnoHistorial[] = mensajes.value
    .slice(-6)
    .map(m => ({ rol: m.rol, texto: m.texto }))

  // 2. Eco optimista del usuario (única caja del hilo).
  mensajes.value.push({ id: seq++, rol: 'user', texto: q, pasos: [], pasosColapsados: false })

  // 3. El mensaje del asistente es dueño de sus pasos y de su texto.
  const viva: Mensaje = { id: seq++, rol: 'assistant', texto: '', pasos: [], pasosColapsados: false }
  msgViva.value = viva

  enviando.value = true
  t0 = performance.now()
  setState('pensando')
  // Al ENVIAR siempre se baja al final, aunque el usuario estuviera leyendo
  // arriba: acaba de escribir algo y quiere ver la respuesta, no el historial.
  await nextTick()
  alFinal(true)

  let tokens = 0
  let msServidor: number | undefined

  try {
    const respuesta = await fetch('/api/conversacion/stream', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'text/event-stream',
        Authorization: `Bearer ${token()}`,
      },
      body: JSON.stringify({ texto: q, historial }),
      signal: miCtrl.signal,
    })

    if (!respuesta.ok || !respuesta.body) {
      const cuerpo = await respuesta.text().catch(() => '')
      throw new Error(cuerpo || `HTTP ${respuesta.status}`)
    }

    for await (const ev of leerSse(respuesta, miCtrl.signal)) {
      if (id !== runId) return // evento de una corrida vieja: se ignora

      if (ev.evento === 'paso') {
        const d = ev.datos || {}
        aplicarPaso(viva, String(d.nombre), String(d.estado), d.detalle ?? null)
        if (d.estado === 'start') setState('herramienta')
      } else if (ev.evento === 'token') {
        const trozo = ev.datos?.texto
        if (typeof trozo === 'string' && trozo) {
          if (!viva.texto) {
            // Primer token: los pasos se colapsan y arranca el texto.
            viva.pasosColapsados = true
            setState('respondiendo')
          }
          viva.texto += trozo
          tokens++
          alFinal()
        }
      } else if (ev.evento === 'result') {
        const r = ev.datos as ResultadoBackend
        // El resultado es la verdad final: reconcilia lo que se transmitió.
        const textoFinal = r.texto || r.pregunta || r.mensaje || viva.texto
        viva.texto = textoFinal
        viva.fallback = r.fallback === true
        // El bot no guardó nada: entrega los datos y la vista de destino.
        if (r.redireccion && r.redireccion.vista) {
          viva.redireccion = {
            vista: r.redireccion.vista,
            etiqueta: r.redireccion.etiqueta || 'IR',
            items: Array.isArray(r.redireccion.items) ? r.redireccion.items : [],
          }
        }
      } else if (ev.evento === 'error') {
        throw new Error(ev.datos?.message || 'No pude completar la petición.')
      } else if (ev.evento === 'done') {
        msServidor = typeof ev.datos?.ms === 'number' ? ev.datos.ms : undefined
      }
    }

    if (id !== runId) return

    // El stream cerró: el turno terminó de verdad.
    if (!viva.texto) {
      // Respuesta sin texto (p.ej. registro OK sin mensaje): no hay hueco vacío.
      viva.texto = 'Turno completado sin texto de respuesta.'
    }
    const ms = msServidor ?? Math.round(performance.now() - t0)
    viva.meta = tokens > 0 ? `[${(ms / 1000).toFixed(1)}S · ${tokens} TOKENS]` : `[${(ms / 1000).toFixed(1)}S]`
    viva.pasosColapsados = viva.pasos.length > 0

    mensajes.value.push({ ...viva })
    msgViva.value = null
    setState('listo')
  } catch (e) {
    if (id !== runId) return

    const err = e as Error & { name?: string }
    if (err.name === 'AbortError') {
      // El texto parcial se conserva; no se pierde lo ya recibido.
      if (viva.texto || viva.pasos.length) {
        viva.detenido = true
        viva.texto = `${viva.texto}${viva.texto ? ' ' : ''}[RESPUESTA DETENIDA]`
        const ms = Math.round(performance.now() - t0)
        viva.meta = `[${(ms / 1000).toFixed(1)}S · DETENIDO]`
        mensajes.value.push({ ...viva })
      }
      msgViva.value = null
      setState('detenido')
    } else {
      // El error se CONSERVA en el hilo: si se queda solo en el mensaje vivo,
      // al limpiarlo desaparece el aviso y el usuario no sabe qué pasó.
      viva.error = err.message || 'No pude completar la petición.'
      // Sin texto inventado: si el modelo no dijo nada, no se muestra nada.
      mensajes.value.push({ ...viva, meta: undefined })
      msgViva.value = null
      setState('error')
    }
  } finally {
    // Siempre: si esta corrida sigue siendo la vigente, desbloquea el composer
    // y programa la vuelta a idle (único temporizador legítimo restante).
    if (id === runId) {
      enviando.value = false
      ctrl = null
      void nextTick(() => {
        alFinal()
        areaRef.value?.focus()
      })

      const destino: UiEstado = uiState.value
      if (destino === 'listo' || destino === 'detenido') {
        idleTimer = setTimeout(() => {
          if (id !== runId) return
          if (uiState.value === destino) setState('idle')
        }, destino === 'listo' ? 1800 : 1500)
      }
    }
  }
}

// ════════════════════════════════════════════════════════════
// CICLO DE VIDA
// ════════════════════════════════════════════════════════════

function onKeydownGlobal(e: KeyboardEvent) {
  if (e.key === 'Escape' && enviando.value) detener()
}

onMounted(() => {
  void nextTick(() => areaRef.value?.focus())
  window.addEventListener('keydown', onKeydownGlobal)
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydownGlobal)
  ctrl?.abort()
  if (idleTimer) clearTimeout(idleTimer)
})
</script>