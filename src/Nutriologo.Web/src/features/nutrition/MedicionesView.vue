<template>
  <section class="med">
    <header class="med__head">
      <h1 class="titulo">mediciones</h1>
    </header>

    <!-- Tipo como chips con nombre. El backend sigue recibiendo el ID. -->
    <div class="chips">
      <button
        v-for="t in tipos"
        :key="t.id"
        type="button"
        class="chip"
        :aria-pressed="tipoActivo === t.id"
        @click="seleccionar(t.id)"
      >
        {{ t.nombre }}
      </button>
    </div>

    <div class="med__cuerpo">
      <!-- ============ COLUMNA IZQUIERDA: datos ============ -->
      <div class="med__datos">
        <!-- Estado vacío explícito -->
        <template v-if="!cargando && !mediciones.length">
          <p class="label">SIN MEDICIONES</p>
          <p class="med__vacio">
            Registra la primera medición de
            <strong>{{ tipoActual?.nombre }}</strong> para empezar a ver la
            evolución.
          </p>
        </template>

        <template v-else>
          <!-- HÉROE: última medición -->
          <section v-if="ultima" class="heroe">
            <p class="label">ÚLTIMA · {{ fechaCorta(ultima.fechaMedicion) }}</p>
            <p class="heroe__valor">{{ fmt(ultima.valor) }}</p>
            <p class="heroe__meta">
              <span class="label">{{ tipoActual?.unidad }}</span>
              <span
                v-if="delta !== null"
                class="heroe__delta"
                :class="delta > 0 ? 'heroe__delta--sube' : 'heroe__delta--baja'"
              >
                {{ delta > 0 ? '▲' : '▼' }} {{ Math.abs(delta).toFixed(1) }}
              </span>
              <span v-if="anterior" class="label">VS. {{ fechaCorta(anterior.fechaMedicion) }}</span>
            </p>
          </section>

          <!-- Sparkline: línea fina + puntos. Último punto en rojo. -->
          <section v-if="mediciones.length > 1" class="spark">
            <svg
              class="spark__svg"
              :viewBox="`0 0 ${W} ${H}`"
              preserveAspectRatio="none"
              role="img"
              :aria-label="`Serie de ${mediciones.length} mediciones`"
            >
              <polyline class="spark__line" :points="linea" />
              <circle
                v-for="(p, i) in puntos"
                :key="i"
                class="spark__punto"
                :class="{ 'spark__punto--ultimo': i === puntos.length - 1 }"
                :cx="p.x"
                :cy="p.y"
                r="2.5"
              />
            </svg>
            <p class="spark__pie label">
              <span>{{ mediciones[mediciones.length - 1] ? fmt(min) : '' }}</span>
              <span>{{ mediciones.length }} MEDICIONES</span>
              <span>{{ fmt(max) }}</span>
            </p>
          </section>

          <!-- HISTORIAL -->
          <section v-if="mediciones.length">
            <p class="label">HISTORIAL</p>
            <ul class="historial">
              <li v-for="m in mediciones" :key="m.id" class="historial__fila">
                <time class="label">{{ fechaCorta(m.fechaMedicion) }}</time>
                <span class="historial__valor num">{{ fmt(m.valor) }}</span>
                <span class="label">{{ tipoActual?.unidad }}</span>
                <span
                  v-if="deltaDe(m) !== null"
                  class="historial__delta"
                  :class="(deltaDe(m) ?? 0) > 0 ? 'historial__delta--sube' : 'historial__delta--baja'"
                >
                  {{ (deltaDe(m) ?? 0) > 0 ? '▲' : '▼' }} {{ Math.abs(deltaDe(m) ?? 0).toFixed(1) }}
                </span>
              </li>
            </ul>
          </section>
        </template>
      </div>

      <!-- ============ COLUMNA DERECHA: captura ============ -->
      <aside class="med__nueva">
        <p class="label">NUEVA MEDICIÓN</p>

        <div class="campo">
          <label class="label" for="m-valor">VALOR</label>
          <div class="campo__box">
            <input
              id="m-valor"
              v-model.number="valor"
              class="input num"
              type="number"
              inputmode="decimal"
              step="0.1"
              :class="{ 'input--error': error || fueraDeRango }"
            />
            <span class="unidad label">{{ tipoActual?.unidad }}</span>
          </div>
          <p v-if="error" class="campo__error label">{{ error }}</p>
          <p v-else-if="fueraDeRango" class="campo__error label">
            FUERA DE RANGO · {{ tipoActual?.min }}–{{ tipoActual?.max }}
            {{ tipoActual?.unidad }}
          </p>
          <p v-else-if="pista" class="campo__pista label">{{ pista }}</p>
        </div>

        <div class="campo">
          <p class="label">FECHA</p>
          <div class="chips">
            <button
              v-for="f in atajosFecha"
              :key="f.clave"
              type="button"
              class="chip chip--sm"
              :aria-pressed="fechaIso === f.iso"
              @click="fechaIso = f.iso"
            >
              {{ f.etiqueta }}
            </button>
          </div>
          <input v-model="fechaIso" class="input input--fecha" type="date" aria-label="Fecha de la medición" />
        </div>

        <p v-if="aviso" class="aviso label">{{ aviso }}</p>

        <button type="button" class="cta" :disabled="!puedeRegistrar || loading" @click="registrar">
          {{ loading ? 'GUARDANDO' : 'REGISTRAR' }}
        </button>
      </aside>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch } from '../../shared/api/client'
import { useProfileStore } from '../../shared/stores/profile'

interface TipoMedicion {
  id: number
  codigo: string
  nombre: string
  unidad: string
  min: number
  max: number
}
interface Medicion {
  id: number
  idTipoMedicionCorporal: number
  valor: number
  fechaMedicion: string
  observacion: string | null
}

const store = useProfileStore()

const tipos = ref<TipoMedicion[]>([])
const mediciones = ref<Medicion[]>([])
const tipoActivo = ref<number | null>(null)
const valor = ref<number | null>(null)
const cargando = ref(true)
const loading = ref(false)
const error = ref('')
const aviso = ref('')

const hoyIso = () => new Date().toISOString().slice(0, 10)
const ayerIso = () => new Date(Date.now() - 86_400_000).toISOString().slice(0, 10)

const fechaIso = ref(hoyIso())
const atajosFecha = computed(() => [
  { clave: 'hoy', etiqueta: 'Hoy', iso: hoyIso() },
  { clave: 'ayer', etiqueta: 'Ayer', iso: ayerIso() },
])

const tipoActual = computed(() => tipos.value.find((t) => t.id === tipoActivo.value) ?? null)
const ultima = computed(() => mediciones.value[0] ?? null)
const anterior = computed(() => mediciones.value[1] ?? null)

// Descarta mediciones de la misma fecha: si se registra dos veces el mismo día,
// la gráfica mostraría dos puntos con el mismo eje temporal.
const unicasPorFecha = computed(() => {
  const vistos = new Set<string>()
  return mediciones.value.filter((m) => {
    const d = m.fechaMedicion.slice(0, 10)
    if (vistos.has(d)) return false
    vistos.add(d)
    return true
  })
})

const delta = computed(() => {
  const a = ultima.value
  const b = anterior.value
  if (!a || !b) return null
  return Math.round((a.valor - b.valor) * 10) / 10
})

const deltaDe = (m: Medicion) => {
  const i = mediciones.value.indexOf(m)
  if (i < 0 || i + 1 >= mediciones.value.length) return null
  return Math.round((m.valor - mediciones.value[i + 1]!.valor) * 10) / 10
}

const min = computed(() => Math.min(...unicasPorFecha.value.map((m) => m.valor)))
const max = computed(() => Math.max(...unicasPorFecha.value.map((m) => m.valor)))

const fmt = (v: number) => (Math.round(v * 10) / 10).toFixed(1)

// Sparkline en viewBox fijo; preserveAspectRatio=none lo estira al ancho.
const W = 100
const H = 40
const puntos = computed(() => {
  const d = [...unicasPorFecha.value].reverse()
  if (!d.length) return []
  const lo = min.value
  const hi = max.value
  const span = hi - lo || 1
  return d.map((m, i) => ({
    x: d.length === 1 ? W / 2 : (i / (d.length - 1)) * (W - 4) + 2,
    y: H - 4 - ((m.valor - lo) / span) * (H - 8),
  }))
})
const linea = computed(() => puntos.value.map((p) => `${p.x},${p.y}`).join(' '))

const pista = computed(() => {
  const t = tipoActual.value
  if (!t || valor.value == null) return ''
  return `RANGO ${t.min}–${t.max} ${t.unidad}`
})

// Fuera de rango es un estado visible, no solo un botón deshabilitado: sin
// feedback el usuario no sabe por qué no se puede registrar.
const fueraDeRango = computed(() => {
  const t = tipoActual.value
  if (!t || valor.value == null) return false
  return valor.value < t.min || valor.value > t.max
})

const puedeRegistrar = computed(() => {
  const t = tipoActual.value
  return t !== null && valor.value != null && !fueraDeRango.value
})

// Fecha legible en formato 04 OCT 2026: el date picker nativo muestra 10/04/2026
// que es ambiguo (¿10 de abril o 4 de octubre?).
function fechaCorta(iso: string) {
  return new Date(iso).toLocaleDateString('es-MX', {
    day: '2-digit',
    month: 'short',
    year: '2-digit',
  }).toUpperCase().replace('.', '')
}

async function seleccionar(id: number) {
  if (tipoActivo.value === id) return
  tipoActivo.value = id
  error.value = ''
  aviso.value = ''
  await cargar()
}

async function cargar() {
  cargando.value = true
  try {
    mediciones.value = await apiFetch<Medicion[]>(`/mediciones?tipo=${tipoActivo.value}`)
  } catch {
    error.value = 'No se pudieron cargar las mediciones.'
  } finally {
    cargando.value = false
  }
}

async function registrar() {
  if (!puedeRegistrar.value || tipoActivo.value == null) return
  loading.value = true
  error.value = ''
  aviso.value = ''
  try {
    await apiFetch('/mediciones', {
      method: 'POST',
      body: JSON.stringify({
        IdTipoMedicionCorporal: tipoActivo.value,
        Valor: valor.value,
        FechaMedicion: new Date(`${fechaIso.value}T09:00:00`).toISOString(),
      }),
    })
    const t = tipoActual.value
    aviso.value = `REGISTRADO · ${fmt(valor.value!)} ${t?.unidad.toUpperCase()}`
    valor.value = null
    await cargar()
  } catch {
    error.value = 'No se pudo registrar la medición.'
  } finally {
    loading.value = false
  }
}

watch(fechaIso, () => { error.value = '' })

onMounted(async () => {
  try {
    await store.cargarCatalogos()
    tipos.value = store.tiposMedicion
    if (tipos.value.length) {
      // PESO por defecto si existe; si no, el primero del catálogo.
      tipoActivo.value = tipos.value.find((t) => t.codigo === 'PESO')?.id ?? tipos.value[0]!.id
      await cargar()
    } else {
      cargando.value = false
    }
  } catch {
    cargando.value = false
    error.value = 'No se pudo cargar el catálogo de mediciones.'
  }
})
</script>

<style>
/* ========== MEDICIONES ==========
   Eje compartido con Inicio/Objetivo (--dash-w). Grid de 2 columnas:
   datos a la izquierda, captura a la derecha. */

.med {
  width: 100%;
  max-width: var(--dash-w);
  margin: 0 auto;
  padding: var(--space-xl);
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.med__cuerpo {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: var(--space-xl);
  align-items: start;
}

.med__datos {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.med__vacio {
  font: 400 14px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-3);
  max-width: 46ch;
}

.chips {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.chip {
  height: 36px;
  padding: 0 16px;
  border: 1px solid var(--nt-line-strong);
  border-radius: 999px;
  background: transparent;
  color: var(--nt-ink-2);
  font: 400 11px/1 var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: border-color var(--nt-t-fast) var(--nt-ease),
    background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.chip:hover {
  border-color: var(--nt-ink);
  color: var(--nt-ink);
}

.chip[aria-pressed='true'] {
  background: var(--nt-ink);
  border-color: var(--nt-ink);
  color: var(--nt-bg);
}

.chip--sm {
  height: 32px;
  padding: 0 14px;
}

/* ---- HÉROE ---- */
.heroe {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.heroe__valor {
  font-family: var(--nt-font-dot);
  font-size: clamp(56px, 9vw, 96px);
  line-height: 0.9;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.heroe__meta {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  flex-wrap: wrap;
}

.heroe__delta {
  font: 400 11px/12px var(--nt-font-mono);
  letter-spacing: 0.08em;
}

.heroe__delta--sube {
  color: var(--nt-ink);
}

/* Rojo reservado para "se aleja mucho". Sin meta definida no se pinta. */
.heroe__delta--baja {
  color: var(--nt-ink);
}

/* ---- SPARKLINE ---- */
.spark {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.spark__svg {
  width: 100%;
  height: 96px;
  overflow: visible;
}

.spark__line {
  fill: none;
  stroke: var(--nt-line-strong);
  stroke-width: 1;
  vector-effect: non-scaling-stroke;
}

.spark__punto {
  fill: var(--nt-ink-3);
}

/* Último punto en rojo: marca "aquí estás". */
.spark__punto--ultimo {
  fill: var(--nt-red);
}

.spark__pie {
  display: flex;
  justify-content: space-between;
  color: var(--nt-ink-3);
}

/* ---- HISTORIAL: filas de altura fija ---- */
.historial {
  list-style: none;
  display: flex;
  flex-direction: column;
}

.historial__fila {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
  height: 48px;
  border-bottom: 1px solid var(--nt-line);
}

.historial__fila time {
  width: 12ch;
  color: var(--nt-ink-2);
}

.historial__valor {
  flex: 1;
  font-size: 15px;
}

.historial__delta {
  font: 400 11px/12px var(--nt-font-mono);
  letter-spacing: 0.06em;
  color: var(--nt-ink-3);
}

/* ---- COLUMNA DERECHA ---- */
.med__nueva {
  position: sticky;
  top: 24px;
  display: flex;
  flex-direction: column;
  gap: var(--space-md);
  padding: var(--space-md);
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line);
  border-radius: var(--radius-card);
}

.campo {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.campo__box {
  position: relative;
}

.input {
  height: 44px;
  width: 100%;
  padding: 0 48px 0 12px;
  border-radius: var(--radius-field);
  background: var(--nt-bg);
  border: 1px solid var(--nt-line-strong);
  color: var(--nt-ink);
  font: 400 16px/1 var(--nt-font-sans);
  outline: none;
}

.input.num {
  font-family: var(--nt-font-mono);
  font-size: 15px;
}

.input[type='number'] {
  -moz-appearance: textfield;
  appearance: textfield;
}

.input[type='number']::-webkit-inner-spin-button,
.input[type='number']::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.input:focus {
  border-color: var(--nt-ink);
}

.input--error {
  border-color: var(--nt-red);
}

.input--fecha {
  padding: 0 12px;
  color-scheme: dark;
}

.unidad {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--nt-ink-3);
  pointer-events: none;
}

.campo__error {
  color: var(--nt-red);
}

.campo__pista {
  color: var(--nt-ink-3);
}

.aviso {
  color: var(--nt-ok);
}

.cta {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 48px;
  padding: 0 28px;
  border: 1px solid var(--nt-ink);
  border-radius: 999px;
  background: var(--nt-ink);
  color: var(--nt-bg);
  font: 400 11px/12px var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.cta:hover:not(:disabled) {
  background: transparent;
  color: var(--nt-ink);
}

.cta:disabled {
  background: var(--nt-surface-3);
  border-color: transparent;
  color: var(--nt-ink-3);
  cursor: default;
}

@media (max-width: 1023px) {
  .med__cuerpo {
    grid-template-columns: minmax(0, 1fr);
  }

  .med__nueva {
    position: static;
  }
}

@media (max-width: 560px) {
  .med {
    padding: var(--space-lg) var(--space-md);
  }
}
</style>