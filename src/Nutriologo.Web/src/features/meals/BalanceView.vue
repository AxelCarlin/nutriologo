<template>
  <section class="balance">
    <header class="balance__head">
      <h1 class="titulo">balance</h1>
    </header>

    <!-- Navegación de fecha: stepping lineal (< >), no popover de calendario
         (nothing-design §9). AppDate solo para saltos largos. -->
    <div class="stepper-fecha" role="group" aria-label="Navegar por fecha">
      <button type="button" class="stepper-fecha__btn" aria-label="Día anterior" @click="mover(-1)">
        <ChevronLeft :size="18" :stroke-width="1.5" />
      </button>
      <button type="button" class="stepper-fecha__hoy" @click="irHoy">
        <span class="stepper-fecha__fecha">{{ fechaLarga }}</span>
        <span v-if="esHoy" class="label">HOY</span>
      </button>
      <button
        type="button"
        class="stepper-fecha__btn"
        aria-label="Día siguiente"
        :disabled="esHoy"
        @click="mover(1)"
      >
        <ChevronRight :size="18" :stroke-width="1.5" />
      </button>
    </div>

    <div v-if="cargando" class="balance__bits" role="status" aria-label="Cargando balance">
      <span v-for="n in 3" :key="n" class="bit" :style="{ animationDelay: `${(n - 1) * 160}ms` }" />
    </div>

    <div v-else-if="error" class="alerta" role="alert">
      <span class="alerta__punto" aria-hidden="true" />
      <div>
        <p class="label">ERROR</p>
        <p class="alerta__msg">{{ error }}</p>
      </div>
      <button type="button" class="btn-ghost btn-ghost--sm" @click="cargar">Reintentar</button>
    </div>

    <template v-else>
      <!-- HÉROE: cifra en dot-matrix, redondeada y formateada igual que Inicio.
           Sin meta no hay denominador: no se inventa (MetaCalorica = 0 filas). -->
      <div class="hero">
        <p class="label">CALORÍAS · {{ fechaLarga }}</p>
        <p class="hero__valor">{{ kcalFmt }}</p>
        <p class="hero__meta">
          <span class="label">KCAL</span>
          <span class="hero__sep" aria-hidden="true" />
          <span class="label">{{ comidas.length }} {{ comidas.length === 1 ? 'COMIDA' : 'COMIDAS' }}</span>
        </p>
      </div>

      <!-- MACROS del día (de /panel, no existen en /balance) -->
      <section class="card">
        <div class="card__head">
          <h2 class="label">MACRONUTRIENTES</h2>
          <span class="label">{{ macroFilas.length ? fechaCorta : 'SIN DATOS' }}</span>
        </div>
        <ul v-if="macroFilas.length" class="macros">
          <li v-for="m in macroFilas" :key="m.codigo" class="macros__row">
            <span class="macros__label label">{{ m.etiqueta }}</span>
            <span class="macros__track">
              <span class="macros__fill" :style="{ width: `${m.pct}%` }" />
            </span>
            <span class="macros__value num">{{ m.gramos }}</span>
            <span class="label">G</span>
          </li>
        </ul>
        <p v-else class="empty">
          Sin desglose de macronutrientes para este día.
        </p>
      </section>

      <!-- COMIDAS: el número deja de ser opaco -->
      <section class="card">
        <div class="card__head">
          <h2 class="label">COMIDAS · {{ fechaLarga }}</h2>
          <span class="label">{{ comidas.length }}</span>
        </div>
        <p v-if="aviso" class="aviso label">{{ aviso }}</p>
        <ul v-if="comidas.length" class="rows">
          <li v-for="c in comidas" :key="c.id" class="rows__grupo">
            <!-- Fila expandible: progressive disclosure. Una vista detalle
                 aparte sería sobre-navegación para 2–3 líneas de contenido. -->
            <button
              type="button"
              class="rows__item rows__item--btn"
              :aria-expanded="abierta === c.id"
              :aria-label="`${c.nombre ?? 'Sin nombre'}: ver alimentos`"
              @click="abierta = abierta === c.id ? null : c.id"
            >
              <span class="rows__hora num">{{ hora(c.hora) }}</span>
              <span class="rows__cuerpo">
                <span class="rows__titulo">{{ c.nombre ?? 'Sin nombre' }}</span>
                <span v-if="c.tipo" class="label">{{ c.tipo }}</span>
              </span>
              <span class="rows__valor num">{{ Math.round(c.caloriasEstimadas ?? 0) }}</span>
              <span class="label">KCAL</span>
              <!-- Punto rojo: el dato es absurdo y pide revisión. Una app que
                   muestra 7,750 kcal sin avisar pierde credibilidad. -->
              <span
                v-if="(c.caloriasEstimadas ?? 0) > UMBRAL_REVISAR"
                class="rows__alerta"
                role="img"
                aria-label="Valor fuera de rango, revisar"
              />
              <ChevronDown
                :size="16"
                :stroke-width="1.5"
                class="rows__chevron"
                :class="{ 'rows__chevron--abierto': abierta === c.id }"
                aria-hidden="true"
              />
            </button>
            <ul v-if="abierta === c.id" class="subrows">
              <li v-for="(it, j) in c.items" :key="j" class="subrows__item">
                <span class="subrows__nombre">{{ it.alimento }}</span>
                <span class="subrows__cant num">
                  {{ fmtCant(it.cantidad) }}{{ it.unidad ? ` ${String(it.unidad).toLowerCase()}` : '' }}
                </span>
                <span class="subrows__kcal num">{{ it.kcal ?? '—' }}</span>
                <span class="label">KCAL</span>
              </li>
              <li v-if="!c.items.length" class="subrows__item">
                <span class="empty">Sin detalle de alimentos.</span>
              </li>
              <!-- Baja lógica en dos pasos: nada se borra físicamente, solo se
                   excluye de las lecturas. Sin confirmación sería un tap
                   accidental con pérdida aparente de datos. -->
              <li class="subrows__item subrows__accion">
                <button
                  v-if="confirmando !== c.id"
                  type="button"
                  class="remove"
                  :aria-label="`Eliminar ${c.nombre ?? 'comida'}`"
                  @click="confirmando = c.id"
                >
                  <Trash2 :size="16" :stroke-width="1.5" aria-hidden="true" />
                  <span>ELIMINAR</span>
                </button>
                <template v-else>
                  <span class="label">¿ELIMINAR ESTA COMIDA?</span>
                  <button
                    type="button"
                    class="remove remove--danger"
                    :disabled="eliminando"
                    @click="eliminar(c.id)"
                  >
                    SÍ, ELIMINAR
                  </button>
                  <button type="button" class="remove" :disabled="eliminando" @click="confirmando = null">
                    CANCELAR
                  </button>
                </template>
              </li>
            </ul>
          </li>
        </ul>
        <p v-else class="empty">
          Sin registros este día.
          <RouterLink to="/registrar" class="enlace">Registrar comida</RouterLink>
        </p>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { ChevronDown, ChevronLeft, ChevronRight, Trash2 } from 'lucide-vue-next'
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

interface ItemComida {
  alimento: string
  cantidad: number
  unidad: string | null
  gramos: number | null
  kcal: number | null
}
interface Comida {
  id: number
  nombre: string | null
  caloriasEstimadas: number | null
  tipo: string | null
  hora: string
  items: ItemComida[]
}
interface Panel {
  fecha: string
  caloriasConsumidas: number
  macros: Record<string, number>
  comidas: Comida[]
}

const ETIQUETA_MACRO: Record<string, string> = {
  PROTEINA: 'PROTEÍNA',
  GRASA: 'GRASA',
  CARBOHIDRATO: 'CARBOHIDRATO',
}

/**
 * Fecha local YYYY-MM-DD. `new Date().toISOString()` es UTC: en México entre
 * las 18:00 y 23:59 ya es el día siguiente y la vista arrancaba consultando
 * "mañana" con 0 kcal sin explicación.
 */
function hoyLocal(): string {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const dia = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${dia}`
}

function desplazar(iso: string, dias: number): string {
  const [a, m, d] = iso.split('-').map(Number)
  const f = new Date(a, m - 1, d + dias)
  const mm = String(f.getMonth() + 1).padStart(2, '0')
  const dd = String(f.getDate()).padStart(2, '0')
  return `${f.getFullYear()}-${mm}-${dd}`
}

const fechaIso = ref(hoyLocal())
const abierta = ref<number | null>(null)
/** Id en confirmación de borrado (segundo paso). Null = sin confirmar. */
const confirmando = ref<number | null>(null)
const eliminando = ref(false)
const aviso = ref('')

/**
 * Umbral de sospecha por comida. Coherente con la validación de backend
 * pendiente: por encima de esto el dato casi seguro es un error de
 * cantidad/unidad, no una comida real.
 */
const UMBRAL_REVISAR = 3000
const cargando = ref(true)
const error = ref('')
const kcal = ref(0)
const macros = ref<Record<string, number>>({})
const comidas = ref<Comida[]>([])

const esHoy = computed(() => fechaIso.value === hoyLocal())

// Formato explícito DD MMM AAAA: el <input type="date"> nativo mostraba
// 10/05/2026, que en México se lee 10 de mayo pero era el 5 de octubre.
function fmtFecha(iso: string): string {
  const [a, m, d] = iso.split('-').map(Number)
  return new Date(a, m - 1, d)
    .toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' })
    .toUpperCase()
    .replace('.', '')
}
const fechaLarga = computed(() => fmtFecha(fechaIso.value))
const fechaCorta = computed(() => {
  const [a, m, d] = fechaIso.value.split('-').map(Number)
  return new Date(a, m - 1, d)
    .toLocaleDateString('es-MX', { day: '2-digit', month: 'short' })
    .toUpperCase()
    .replace('.', '')
})

/** Redondeo + formato idénticos a Inicio: mismo dato, misma presentación. */
const kcalFmt = computed(() => Math.round(kcal.value).toLocaleString('es-MX'))

/** Cantidad sin ceros colgando: 100 → "100", 1.5 → "1.5". */
const fmtCant = (v: number) => (Math.round(v * 100) / 100).toString()

/** Ver Inicio.vue: el backend emite wall-clock UTC sin offset. */
const hora = (iso: string) =>
  new Date(iso.endsWith('Z') ? iso : `${iso}Z`).toLocaleTimeString('es-MX', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })

const macroFilas = computed(() => {
  const vals = Object.values(macros.value)
  const max = Math.max(...vals, 1)
  return Object.entries(macros.value)
    .map(([codigo, gramos]) => ({
      codigo,
      etiqueta: ETIQUETA_MACRO[codigo] ?? codigo,
      gramos: Math.round(gramos),
      pct: Math.round((gramos / max) * 100),
    }))
    .sort((a, b) => b.gramos - a.gramos)
})

function mover(dias: number) {
  fechaIso.value = desplazar(fechaIso.value, dias)
  // Cambiar de día invalida la fila abierta: los ids ya no corresponden.
  abierta.value = null
  cargar()
}

function irHoy() {
  if (!esHoy.value) {
    fechaIso.value = hoyLocal()
    cargar()
  }
}

async function eliminar(id: number) {
  if (eliminando.value) return
  eliminando.value = true
  try {
    await apiFetch(`/comidas/${id}`, { method: 'DELETE' })
    confirmando.value = null
    abierta.value = null
    aviso.value = 'ELIMINADA · YA NO CUENTA EN EL BALANCE'
    setTimeout(() => (aviso.value = ''), 4000)
    // Recarga desde el servidor: los totales se recomputan allá, no acá.
    await cargar()
  } catch {
    aviso.value = 'NO SE PUDO ELIMINAR · INTENTÁ DE NUEVO'
  } finally {
    eliminando.value = false
  }
}

async function cargar() {
  cargando.value = true
  error.value = ''
  try {
    // /panel en vez de /balance: un solo fetch trae kcal + macros + comidas.
    // /balance solo devuelve el agregado y dejaba el número opaco.
    const p = await apiFetch<Panel>(`/panel?fecha=${fechaIso.value}T12:00:00`)
    kcal.value = p.caloriasConsumidas
    macros.value = p.macros ?? {}
    comidas.value = p.comidas ?? []
  } catch (e: unknown) {
    const status = (e as { status?: number })?.status
    error.value = `No se pudo cargar el balance${status ? ` (HTTP ${status})` : ''}.`
  } finally {
    cargando.value = false
  }
}

onMounted(cargar)
</script>

<style>
/* ========== BALANCE ==========
   Eje --dash-w, título .titulo, CTA y bits del sistema. Fuente única /panel:
   kcal + macros + comidas en un solo fetch. */

.balance {
  width: 100%;
  max-width: var(--dash-w);
  margin: 0 auto;
  padding: var(--space-xl);
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.balance__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}

/* --- STEPPING DE FECHA (nothing-design §9: linear stepping, no popover) --- */
.stepper-fecha {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  padding-bottom: var(--space-md);
  border-bottom: 1px solid var(--nt-line);
}

.stepper-fecha__btn {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  border: 1px solid transparent;
  background: transparent;
  color: var(--nt-ink-2);
  cursor: pointer;
  transition: border-color var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.stepper-fecha__btn:hover:not(:disabled) {
  border-color: var(--nt-line-strong);
  color: var(--nt-ink);
}

.stepper-fecha__btn:disabled {
  opacity: 0.3;
  cursor: default;
}

.stepper-fecha__hoy {
  display: flex;
  align-items: baseline;
  gap: var(--space-sm);
  border: 0;
  background: transparent;
  cursor: pointer;
  padding: 0;
}

.stepper-fecha__fecha {
  font: 400 20px/1 var(--nt-font-dot);
  text-transform: lowercase;
  color: var(--nt-ink);
}

/* --- BITS / ALERTA (patrón del sitio) --- */
.balance__bits {
  display: flex;
  gap: 8px;
  padding: 64px 0;
  justify-content: center;
}

.bit {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-ink);
  animation: bit-blink 1.2s steps(1) infinite;
}

@keyframes bit-blink {
  0%,
  100% {
    opacity: 0.2;
  }
  33% {
    opacity: 1;
  }
}

.alerta {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  padding: var(--space-md);
  border: 1px solid var(--nt-red);
  border-radius: var(--radius-field);
}

.alerta__msg {
  font: 400 14px/1.45 var(--nt-font-sans);
}

.alerta__punto {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-red);
  flex-shrink: 0;
}

.btn-ghost--sm {
  height: 32px;
  padding: 0 12px;
}

/* --- HÉROE --- */
.hero {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.hero__valor {
  font-family: var(--nt-font-dot);
  font-size: clamp(64px, 12vw, 128px);
  line-height: 0.9;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.hero__meta {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  flex-wrap: wrap;
}

.hero__sep {
  width: 1px;
  height: 12px;
  background: var(--nt-line-strong);
}

/* --- CARDS (superficie plana + hairline, padding real) --- */
.card {
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line);
  border-radius: var(--radius-card);
  padding: var(--space-md);
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-sm);
}

/* Sin `font: inherit`: ganaba a `.label` y los títulos salían en Grotesk. */
.card__head h2 {
  color: var(--nt-ink-2);
}

.empty {
  font: 400 14px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-3);
  max-width: 52ch;
}

.enlace {
  font: 400 11px/12px var(--nt-font-mono);
  letter-spacing: 0.08em;
  color: var(--nt-ink-2);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.enlace:hover {
  color: var(--nt-ink);
}

/* --- MACROS --- */
.macros {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.macros__row {
  display: grid;
  grid-template-columns: 110px 1fr auto auto;
  align-items: center;
  gap: var(--space-sm);
}

.macros__track {
  height: 6px;
  background: var(--nt-surface-3);
}

.macros__fill {
  display: block;
  height: 100%;
  background: var(--nt-ink);
}

.macros__value {
  font-size: 16px;
  text-align: right;
}

/* --- FILAS (comidas del día) --- */
.rows {
  list-style: none;
  display: flex;
  flex-direction: column;
}

.rows__item {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  padding: 12px 0;
  border-top: 1px solid var(--nt-line);
}

.rows__item:first-child {
  border-top: 0;
}

.rows__hora {
  font-size: 12px;
  color: var(--nt-ink-2);
  width: 5ch;
}

.rows__cuerpo {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.rows__titulo {
  font: 400 15px/1.3 var(--nt-font-sans);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rows__valor {
  font-size: 15px;
}

/* Punto rojo: valor fuera de rango razonable, pide revisión. */
.rows__alerta {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-red);
  flex-shrink: 0;
}

.aviso {
  color: var(--nt-ok);
}

.subrows__accion {
  justify-content: flex-end;
}

/* Eliminar: texto discreto que se vuelve rojo solo en hover/confirmar.
   Sin borde: no compite con el CTA ni parece acción principal. */
.remove {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 0;
  background: transparent;
  color: var(--nt-ink-3);
  font: 400 11px/12px var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  padding: 12px 4px;
  transition: color var(--nt-t-fast) var(--nt-ease);
}

.remove:hover:not(:disabled) {
  color: var(--nt-red);
}

.remove--danger {
  color: var(--nt-red);
}

.remove:disabled {
  opacity: 0.5;
  cursor: default;
}

.rows__grupo {
  border-top: 1px solid var(--nt-line);
}

.rows__grupo:first-child {
  border-top: 0;
}

.rows__item--btn {
  width: 100%;
  border: 0;
  background: transparent;
  color: var(--nt-ink);
  font: inherit;
  text-align: left;
  cursor: pointer;
  border-top: 0;
}

.rows__chevron {
  color: var(--nt-ink-3);
  flex-shrink: 0;
  transition: transform var(--nt-t-base) var(--nt-ease);
}

.rows__chevron--abierto {
  transform: rotate(180deg);
  color: var(--nt-ink);
}

/* Sub-items: indentación 16–24px, mismo filete, sin tree lines (§4). */
.subrows {
  list-style: none;
  display: flex;
  flex-direction: column;
  padding-left: 20px;
}

.subrows__item {
  display: flex;
  align-items: baseline;
  gap: var(--space-sm);
  padding: 10px 0;
  border-top: 1px solid var(--nt-line);
}

.subrows__nombre {
  flex: 1;
  font: 400 14px/1.3 var(--nt-font-sans);
  color: var(--nt-ink-2);
}

.subrows__cant {
  font-size: 13px;
  color: var(--nt-ink-3);
}

.subrows__kcal {
  font-size: 13px;
  min-width: 5ch;
  text-align: right;
}

.btn-ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 40px;
  padding: 0 16px;
  border-radius: var(--radius-field);
  background: transparent;
  border: 1px solid var(--nt-ink);
  color: var(--nt-ink);
  font: 400 11px/12px var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.1em;
  cursor: pointer;
  margin-left: auto;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.btn-ghost:hover {
  background: var(--nt-ink);
  color: var(--nt-bg);
}

@media (max-width: 640px) {
  .balance {
    padding: var(--space-lg) var(--space-md);
  }

  .macros__row {
    grid-template-columns: 88px 1fr auto auto;
  }
}
</style>