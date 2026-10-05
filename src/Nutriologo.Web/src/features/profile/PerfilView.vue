<template>
  <section class="perfil">
    <!-- ============ IDENTIDAD ============ -->
    <header class="perfil__head">
      <span class="avatar" aria-hidden="true">{{ inicial }}</span>
      <div class="perfil__id">
        <p class="perfil__nombre">{{ nombre }}</p>
        <p v-if="derivados.length" class="derivados">
          <span v-for="d in derivados" :key="d.k">{{ d.v }}&nbsp;·&nbsp;</span>
        </p>
        <p v-else class="derivados">CARGANDO…</p>
      </div>
    </header>

    <div v-if="error" class="alerta" role="alert">
      <span class="alerta__punto" aria-hidden="true" />
      <span>{{ error }}</span>
    </div>

    <form class="perfil__form" @submit.prevent="guardar">
      <!-- DATOS PERSONALES -->
      <section class="bloque">
        <p class="label">DATOS PERSONALES</p>

        <AppDate v-model="nacimiento" label="NACIMIENTO" :error="errores.nacimiento" />

        <AppField
          v-model="altura"
          label="ALTURA"
          unidad="cm"
          :min="100"
          :max="250"
          :step="1"
          :error="errores.altura"
        />

        <p class="label sub">SEXO BIOLÓGICO</p>
        <AppSegmented
          :model-value="sexo"
          :options="opcionesSexo"
          label="Sexo biológico"
          @update:model-value="onSexo"
        />
      </section>

      <!-- ACTIVIDAD -->
      <section class="bloque">
        <p class="label">ACTIVIDAD</p>
        <AppSegmented
          :model-value="nivelActividad"
          :options="opcionesActividad"
          label="Nivel de actividad"
          variante="lista"
          @update:model-value="onActividad"
        />
      </section>

      <p class="nota">
        Estos datos se usan para calcular tu gasto calórico y tu meta. No se
        comparten con nadie.
      </p>

      <div class="perfil__pie">
        <p v-if="sucio" class="sucio label">
          <span class="sucio__punto" aria-hidden="true" />
          SIN GUARDAR
        </p>
        <p v-if="aviso" class="aviso label">{{ aviso }}</p>
        <button v-if="sucio" type="button" class="btn-ghost" @click="descartar">Cancelar</button>
        <button type="submit" class="cta" :disabled="!sucio || loading || erroresPendientes">
          {{ loading ? 'GUARDANDO' : 'GUARDAR' }}
        </button>
      </div>
    </form>

    <!-- ============ CUENTA ============ -->
    <section class="cuenta">
      <p class="label">CUENTA</p>
      <button type="button" class="cuenta__fila" @click="salir">
        <span>Cerrar sesión</span>
        <LogOut :size="16" :stroke-width="1.5" aria-hidden="true" />
      </button>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { LogOut } from 'lucide-vue-next'
import AppDate from '../../shared/components/AppDate.vue'
import AppField from '../../shared/components/AppField.vue'
import AppSegmented from '../../shared/components/AppSegmented.vue'
import { logout, userName } from '../../shared/api/auth'
import { apiFetch } from '../../shared/api/client'
import { useProfileStore } from '../../shared/stores/profile'

const router = useRouter()
const store = useProfileStore()

// userName es un ref que se hidrata en import; en tests (y tras un login
// en otra pestaña) puede quedar desactualizado. localStorage es la fuente.
const nombre = computed(() => (localStorage.getItem('nutriologo_nombre') || userName.value || 'Usuario').toUpperCase())
const inicial = computed(() => nombre.value.charAt(0))

// --- Estado del formulario (copia editable del perfil) ---
const nacimiento = ref<string | null>(null)
const altura = ref<number | null>(null)
const sexo = ref<number | null>(null)
const nivelActividad = ref<number | null>(null)
const loading = ref(false)
const error = ref('')
const aviso = ref('')

// --- Original, para detectar cambios sin guardar ---
const original = ref('')

const firma = () => JSON.stringify({
  n: nacimiento.value,
  a: altura.value,
  s: sexo.value,
  v: nivelActividad.value,
})

const sucio = computed(() => firma() !== original.value)

const opcionesSexo = computed(() =>
  store.sexos.map((s) => ({ value: s.id, label: s.nombre })),
)
const opcionesActividad = computed(() =>
  store.actividad.map((a) => ({
    value: a.id,
    label: a.nombre,
    hint: a.descripcion,
    meta: `×${a.factor.toFixed(2)}`,
  })),
)

const errores = computed(() => ({
  nacimiento:
    !nacimiento.value
      ? 'FECHA REQUERIDA'
      : new Date(nacimiento.value) > new Date()
        ? 'FECHA FUTURA'
        : undefined,
  altura: altura.value == null ? 'ALTURA REQUERIDA' : undefined,
}))
const erroresPendientes = computed(() => Object.values(errores.value).some(Boolean))

// --- Datos derivados: dan contexto de por qué importan estos campos ---
// El peso no está en el perfil (vive en MedicionCorporal), así que se pide la
// última medición. Sin esto, IMC y TMB no se pueden calcular.
const ultimaMedicion = ref<{ idTipoMedicionCorporal: number; valor: number } | null>(null)

const peso = computed(() => {
  const t = store.tiposMedicion.find((x) => x.codigo === 'PESO')
  if (!t) return null
  return ultimaMedicion.value?.idTipoMedicionCorporal === t.id
    ? ultimaMedicion.value.valor
    : null
})

const bmi = computed(() => {
  const p = peso.value
  const a = altura.value
  if (!p || !a) return null
  const m = a / 100
  return Math.round((p / (m * m)) * 10) / 10
})

const bmiTexto = computed(() => {
  const b = bmi.value
  if (b == null) return null
  const nivel = b < 18.5 ? 'BAJO' : b < 25 ? 'NORMAL' : b < 30 ? 'ALTO' : 'OBESIDAD'
  return `IMC ${b.toFixed(1)} · ${nivel}`
})

const tmb = computed(() => {
  const p = peso.value
  const a = altura.value
  const e = store.edad
  if (!p || !a || !e || sexo.value == null) return null
  // Mifflin-St Jeor: coincide con la fórmula del backend.
  const s = sexo.value === 1 ? -161 : 5
  return Math.round(10 * p + 6.25 * a - 5 * e + s)
})

const derivados = computed(() =>
  [
    store.edad != null ? { k: 'edad', v: `${store.edad} años` } : null,
    altura.value ? { k: 'altura', v: `${altura.value} cm` } : null,
    bmiTexto.value ? { k: 'imc', v: bmiTexto.value } : null,
    tmb.value ? { k: 'tmb', v: `TMB ${tmb.value.toLocaleString('es-MX')} kcal` } : null,
  ].filter(Boolean) as { k: string; v: string }[],
)

function onSexo(v: string | number | null) {
  sexo.value = v == null ? null : Number(v)
}
function onActividad(v: string | number | null) {
  nivelActividad.value = v == null ? null : Number(v)
}

function hidratar() {
  const p = store.perfil
  nacimiento.value = p ? p.fechaNacimiento.slice(0, 10) : null
  altura.value = p?.altura ?? null
  sexo.value = p?.idSexoBiologico ?? null
  nivelActividad.value = p?.idNivelActividadFisica ?? null
  original.value = firma()
}

function descartar() {
  hidratar()
  aviso.value = ''
  error.value = ''
}

async function guardar() {
  if (!sucio.value || erroresPendientes.value) return
  loading.value = true
  error.value = ''
  aviso.value = ''
  try {
    await store.guardarPerfil({
      idSexoBiologico: sexo.value!,
      idNivelActividadFisica: nivelActividad.value!,
      fechaNacimiento: new Date(`${nacimiento.value}T12:00:00`).toISOString(),
      altura: altura.value!,
    })
    original.value = firma()
    aviso.value = 'PERFIL GUARDADO'
    setTimeout(() => (aviso.value = ''), 3000)
  } catch (e: unknown) {
    const status = (e as { status?: number })?.status
    error.value =
      status === 400
        ? 'El backend rechazó los datos. Revisá los campos.'
        : 'No se pudo guardar el perfil.'
  } finally {
    loading.value = false
  }
}

function salir() {
  logout()
  router.replace('/login')
}

// Limpia el aviso de "sin guardar" si el usuario no volvió a editar.
watch([nacimiento, altura, sexo, nivelActividad], () => {
  if (aviso.value) aviso.value = ''
})

onMounted(async () => {
  await store.cargarTodo()
  hidratar()
  try {
    const pesoId = store.tiposMedicion.find((t) => t.codigo === 'PESO')?.id
    if (pesoId) {
      const meds = await apiFetch<{ idTipoMedicionCorporal: number; valor: number }[]>(
        `/mediciones?tipo=${pesoId}`,
      )
      ultimaMedicion.value = meds[0] ?? null
    }
  } catch {
    // Sin peso no hay IMC ni TMB; la vista funciona igual.
    ultimaMedicion.value = null
  }
})
</script>

<style>
/* ========== PERFIL ==========
   Eje compartido (--dash-w). Dos columnas: identidad+form a la izquierda,
   cuenta separada al pie por filete. */

.perfil {
  width: 100%;
  max-width: var(--dash-w);
  margin: 0 auto;
  padding: var(--space-xl);
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

/* ---- Identidad ---- */
.perfil__head {
  display: flex;
  align-items: center;
  gap: 20px;
  padding-bottom: var(--space-lg);
  border-bottom: 1px solid var(--nt-line);
}

/* Avatar: cuadrado blanco con la inicial en dot-matrix. Mismo lenguaje que
   el logo "N" del sidebar. */
.avatar {
  width: 64px;
  height: 64px;
  border-radius: var(--radius-field);
  background: var(--nt-ink);
  color: var(--nt-bg);
  display: grid;
  place-items: center;
  font: 400 32px/1 var(--nt-font-dot);
  flex-shrink: 0;
}

.perfil__id {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.perfil__nombre {
  font: 400 20px/1.2 var(--nt-font-sans);
}

.derivados {
  font: 400 11px/1.6 var(--nt-font-mono);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--nt-ink-2);
}

/* El uppercase de la línea derivada lo aplica el CSS, no el texto: los tests
   verifican el contenido real, no la presentación. */
.derivados :is(span, dd) {
  text-transform: none;
}

.alerta {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: var(--space-sm) var(--space-md);
  border: 1px solid var(--nt-red);
  border-radius: var(--radius-field);
  font: 400 13px/1.4 var(--nt-font-sans);
}

.alerta__punto {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--nt-red);
  flex-shrink: 0;
}

/* ---- Formulario ---- */
.perfil__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
  max-width: 520px;
}

.bloque {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  padding-top: var(--space-md);
  border-top: 1px solid var(--nt-line);
}

.sub {
  margin-top: var(--space-xs);
}

.nota {
  font: 400 13px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-3);
  max-width: 48ch;
}

.perfil__pie {
  display: flex;
  align-items: center;
  gap: var(--space-md);
  flex-wrap: wrap;
  padding-top: var(--space-sm);
  border-top: 1px solid var(--nt-line);
}

.sucio {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-right: auto;
  color: var(--nt-ink-2);
}

/* Punto rojo: único uso del acento como "acción pendiente". */
.sucio__punto {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--nt-red);
}

.aviso {
  color: var(--nt-ok);
}

.btn-ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 44px;
  padding: 0 20px;
  border: 1px solid var(--nt-ink);
  border-radius: 999px;
  background: transparent;
  color: var(--nt-ink);
  font: 400 11px/12px var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.btn-ghost:hover {
  background: var(--nt-ink);
  color: var(--nt-bg);
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

/* ---- Cuenta ---- */
.cuenta {
  display: flex;
  flex-direction: column;
  padding-top: var(--space-md);
  border-top: 1px solid var(--nt-line);
  max-width: 520px;
}

.cuenta__fila {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  border: 0;
  border-bottom: 1px solid var(--nt-line);
  background: transparent;
  color: var(--nt-ink);
  font: 400 14px/1 var(--nt-font-sans);
  text-align: left;
  cursor: pointer;
  transition: color var(--nt-t-fast) var(--nt-ease);
}

.cuenta__fila:hover {
  color: var(--nt-red);
}

@media (max-width: 560px) {
  .perfil {
    padding: var(--space-lg) var(--space-md);
  }
}
</style>