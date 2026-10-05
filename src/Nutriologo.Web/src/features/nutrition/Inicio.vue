<template>
  <section class="panel">
    <!-- Cabecera: saludo + fecha. Alineada a la izquierda: Nothing no centra la identidad. -->
    <header class="panel-head">
      <div>
        <p class="label">SALUDO</p>
        <h1 class="panel-head__title">hola, {{ nombre ?? 'usuario' }}</h1>
      </div>
      <p class="label panel-head__fecha">{{ fechaCorta }}</p>
    </header>

    <!-- Estados: carga / error / datos. Nunca "Cargando..." perpetuo. -->
    <div v-if="cargando" class="panel-bits" role="status" aria-label="Cargando datos">
      <span v-for="n in 3" :key="n" class="bit" :style="{ animationDelay: `${(n - 1) * 160}ms` }" />
    </div>

    <div v-else-if="error" class="panel-alert" role="alert">
      <span class="status-dot status-dot--alert" />
      <div>
        <p class="label">ERROR</p>
        <p class="panel-alert__msg">{{ error }}</p>
      </div>
      <button type="button" class="btn-ghost btn-ghost--sm" @click="cargar">Reintentar</button>
    </div>

    <template v-else>
      <!-- ============ HÉROE: calorías del día ============
           No hay denominator porque MetaCalorica está vacía. No se inventa. -->
      <div class="hero">
        <p class="label">CALORÍAS · HOY</p>
        <p class="hero__value">{{ Math.round(kcalHoy).toLocaleString('es-MX') }}</p>
        <p class="hero__meta">
          <span class="label">KCAL</span>
          <span v-if="tieneObjetivo && metaCalorica" class="hero__meta-sep" />
          <span v-if="tieneObjetivo && metaCalorica" class="label">META {{ metaCalorica.toLocaleString('es-MX') }}</span>
          <RouterLink v-else to="/objetivo" class="label hero__meta-warn hero__meta-accion">
            SIN META DEFINIDA · DEFINIR META ›
          </RouterLink>
        </p>

        <!-- Barra segmentada: 7 días. Escala = la meta si existe, si no el
             máximo de la serie. Nunca un 100% inventado. -->
        <div class="bars" role="img" :aria-label="`Serie de 7 días, máximo ${maximoSerie} kcal`">
          <div v-for="d in serie7" :key="d.dia" class="bars__col">
            <!-- Día sin registro: solo la línea base, no la caja entera. Una
                 caja grande y vacía se lee como error de carga, no como cero. -->
            <div class="bars__track" :class="{ 'bars__track--vacia': !d.kcal }">
              <div
                v-if="d.kcal"
                class="bars__bar"
                :class="{ 'bars__bar--hoy': d.hoy }"
                :style="{ height: `${d.altura}%` }"
              />
            </div>
            <span class="bars__label label">{{ d.etiqueta }}</span>
          </div>
        </div>
        <p v-if="!tieneObjetivo" class="bars__pie">
          <span
            class="label"
            title="Sin meta, la serie se normaliza a su propio máximo para poder comparar días"
          >ESCALA RELATIVA</span>
        </p>
      </div>

      <!-- ============ MACROS ============ -->
      <section class="card">
        <div class="card__head">
          <h2 class="label">MACRONUTRIENTES</h2>
          <span class="label">{{ Object.keys(macros).length ? 'HOY' : 'SIN DATOS' }}</span>
        </div>
        <ul v-if="Object.keys(macros).length" class="macros">
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
          Registra alimentos con composición nutricional para ver el desglose de
          proteína, grasa y carbohidratos.
        </p>
      </section>

      <!-- ============ COMIDAS DE HOY ============ -->
      <section class="card">
        <div class="card__head">
          <h2 class="label">COMIDAS · HOY</h2>
          <span class="label">{{ comidas.length }}</span>
        </div>
        <ul v-if="comidas.length" class="rows">
          <li v-for="c in comidas" :key="c.id" class="rows__item">
            <span class="rows__hora num">{{ hora(c.hora) }}</span>
            <span class="rows__cuerpo">
              <span class="rows__titulo">{{ c.nombre ?? 'Sin nombre' }}</span>
              <span v-if="c.tipo" class="label">{{ c.tipo }}</span>
            </span>
            <span class="rows__valor num">{{ Math.round(c.caloriasEstimadas ?? 0) }}</span>
            <span class="label">KCAL</span>
          </li>
        </ul>
        <p v-else class="empty">
          Sin registros hoy. El panel se llena solo en cuanto registres la primera
          comida.
        </p>
      </section>

      <!-- ============ PESO ============ -->
      <section class="card">
        <div class="card__head">
          <h2 class="label">PESO</h2>
          <span v-if="peso" class="label">{{ fechaPeso }}</span>
        </div>
        <div v-if="peso" class="widget">
          <span class="widget__value">{{ peso }}</span>
          <span class="widget__unit label">KG</span>
        </div>
        <p v-else class="empty">
          Sin mediciones de peso.
          <RouterLink to="/mediciones" class="enlace">Registrar la primera</RouterLink>
        </p>
      </section>

      <!-- ============ CTA ÚNICO ============
           Las acciones rápidas duplicaban el sidebar. Un solo CTA principal. -->
      <div class="panel-cta">
        <button type="button" class="cta" @click="irA('/chat')">
          <span class="cta__dot" aria-hidden="true" />
          <span>Consultar al nutriólogo</span>
        </button>
        <RouterLink to="/registrar" class="btn-ghost">Registrar comida</RouterLink>
        <RouterLink to="/objetivo" class="btn-ghost">Definir objetivo</RouterLink>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch } from '../../shared/api/client'
import { userName } from '../../shared/api/auth'

interface Comida {
  id: number
  nombre: string | null
  caloriasEstimadas: number | null
  tipo: string | null
  hora: string
}
interface Panel {
  fecha: string
  caloriasConsumidas: number
  metaCalorica: number | null
  tieneObjetivo: boolean
  tienePerfil: boolean
  peso: number | null
  fechaPeso: string | null
  macros: Record<string, number>
  comidas: Comida[]
  serie: { dia: string; kcal: number }[]
}

const router = useRouter()
const nombre = computed(() => userName.value?.trim().toLowerCase() ?? null)

const cargando = ref(true)
const error = ref('')
const kcalHoy = ref(0)
const metaCalorica = ref<number | null>(null)
const tieneObjetivo = ref(false)
const peso = ref<number | null>(null)
const fechaPeso = ref<string | null>(null)
const macros = ref<Record<string, number>>({})
const comidas = ref<Comida[]>([])
const serie = ref<{ dia: string; kcal: number }[]>([])

const irA = (p: string) => router.push(p)

const ETIQUETA_MACRO: Record<string, string> = {
  PROTEINA: 'PROTEÍNA',
  GRASA: 'GRASA',
  CARBOHIDRATO: 'CARBOHIDRATO',
}

const fechaCorta = computed(() =>
  new Date().toLocaleDateString('es-MX', { day: '2-digit', month: 'short' }).toUpperCase(),
)

/**
 * El backend guarda wall-clock UTC sin offset (`timestamp` + ambos writers en
 * UTC). Sin la 'Z', JS lo parsea como hora local y muestra la hora UTC como
 * si fuera local (desfase de -6h en México). Con 'Z' se convierte bien.
 * Limitación conocida: el agrupamiento por día sigue siendo día UTC, así que
 * una comida de las 19:33 locales aparece bajo el día UTC siguiente.
 */
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

const maximoSerie = computed(() => {
  const max = Math.max(...serie.value.map((d) => d.kcal), 0)
  return metaCalorica.value ?? (max || 1)
})

const serie7 = computed(() => {
  const hoy = new Date().toDateString()
  return serie.value.slice(-7).map((d) => ({
    dia: d.dia,
    kcal: d.kcal,
    hoy: new Date(d.dia).toDateString() === hoy,
    etiqueta: new Date(d.dia).toLocaleDateString('es-MX', { weekday: 'narrow' }).toUpperCase(),
    altura: Math.min(100, Math.round((d.kcal / maximoSerie.value) * 100)),
  }))
})

async function cargar() {
  cargando.value = true
  error.value = ''
  try {
    const p = await apiFetch<Panel>('/panel')
    kcalHoy.value = p.caloriasConsumidas
    metaCalorica.value = p.metaCalorica
    tieneObjetivo.value = p.tieneObjetivo
    peso.value = p.peso
    fechaPeso.value = p.fechaPeso ? new Date(p.fechaPeso).toLocaleDateString('es-MX') : null
    macros.value = p.macros ?? {}
    comidas.value = p.comidas ?? []
    serie.value = p.serie ?? []
  } catch (e: unknown) {
    // apiFetch lanza {status, message}: no es una Error, hay que leer status.
    const status = (e as { status?: number })?.status
    error.value =
      status === 401
        ? 'Sesión expirada. Volvé a entrar.'
        : `No se pudo cargar el panel${status ? ` (HTTP ${status})` : ''}.`
  } finally {
    cargando.value = false
  }
}

onMounted(cargar)
</script>

<style>
/* ========== PANEL (Inicio) ==========
   Gramatica Nothing aplicada a la critiquerceived:
   hero unico en dot-matrix, superficies planas, hairline, mono MAYUSCULAS,
   rojo reservado para error/activo. Sin datos inventados: cada bloque
   declara su propio empty state. */

.panel {
  width: 100%;
  max-width: var(--dash-w);
  margin: 0 auto;
  padding: var(--space-xl);
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.panel-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-md);
}

/* Voz dot-matrix: la firma de identidad, reservada para cifras dominantes. */
.panel-head__title {
  font: 400 clamp(28px, 4vw, 44px) / 1 var(--nt-font-dot);
  text-transform: lowercase;
  letter-spacing: 0;
  margin-top: var(--space-xs);
}

.panel-head__fecha {
  color: var(--nt-ink-3);
}

/* ---- Loader: bits en cascada, no texto plano ---- */
.panel-bits {
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

/* ---- Alerta: unico lugar donde el rojo codifica estado ---- */
.panel-alert {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  padding: var(--space-md);
  border: 1px solid var(--nt-red);
  border-radius: var(--radius-input);
}

.panel-alert__msg {
  font: 400 14px/1.45 var(--nt-font-sans);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-ink-3);
  flex-shrink: 0;
}

.status-dot--alert {
  background: var(--nt-red);
}

/* ---- HERO ---- */
.hero {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.hero__value {
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

.hero__meta-sep {
  width: 1px;
  height: 12px;
  background: var(--nt-line-strong);
}

/* Sin meta: el aviso es gris, no rojo. Rojo queda para error real. */
.hero__meta-warn {
  color: var(--nt-ink-3);
  border: 1px dashed var(--nt-line-strong);
  padding: 2px 8px;
}

/* ---- BARRA SEGMENTADA (7 días) ---- */
.bars {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 6px;
  margin-top: var(--space-md);
}

.bars__col {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.bars__track {
  height: 72px;
  display: flex;
  align-items: flex-end;
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line);
}

.bars__bar {
  width: 100%;
  min-height: 2px;
  background: var(--nt-ink-3);
  transition: height var(--nt-t-slow) var(--nt-ease);
}

.bars__bar--hoy {
  background: var(--nt-ink);
}

/* Día sin registro: solo la línea base. La caja entera vacía parecía
   un error de carga; así se distingue "sin registro" de "cero". */
.bars__track--vacia {
  height: 72px;
  background: transparent;
  border: 0;
  border-bottom: 1px solid var(--nt-line);
}

.hero__meta-accion {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.hero__meta-accion:hover {
  color: var(--nt-ink);
}

.bars__label {
  text-align: center;
  color: var(--nt-ink-2);
  font-size: 10px;
}

.bars__pie {
  color: var(--nt-ink-2);
}

/* ---- CARDS ---- */
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

/* Sin `font: inherit`: esa regla (0,1,1) ganaba a `.label` (0,1,0) y los
   títulos salían en Grotesk ~16px en vez de mono 11px. */
.card__head h2 {
  color: var(--nt-ink-2);
}

.empty {
  font: 400 14px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-3);
  max-width: 52ch;
}

.empty code {
  font-family: var(--nt-font-mono);
  font-size: 12px;
  border: 1px solid var(--nt-line);
  padding: 0 4px;
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

/* ---- MACROS: barra + cifra mono ---- */
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

/* ---- FILAS (comidas) ---- */
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

/* ---- WIDGET (peso) ---- */
.widget {
  display: flex;
  align-items: baseline;
  gap: var(--space-sm);
}

.widget__value {
  font-family: var(--nt-font-dot);
  font-size: clamp(48px, 8vw, 72px);
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

/* ---- CTA ---- */
.panel-cta {
  display: flex;
  gap: var(--space-sm);
  flex-wrap: wrap;
  align-items: center;
  padding-top: var(--space-xs);
}

.cta {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  height: 44px;
  padding: 0 20px;
  border: 1px solid var(--nt-ink);
  border-radius: var(--radius-input);
  background: var(--nt-ink);
  color: var(--nt-bg);
  font: 400 11px/12px var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.cta:hover {
  background: transparent;
  color: var(--nt-ink);
}

/* Rojo: un solo punto, marca el CTA activo. */
.cta__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--nt-red);
}

.btn-ghost--sm {
  height: 32px;
  padding: 0 12px;
}

@media (max-width: 640px) {
  .panel {
    padding: var(--space-lg) var(--space-md);
  }

  .macros__row {
    grid-template-columns: 88px 1fr auto auto;
  }

  .bars__track {
    height: 56px;
  }
}
</style>