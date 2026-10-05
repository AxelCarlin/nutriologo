<template>
  <section class="objetivo">
    <!-- ============ COLUMNA IZQUIERDA: FORMULARIO ============ -->
    <form class="form" @submit.prevent="calcular">
      <header class="form__head">
        <h1 class="titulo">objetivo nutricional</h1>
      </header>

      <!-- DATOS -->
      <section class="bloque">
        <p class="label">DATOS</p>

        <AppField v-model="form.pesoKg" label="PESO" unidad="kg" :min="20" :max="400" :step="0.1" />

        <!-- Altura, edad y sexo vienen del perfil: pedirlos otra vez obligaba
             a mantener el mismo dato en dos lugares. Solo se editan en Perfil. -->
        <div v-if="desdePerfil" class="heredado">
          <p class="label">DESDE TU PERFIL</p>
          <dl class="heredado__lista">
            <div class="heredado__fila">
              <dt class="label">ALTURA</dt>
              <dd class="num">{{ form.alturaCm }} CM</dd>
            </div>
            <div class="heredado__fila">
              <dt class="label">EDAD</dt>
              <dd class="num">{{ form.edadAnios }} AÑOS</dd>
            </div>
            <div class="heredado__fila">
              <dt class="label">SEXO</dt>
              <dd class="num">{{ sexoNombre }}</dd>
            </div>
          </dl>
          <!-- En móvil el bloque de 3 filas + enlace ocupa ~200px de lectura:
               se compacta a una sola línea. -->
          <p class="heredado__compacto">
            <span class="num">{{ form.alturaCm }} CM · {{ form.edadAnios }} AÑOS · {{ sexoNombre }}</span>
            <RouterLink to="/perfil" class="enlace">EDITAR</RouterLink>
          </p>
          <RouterLink to="/perfil" class="enlace heredado__enlace">EDITAR EN PERFIL</RouterLink>
        </div>

        <!-- Sin perfil: se piden, porque sin ellos no hay cálculo posible. -->
        <div v-else class="fila-2">
          <AppField v-model="form.alturaCm" label="ALTURA" unidad="cm" :min="100" :max="250" />
          <AppField v-model="form.edadAnios" label="EDAD" unidad="años" :min="13" :max="120" />
          <AppSegmented
            :model-value="form.sexo"
            :options="opcionesSexo"
            label="Sexo"
            @update:model-value="(v) => (form.sexo = Number(v))"
          />
        </div>
      </section>

      <!-- ACTIVIDAD: lista vertical. Cinco opciones no caben en una fila de
           372px: el pill envolvía a dos líneas y se deformaba. -->
      <section class="bloque">
        <p class="label">ACTIVIDAD</p>
        <AppSegmented
          :model-value="nivelActivo"
          :options="opcionesActividad"
          label="Nivel de actividad"
          variante="lista"
          @update:model-value="onActividad"
        />
      </section>

      <!-- META -->
      <section class="bloque">
        <p class="label">META</p>

        <p class="label sub">TIPO DE OBJETIVO</p>
        <AppSegmented
          :model-value="form.tipoObjetivo"
          :options="opcionesTipo"
          label="Tipo de objetivo"
          @update:model-value="(v) => (form.tipoObjetivo = Number(v))"
        />

        <div class="fila-2">
          <AppField
            v-model="form.pesoObjetivoKg"
            label="PESO OBJETIVO"
            unidad="kg"
            :min="20"
            :max="400"
            :step="0.1"
            :error="errores.pesoObjetivo ?? undefined"
          />
          <AppField
            v-model="form.semanasObjetivo"
            label="SEMANAS"
            unidad="sem"
            :min="1"
            :max="520"
            :error="errores.semanas ?? undefined"
          />
        </div>

        <!-- Lectura en vivo: valida antes de calcular, no después. -->
        <p v-if="ritmoPrevio !== null" class="ritmo-previo label">
          RITMO {{ Math.abs(ritmoPrevio).toFixed(2) }} KG/SEM
          <span v-if="form.tipoObjetivo === 0">DE PÉRDIDA</span>
          <span v-else-if="form.tipoObjetivo === 2">DE GANANCIA</span>
        </p>
      </section>

      <div class="form__pie">
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button type="submit" class="cta" :disabled="!puedeCalcular || loading">
          {{ loading ? 'CALCULANDO' : 'CALCULAR' }}
        </button>
      </div>
    </form>

    <!-- ============ COLUMNA DERECHA: RESULTADO ============ -->
    <aside class="result">
      <!-- Calculando -->
      <div v-if="loading" class="result__bits" role="status" aria-label="Calculando">
        <span v-for="n in 3" :key="n" class="bit" :style="{ animationDelay: `${(n - 1) * 160}ms` }" />
      </div>

      <!-- Sin resultado todavía -->
      <div v-else-if="!resultado" class="result__vacio">
        <p class="label">RESULTADO</p>
        <p class="result__placeholder">
          Ajustá los datos y calculá para ver tu meta calórica, el ritmo semanal y
          la evaluación de riesgo.
        </p>
      </div>

      <template v-else>
        <!-- HÉROE: kcal en dot-matrix -->
        <p class="label">META CALÓRICA</p>
        <p class="kcal">{{ Math.round(resultado.metaCaloricaEstimada).toLocaleString('es-MX') }}</p>
        <p class="result__unidad label">KCAL / DÍA</p>

        <!-- Barra: déficit o superávit contra el mantenimiento (TDEE) -->
        <div class="barra">
          <div class="barra__track">
            <div class="barra__fill" :style="{ width: `${pctBarra}%` }" />
          </div>
          <p class="barra__pie label">
            <span v-if="resultado.deficitDiarioRequerido > 0">
              DÉFICIT {{ Math.round(resultado.deficitDiarioRequerido) }} VS. MANTENIMIENTO
            </span>
            <span v-else>MANTENIMIENTO</span>
            <span v-if="resultado.deficitDiarioRequerido > 0">
              TDEE {{ Math.round(resultado.gastoEnergeticoTotal).toLocaleString('es-MX') }}
            </span>
          </p>
        </div>

        <!-- Derivadas: en mono, alineadas -->
        <dl class="derivadas">
          <div class="derivadas__fila">
            <dt class="label">BMR</dt>
            <dd class="num">{{ Math.round(resultado.metabolismoBasal) }}</dd>
          </div>
          <div class="derivadas__fila">
            <dt class="label">TDEE</dt>
            <dd class="num">{{ Math.round(resultado.gastoEnergeticoTotal) }}</dd>
          </div>
          <div class="derivadas__fila">
            <dt class="label">RITMO</dt>
            <dd class="num">{{ Math.abs(resultado.ritmoSemanalKg).toFixed(2) }} KG/SEM</dd>
          </div>
          <div class="derivadas__fila">
            <dt class="label">CAMBIO</dt>
            <dd class="num">{{ resultado.porcentajeCambio.toFixed(1) }}%</dd>
          </div>
        </dl>

        <!-- RIESGO: el único lugar con rojo semántico -->
        <section class="riesgo">
          <p class="label">RIESGO</p>
          <p class="riesgo__nivel" :data-nivel="riesgo.nivel">
            <span class="riesgo__punto" aria-hidden="true" />
            <span>{{ riesgo.etiqueta }}</span>
          </p>
          <p class="riesgo__razon">{{ riesgo.razon }}</p>
          <p v-if="riesgo.ritmo !== null" class="riesgo__dato label">
            {{ Math.abs(riesgo.ritmo).toFixed(2) }} KG/SEM
          </p>
        </section>

        <section v-if="resultado.evaluacionMetaCalorica" class="riesgo">
          <p class="label">RIESGO DE LA META</p>
          <p class="riesgo__nivel" :data-nivel="riesgoMeta.nivel">
            <span class="riesgo__punto" aria-hidden="true" />
            <span>{{ riesgoMeta.etiqueta }}</span>
          </p>
          <p class="riesgo__razon">
            {{ resultado.evaluacionMetaCalorica.descripcion }} ·
            {{ Math.round(resultado.evaluacionMetaCalorica.deficitCalculado) }} KCAL
            ({{ (resultado.evaluacionMetaCalorica.porcentajeDeficit * 100).toFixed(1) }}%)
          </p>
        </section>
      </template>
    </aside>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppField from '../../shared/components/AppField.vue'
import AppSegmented from '../../shared/components/AppSegmented.vue'
import { apiFetch } from '../../shared/api/client'
import { useProfileStore } from '../../shared/stores/profile'

interface Evaluacion {
  codigo: string
  descripcion: string
  idNivelRiesgo: number
}
interface Resultado {
  metabolismoBasal: number
  gastoEnergeticoTotal: number
  deficitDiarioRequerido: number
  metaCaloricaEstimada: number
  ritmoSemanalKg: number
  porcentajeCambio: number
  evaluacionObjetivo: Evaluacion | null
  evaluacionMetaCalorica: (Evaluacion & { deficitCalculado: number; porcentajeDeficit: number }) | null
}

const sexos = [
  { v: 0, t: 'Masculino' },
  { v: 1, t: 'Femenino' },
]
const tipos = [
  { v: 0, t: 'Perder' },
  { v: 1, t: 'Mantener' },
  { v: 2, t: 'Ganar' },
]
const opcionesTipo = computed(() => tipos.map((t) => ({ value: t.v, label: t.t })))

const store = useProfileStore()

const form = ref({
  pesoKg: 92,
  alturaCm: 175,
  edadAnios: 35,
  sexo: 0,
  factorActividad: 1.55,
  tipoObjetivo: 0,
  pesoObjetivoKg: 82,
  semanasObjetivo: 22,
})

/** Si hay perfil, altura/edad/sexo se heredan y no se vuelven a pedir. */
const desdePerfil = computed(() => store.perfil != null)
const sexoNombre = computed(() => store.sexoActual?.nombre ?? '—')

const opcionesSexo = computed(() =>
  sexos.map((s) => ({ value: s.v, label: s.t })),
)
const opcionesActividad = computed(() =>
  store.actividad.map((a) => ({
    value: a.id,
    label: a.nombre,
    hint: a.descripcion,
    meta: `×${a.factor.toFixed(2)}`,
  })),
)
const nivelActivo = computed(
  () => store.actividad.find((a) => a.factor === form.value.factorActividad)?.id ?? null,
)

function onActividad(v: string | number | null) {
  const a = store.actividad.find((x) => x.id === Number(v))
  if (a) form.value.factorActividad = a.factor
}

const loading = ref(false)
const error = ref('')
const resultado = ref<Resultado | null>(null)

const errores = computed(() => {
  const f = form.value
  const e: Record<string, string | null> = { pesoObjetivo: null, semanas: null }
  if (f.tipoObjetivo !== 1) {
    if (!f.pesoObjetivoKg || f.pesoObjetivoKg <= 0) e.pesoObjetivo = 'PESO OBJETIVO REQUERIDO'
    else if (f.pesoObjetivoKg === f.pesoKg) e.pesoObjetivo = 'DEBE DIFERIR DEL PESO ACTUAL'
    if (!f.semanasObjetivo || f.semanasObjetivo <= 0) e.semanas = 'SEMANAS REQUERIDAS'
  }
  return e
})

const puedeCalcular = computed(
  () =>
    form.value.pesoKg > 0 &&
    form.value.alturaCm > 0 &&
    form.value.edadAnios > 0 &&
    Object.values(errores.value).every((x) => x === null),
)

// Ritmo calculado en el cliente para preview: no espera al backend.
const ritmoPrevio = computed(() => {
  const f = form.value
  if (f.tipoObjetivo === 1) return null
  if (!f.pesoObjetivoKg || !f.semanasObjetivo) return null
  return (f.pesoObjetivoKg - f.pesoKg) / f.semanasObjetivo
})

const pctBarra = computed(() => {
  const r = resultado.value
  if (!r?.gastoEnergeticoTotal) return 0
  const ratio = r.metaCaloricaEstimada / r.gastoEnergeticoTotal
  return Math.max(0, Math.min(100, ratio * 100))
})

// NivelRiesgo en DB: 1=MODERADO, 2=ALTO, 3=BAJO. El mapeo anterior en Inicio
// era invertido (1→verde, 3→rojo).
function aRiesgo(e: Evaluacion | null, ritmo: number | null) {
  if (!e) return { nivel: 'desconocido', etiqueta: 'Sin evaluar', razon: '', ritmo }
  const mapa: Record<number, { nivel: string; etiqueta: string }> = {
    1: { nivel: 'moderado', etiqueta: 'Moderado' },
    2: { nivel: 'alto', etiqueta: 'Alto' },
    3: { nivel: 'bajo', etiqueta: 'Bajo' },
  }
  const m = mapa[e.idNivelRiesgo] ?? { nivel: 'desconocido', etiqueta: 'Desconocido' }
  return { ...m, razon: e.descripcion, ritmo }
}

const riesgo = computed(() =>
  aRiesgo(resultado.value?.evaluacionObjetivo ?? null, resultado.value?.ritmoSemanalKg ?? null),
)
const riesgoMeta = computed(() =>
  aRiesgo(resultado.value?.evaluacionMetaCalorica ?? null, null),
)

onMounted(async () => {
  await store.cargarTodo()
  const p = store.perfil
  if (p) {
    form.value.alturaCm = p.altura
    if (store.edad != null) form.value.edadAnios = store.edad
    if (p.idSexoBiologico === 2) form.value.sexo = 1
  }
  if (store.actividadActual) {
    form.value.factorActividad = store.actividadActual.factor
  }
})

async function calcular() {
  if (!puedeCalcular.value || loading.value) return
  loading.value = true
  error.value = ''
  try {
    resultado.value = await apiFetch<Resultado>('/objetivo/calcular', {
      method: 'POST',
      body: JSON.stringify(form.value),
    })
  } catch (e: unknown) {
    const status = (e as { status?: number })?.status
    const detalle = (e as { message?: string })?.message
    error.value =
      status === 400
        ? 'Revisá los datos: el backend rechazó el cálculo.'
        : detalle || 'No se pudo calcular el objetivo.'
    resultado.value = null
  } finally {
    loading.value = false
  }
}
</script>

<style>
/* ========== OBJETIVO NUTRICIONAL ==========
   Dos columnas: formulario izquierda, resultado héroe derecha (sticky).
   Eje compartido con Inicio (--dash-w). */

.objetivo {
  display: grid;
  grid-template-columns: minmax(0, 520px) minmax(0, 1fr);
  gap: var(--space-lg);
  width: 100%;
  max-width: var(--dash-w);
  margin: 0 auto;
  padding: var(--space-xl);
  align-items: start;
}

/* ---------- FORMULARIO ---------- */
.form {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.form__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
}

.bloque {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  padding-top: var(--space-md);
  border-top: 1px solid var(--nt-line);
}

.bloque > .label,
.sub {
  color: var(--nt-ink-2);
}

.sub {
  margin-top: var(--space-xs);
}

/* Campos cortos en fila: 3 arriba, 2 abajo. */
.fila-3 {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
}

.fila-2 {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.campo {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.campo__box {
  position: relative;
}

.input {
  height: 44px;
  width: 100%;
  padding: 0 52px 0 12px;
  border-radius: var(--radius-field);
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line-strong);
  color: var(--nt-ink);
  font: 400 16px/1 var(--nt-font-sans);
  outline: none;
  min-width: 0;
}

/* Spinners nativos fuera del lenguaje. */
.input[type='number'] {
  -moz-appearance: textfield;
  appearance: textfield;
}

.input[type='number']::-webkit-inner-spin-button,
.input[type='number']::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.input.num {
  font-family: var(--nt-font-mono);
  font-size: 15px;
}

.input:focus {
  border-color: var(--nt-ink);
}

.input--error {
  border-color: var(--nt-red);
}

/* Unidad DENTRO del campo, a la derecha: se lee junto al valor. */
.unidad {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--nt-ink-2);
  pointer-events: none;
}

.campo__error {
  color: var(--nt-red);
}

/* ---------- DATOS HEREDADOS ---------- */
.heredado {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: var(--space-sm) 0;
}

.heredado__lista {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--nt-line);
}

.heredado__fila {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-sm);
  padding: 10px 0;
  border-bottom: 1px solid var(--nt-line);
}

.heredado__fila dt {
  color: var(--nt-ink-2);
}

/* Valores en mono MAYÚSCULAS como el resto del sitio: "Masculino" en Grotesk
   capitalizado rompía la voz de datos. */
.heredado__fila dd {
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-size: 14px;
}

/* Compacto solo móvil: una línea en vez de 3 filas + enlace. */
.heredado__compacto {
  display: none;
}

.heredado__compacto .num {
  font-size: 12px;
  letter-spacing: 0.06em;
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

.ritmo-previo {
  color: var(--nt-ink-2);
}

.form__pie {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-md);
  flex-wrap: wrap;
  padding-top: var(--space-sm);
  border-top: 1px solid var(--nt-line);
}

.form__pie .form-error {
  margin-right: auto;
}

.cta {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 48px;
  padding: 0 32px;
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

/* ---------- RESULTADO (sticky) ---------- */
.result {
  position: sticky;
  top: 24px;
  align-self: start;
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  padding: var(--space-lg);
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line);
  border-radius: var(--radius-card);
}

.result > .label {
  color: var(--nt-ink-2);
}

.result__bits {
  display: flex;
  gap: 8px;
  justify-content: center;
  padding: 80px 0;
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

.result__placeholder {
  font: 400 14px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-2);
  max-width: 40ch;
}

/* HÉROE */
.kcal {
  font-family: var(--nt-font-dot);
  font-size: clamp(64px, 8vw, 96px);
  line-height: 0.9;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
  margin-top: var(--space-xs);
}

.result__unidad {
  margin-top: -4px;
}

.barra__track {
  height: 8px;
  background: var(--nt-surface-3);
}

.barra__fill {
  height: 100%;
  background: var(--nt-ink);
  transition: width var(--nt-t-slow) var(--nt-ease);
}

.barra__pie {
  display: flex;
  justify-content: space-between;
  gap: var(--space-sm);
  margin-top: 6px;
  color: var(--nt-ink-2);
}

/* Derivadas con filete, sin cards. */
.derivadas {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--nt-line);
  margin-top: var(--space-sm);
}

.derivadas__fila {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-sm);
  padding: 10px 0;
  border-bottom: 1px solid var(--nt-line);
}

.derivadas__fila dt {
  color: var(--nt-ink-2);
}

.derivadas__fila dd {
  font-size: 15px;
}

/* ---------- RIESGO: el único lugar con rojo semántico ---------- */
.riesgo {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: var(--space-sm);
  padding-top: var(--space-sm);
  border-top: 1px solid var(--nt-line);
}

.riesgo__nivel {
  display: flex;
  align-items: center;
  gap: 8px;
  font: 400 18px/1.2 var(--nt-font-sans);
}

.riesgo__punto {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-ink);
}

/* Rojo SOLO para riesgo alto. Bajo = blanco, no verde: Nothing no usa
   semáforo verde, usa ausencia de alarma. */
.riesgo__nivel[data-nivel='alto'] {
  color: var(--nt-red);
}

.riesgo__nivel[data-nivel='alto'] .riesgo__punto {
  background: var(--nt-red);
}

.riesgo__nivel[data-nivel='moderado'] .riesgo__punto {
  background: var(--nt-warn);
}

.riesgo__razon {
  font: 400 13px/1.45 var(--nt-font-sans);
  color: var(--nt-ink-2);
}

.riesgo__dato {
  color: var(--nt-ink-2);
}

/* En móvil el resultado va debajo del formulario: el CTA se queda fijo
   abajo con safe-area en vez de perderse al fondo del scroll. */
@media (max-width: 1023px) {
  .objetivo {
    grid-template-columns: minmax(0, 1fr);
  }

  .result {
    position: static;
  }

  .fila-3 {
    grid-template-columns: repeat(2, 1fr);
  }

  .form__pie {
    position: sticky;
    bottom: 0;
    background: linear-gradient(transparent, var(--nt-bg) 30%);
    padding: var(--space-sm) 0 calc(var(--space-sm) + env(safe-area-inset-bottom, 0px));
    border-top: 0;
  }
}

@media (max-width: 640px) {
  .objetivo {
    padding: var(--space-lg) var(--space-md);
  }

  .fila-3,
  .fila-2 {
    grid-template-columns: 1fr;
  }

  /* Compacto: una línea en vez de 3 filas + enlace. */
  .heredado__lista,
  .heredado > .label,
  .heredado__enlace {
    display: none;
  }

  .heredado__compacto {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-sm);
    padding: 10px 0;
    border-top: 1px solid var(--nt-line);
    border-bottom: 1px solid var(--nt-line);
  }
}
</style>