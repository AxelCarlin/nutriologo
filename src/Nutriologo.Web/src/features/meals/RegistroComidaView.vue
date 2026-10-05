<template>
  <section class="registro">
    <!-- Título dot-matrix: igual de pesado que en Inicio y Chat. -->
    <header class="registro__head">
      <h1 class="registro__titulo">registrar comida</h1>
      <p class="label">{{ hoy }}</p>
    </header>

    <!-- Tipo de comida como chips (viene del catálogo, no texto libre). -->
    <div class="grupo">
      <p class="label">TIPO DE COMIDA</p>
      <div class="chips">
        <button
          v-for="t in tiposComida"
          :key="t.id"
          type="button"
          class="chip"
          :class="{ 'chip--on': idTipoComida === t.id }"
          :aria-pressed="idTipoComida === t.id"
          @click="idTipoComida = t.id"
        >
          {{ t.descripcion }}
        </button>
      </div>
    </div>

    <!-- ============ ALIMENTOS ============ -->
    <div class="grupo">
      <p class="label">ALIMENTOS · {{ filas.length }}</p>

      <ul v-if="filas.length" class="lineas">
        <li v-for="(f, i) in filas" :key="i" class="linea" :class="{ 'linea--error': errores[i] }">
          <!-- Combobox: input + resultados. Reemplaza al <select> nativo. -->
          <div class="combobox">
            <input
              v-model="f.texto"
              class="input combobox__input"
              :class="{ 'combobox__input--sin-resolver': sinResolver(i) }"
              :placeholder="f.nombreAlimento || 'Buscar alimento…'"
              :aria-invalid="sinResolver(i) || undefined"
              :aria-label="`Alimento ${i + 1}`"
              @focus="abrir(i)"
              @input="abrir(i)"
            />
            <ul v-if="abierto === i" class="combobox__lista">
              <li v-if="!candidatos(i).length" class="combobox__vacio label">SIN COINCIDENCIAS</li>
              <li
                v-for="a in candidatos(i)"
                :key="a.nombre"
                class="combobox__opcion"
                @click="elegir(i, a)"
              >
                <span>{{ a.nombre }}</span>
                <span class="num">{{ a.kcalPorBase }} kcal/100g</span>
              </li>
            </ul>
          </div>

          <!-- Stepper propio; los spinners nativos quedan ocultos. -->
          <div class="stepper">
            <button type="button" class="stepper__btn" aria-label="Menos" @click="bajar(i)">−</button>
            <input
              v-model.number="f.cantidad"
              class="input stepper__valor num"
              type="number"
              inputmode="decimal"
              step="1"
              min="0"
              :aria-label="`Cantidad ${i + 1}`"
            />
            <button type="button" class="stepper__btn" aria-label="Más" @click="subir(i)">+</button>
          </div>

          <!-- Unidad: selector de catálogo, no texto libre. -->
          <select v-model="f.unidad" class="input select" :aria-label="`Unidad ${i + 1}`">
            <option v-for="u in unidades" :key="u.codigo" :value="u.codigo">{{ u.codigo.toLowerCase() }}</option>
          </select>

          <!-- kcal en vivo: el dato que faltaba. -->
          <span class="linea__kcal num" :class="{ 'linea__kcal--vacio': !kcalDe(i) }">
            {{ kcalDe(i) ?? '—' }}
            <span class="label">KCAL</span>
          </span>

          <button type="button" class="remove" :aria-label="`Quitar ${f.nombreAlimento}`" @click="quitar(i)">
            <X :size="16" :stroke-width="1.5" />
          </button>

          <p v-if="errores[i]" class="linea__error label">{{ errores[i] }}</p>
        </li>
      </ul>

      <button type="button" class="btn-ghost" @click="agregar">+ añadir alimento</button>
    </div>

    <!-- ============ TOTAL EN VIVO ============ -->
    <div class="total">
      <div class="total__cabeza">
        <p class="label">ESTA COMIDA</p>
        <p class="total__valor">{{ total.toLocaleString('es-MX') }}<span class="label"> KCAL</span></p>
      </div>

      <!-- Barra segmentada del día: gris lo ya consumido, blanco esta comida. -->
      <div class="barra" role="img" :aria-label="`Hoy ${hoyKcal} kcal, esta comida ${total} kcal`">
        <div class="barra__track">
          <div class="barra__previo" :style="{ width: `${pctPrevio}%` }" />
          <div class="barra__nueva" :style="{ width: `${pctNueva}%` }" />
        </div>
        <p class="barra__pie label">
          <span>HOY {{ hoyKcal.toLocaleString('es-MX') }}</span>
          <span v-if="tieneObjetivo && metaCalorica">/ {{ metaCalorica.toLocaleString('es-MX') }}</span>
          <span v-else class="barra__pie-warn">SIN META</span>
        </p>
      </div>
    </div>

    <!-- ============ CTA + estados ============ -->
    <div class="registro__pie">
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <p v-if="aviso" class="form-ok">{{ aviso }}</p>
      <button
        type="button"
        class="cta"
        :disabled="!puedeRegistrar || loading"
        @click="registrar"
      >
        {{ loading ? 'REGISTRANDO' : 'REGISTRAR' }}
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next'
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../shared/api/client'

interface Alimento {
  nombre: string
  kcalPorBase: number | null
  baseGramos: number | null
  baseUnidad: string | null
  porcion: { descripcion: string; cantidad: number; gramos: number | null; unidad: string } | null
}
interface Fila {
  nombreAlimento: string
  texto: string
  cantidad: number
  unidad: string
}

const alimentos = ref<Alimento[]>([])
const unidades = ref<{ codigo: string; simbolo: string | null }[]>([])

// TipoComida no está mapeada en EF; el endpoint /panel la resuelve para las
// comidas registradas. Acá se replica el catálogo por simplicidad del cliente:
// es dato de solo lectura y no muta desde la UI.
const tiposComida = [
  { id: 5, descripcion: 'Desayuno' },
  { id: 1, descripcion: 'Comida' },
  { id: 3, descripcion: 'Colación' },
  { id: 2, descripcion: 'Cena' },
]

const filas = ref<Fila[]>([vacia()])
const idTipoComida = ref<number | null>(5)
const abierto = ref<number | null>(null)
const loading = ref(false)
const error = ref('')
const aviso = ref('')
const hoyKcal = ref(0)
const metaCalorica = ref<number | null>(null)
const tieneObjetivo = ref(false)
/** Datos que trajo el chat: el formulario viene precargado, no vacío. */
const desdeChat = ref(false)

const route = useRoute()
const router = useRouter()

/**
 * Lee los items que dejó el chat en la query (?nombre=&cantidad=&unidad=).
 * Los tres vienen en el mismo orden: se leen por índice, no por emparejar
 * valores sueltos. Si algo no cuadra se ignora la fila, no se inventa.
 */
function filasDesdeChat(): Fila[] | null {
  const nombres = toArray(route.query.nombre)
  const cantidades = toArray(route.query.cantidad)
  const unidadesQ = toArray(route.query.unidad)
  if (!nombres.length) return null

  const out: Fila[] = []
  for (let i = 0; i < nombres.length; i++) {
    const nombre = String(nombres[i] ?? '').trim()
    if (!nombre) continue
    const cantidad = Number(cantidades[i])
    out.push({
      nombreAlimento: nombre,
      texto: nombre,
      cantidad: Number.isFinite(cantidad) && cantidad > 0 ? cantidad : 100,
      unidad: String(unidadesQ[i] ?? 'G').toUpperCase() || 'G',
    })
  }
  return out.length ? out : null
}

function toArray(v: unknown): unknown[] {
  if (v === undefined || v === null) return []
  return Array.isArray(v) ? v : [v]
}

function vacia(): Fila {
  return { nombreAlimento: '', texto: '', cantidad: 100, unidad: 'G' }
}

const hoy = new Date()
  .toLocaleDateString('es-MX', { weekday: 'long', day: '2-digit', month: 'long' })
  .toUpperCase()

function abrir(i: number) {
  abierto.value = i
}

function cerrar() {
  abierto.value = null
}

function candidatos(i: number) {
  const q = filas.value[i]?.texto.trim().toLowerCase() ?? ''
  const opciones = alimentos.value.filter((a) => !q || a.nombre.toLowerCase().includes(q))
  return opciones.slice(0, 8)
}

function elegir(i: number, a: Alimento) {
  filas.value[i]!.nombreAlimento = a.nombre
  // Si el alimento trae porción declarada, se precarga la unidad de esa porción.
  if (a.porcion) filas.value[i]!.unidad = a.porcion.unidad
  cerrar()
}

// kcal en vivo: escala la energía por 100 g a la cantidad pedida.
/**
 * Kcal de una fila, usando la misma aritmética que el backend.
 *
 * Antes dividía siempre por 100, asumiendo que la cantidad venía en gramos:
 * con unidad PIEZA daba `155 × 2 / 100 = 3 kcal` por dos huevos. Ahora:
 *   · si la unidad de la fila es la MISMA que la base del alimento, la cantidad
 *     ya está en la base y se usa directo;
 *   · si la fila usa la porción declarada (1 PIEZA = 50 g), se convierte a
 *     gramos primero: `cantidad × gramos / baseGramos`;
 *   · cualquier otra combinación se devuelve como `null` en vez de inventar un
 *     número: la fila muestra "—" y el cálculo real lo hace el servidor al
 *     registrar.
 */
function kcalDe(i: number): number | null {
  const f = filas.value[i]
  // Fila sin resolver: lo escrito no es el alimento que se guardaría, así que
  // cualquier cifra sería la de otro alimento. Se muestra "—" hasta que se elija.
  if (sinResolver(i)) return null
  if (!f?.nombreAlimento || !f.cantidad || f.cantidad <= 0) return null
  const a = alimentos.value.find((x) => x.nombre === f.nombreAlimento)
  if (!a?.kcalPorBase) return null

  const base = a.baseGramos && a.baseGramos > 0 ? a.baseGramos : 100

  // La unidad de la fila coincide con la base del alimento (kcal cada `base`).
  if (f.unidad === a.baseUnidad) {
    return Math.round((a.kcalPorBase * f.cantidad) / base)
  }

  // La fila usa la porción declarada: hay que pasar a gramos primero.
  const porcion = a.porcion
  if (porcion && porcion.unidad === f.unidad && porcion.gramos && porcion.gramos > 0) {
    const gramos = f.cantidad * porcion.gramos
    return Math.round((a.kcalPorBase * gramos) / base)
  }

  return null
}

const total = computed(() =>
  filas.value.reduce((sum, _, i) => sum + (kcalDe(i) ?? 0), 0),
)

/**
 * ¿Lo que la persona escribió corresponde al alimento que se va a guardar?
 *
 * El combobox tiene dos campos: `texto` es lo que se ve y se escribe, y
 * `nombreAlimento` es lo que se registra. Solo pasan a coincidir al TOCAR una
 * sugerencia. Si no, la fila puede mostrar "Huevo" arriba y guardar "Aceite"
 * (el primero del catálogo, que se preselecciona al abrir), sin avisar: es
 * comida guardada con el alimento equivocado y el usuario no tiene forma de
 * enterarse.
 *
 * Se acepta el texto vacío (todavía no se escribió nada) y el que coincide con
 * el alimento ya elegido; en cualquier otro caso la fila está sin resolver.
 */
function normalizar(s: string): string {
  // NFD separa la tilde de la letra; el rango \u0300-\u036f son los diacríticos
  // combinantes, que es lo que se descarta para comparar "HUEVO" con "Huevo".
  return s.trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
}

function sinResolver(i: number): boolean {
  const f = filas.value[i]
  if (!f) return true
  const escrito = normalizar(f.texto)
  const elegido = normalizar(f.nombreAlimento)
  // Sin escribir nada: no hay contradicción, la fila sigue con el preseleccionado.
  if (!escrito) return false
  return escrito !== elegido
}

// Validación visible por fila.
const errores = computed(() =>
  filas.value.map((f, i) => {
    if (sinResolver(i)) return 'ELEGÍ UN ALIMENTO DE LA LISTA'
    if (!f.nombreAlimento) return 'ELEGÍ UN ALIMENTO'
    if (!f.cantidad || f.cantidad <= 0) return 'CANTIDAD DEBE SER MAYOR A 0'
    return null
  }),
)

const puedeRegistrar = computed(() => filas.value.length > 0 && errores.value.every((e) => e === null))

// Escala de la barra: meta si existe, si no el máximo entre lo previo y el total.
const escalaBarra = computed(() => metaCalorica.value ?? Math.max(hoyKcal.value + total.value, 1))
const pctPrevio = computed(() => Math.min(100, (hoyKcal.value / escalaBarra.value) * 100))
const pctNueva = computed(() => Math.min(100 - pctPrevio.value, (total.value / escalaBarra.value) * 100))

// Paso fijo de 1. Un paso adaptativo (10 con cantidades grandes) obligaba a
// llegar a 12 para poder bajar de a uno; el input numérico cubre el resto.
const PASO = 1

function subir(i: number) {
  const f = filas.value[i]!
  f.cantidad = Math.round((f.cantidad + PASO) * 100) / 100
}

function bajar(i: number) {
  const f = filas.value[i]!
  f.cantidad = Math.max(0, Math.round((f.cantidad - PASO) * 100) / 100)
}

function agregar() {
  filas.value.push(vacia())
  abierto.value = filas.value.length - 1
}

function quitar(i: number) {
  filas.value.splice(i, 1)
  if (!filas.value.length) filas.value.push(vacia())
  cerrar()
}

onMounted(async () => {
  try {
    const [cat, uni, panel] = await Promise.all([
      apiFetch<Alimento[]>('/alimentos'),
      apiFetch<{ codigo: string; simbolo: string | null }[]>('/unidades'),
      apiFetch<{ caloriasConsumidas: number; metaCalorica: number | null; tieneObjetivo: boolean }>('/panel'),
    ])
    alimentos.value = cat
    unidades.value = uni
    hoyKcal.value = panel.caloriasConsumidas
    metaCalorica.value = panel.metaCalorica
    tieneObjetivo.value = panel.tieneObjetivo

    // El chat ya entendió la comida: se precarga y NO se pisa con el primer
    // alimento del catálogo. El usuario revisa y aprieta REGISTRAR.
    const traidas = filasDesdeChat()
    if (traidas) {
      filas.value = traidas
      desdeChat.value = true
      aviso.value = `TRAÍDO DEL CHAT · ${traidas.length} ${traidas.length === 1 ? 'ALIMENTO' : 'ALIMENTOS'} · REVISÁ Y REGISTRÁ`
      // La query ya se consumió: al recargar no vuelve a precargar.
      const { nombre, cantidad, unidad, ...resto } = route.query
      void router.replace({ path: route.path, query: { ...resto } })
    } else if (cat[0]) {
      filas.value[0]!.nombreAlimento = cat[0].nombre
    }
  } catch (e: unknown) {
    error.value = 'No se pudo cargar el catálogo de alimentos.'
  }
})

async function registrar() {
  if (!puedeRegistrar.value || loading.value) return
  loading.value = true
  error.value = ''
  aviso.value = ''
  try {
    const cuerpo = {
      FechaConsumo: new Date().toISOString(),
      // `Nombre` es el texto libre que se muestra; `TipoComida` es el Id con el
      // que el panel agrupa. Antes no se mandaba y toda fila quedaba sin tipo,
      // así que el panel mostraba "Comida" para el desayuno y para la cena igual.
      Nombre: tiposComida.find((t) => t.id === idTipoComida.value)?.descripcion ?? 'Comida',
      TipoComida: idTipoComida.value,
      Items: filas.value
        .filter((f) => f.nombreAlimento)
        .map((f) => ({
          nombreAlimento: f.nombreAlimento,
          cantidad: f.cantidad,
          unidad: f.unidad,
        })),
    }
    const r = await apiFetch<{ id: number }>('/comidas', {
      method: 'POST',
      body: JSON.stringify(cuerpo),
    })
    aviso.value = `REGISTRADO · #${r.id} · +${total.value} KCAL`
    // Limpia el formulario: el siguiente registro arranca de cero.
    filas.value = [vacia()]
    if (alimentos.value[0]) filas.value[0]!.nombreAlimento = alimentos.value[0].nombre
    const panel = await apiFetch<{ caloriasConsumidas: number }>('/panel')
    hoyKcal.value = panel.caloriasConsumidas
    cerrar()
  } catch (e: unknown) {
    const status = (e as { status?: number })?.status
    error.value =
      status === 503
        ? 'El modelo está ocupado. Intentá de nuevo en un momento.'
        : 'No se pudo registrar la comida.'
  } finally {
    loading.value = false
  }
}
</script>

<style>
/* ========== REGISTRAR COMIDA ==========
   Eje propio (--form-w): más angosto que el dashboard de Inicio porque es
   un formulario denso, pero centrado y consistente con Chat. */

.registro {
  width: 100%;
  max-width: var(--form-w);
  margin: 0 auto;
  padding: var(--space-xl) var(--space-md);
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.registro__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-sm);
}

/* Voz dot-matrix, igual que Inicio. Antes era un h2 de 24px. */
.registro__titulo {
  font: 400 clamp(28px, 4vw, 40px) / 1 var(--nt-font-dot);
  text-transform: lowercase;
}

.registro__head .label {
  color: var(--nt-ink-3);
}

.grupo {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

/* --- CHIPS (tipo de comida) --- */
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

/* Activo: relleno blanco. Es el mismo peso que un dato confirmado. */
.chip--on {
  background: var(--nt-ink);
  border-color: var(--nt-ink);
  color: var(--nt-bg);
}

/* --- INPUTS: un solo radio (12px) y sin controles nativos --- */
.input {
  height: 44px;
  padding: 0 12px;
  border-radius: var(--radius-field);
  background: var(--nt-surface-1);
  border: 1px solid var(--nt-line-strong);
  color: var(--nt-ink);
  font: 400 15px/1 var(--nt-font-sans);
  outline: none;
  width: 100%;
  min-width: 0;
}

.input::placeholder {
  color: var(--nt-ink-3);
  font: 400 14px/1 var(--nt-font-sans);
  text-transform: none;
}

.input:focus {
  border-color: var(--nt-ink);
}

/* Spinners nativos fuera: Nothing nunca los expone. */
.input[type='number'] {
  -moz-appearance: textfield;
  appearance: textfield;
}

.input[type='number']::-webkit-inner-spin-button,
.input[type='number']::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

/* Select: appearance:none + flecha propia. */
.select {
  appearance: none;
  -webkit-appearance: none;
  cursor: pointer;
  padding-right: 30px;
  background-image: linear-gradient(45deg, transparent 50%, var(--nt-ink-2) 50%),
    linear-gradient(135deg, var(--nt-ink-2) 50%, transparent 50%);
  background-position: calc(100% - 16px) 50%, calc(100% - 11px) 50%;
  background-size: 5px 5px;
  background-repeat: no-repeat;
  font-family: var(--nt-font-mono);
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.select option {
  background: var(--nt-surface-1);
  color: var(--nt-ink);
}

/* --- LÍNEAS DE ALIMENTO --- */
.lineas {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.linea {
  display: grid;
  grid-template-columns: 1fr auto 72px auto auto;
  align-items: center;
  gap: 8px;
  padding: var(--space-sm);
  border: 1px solid var(--nt-line);
  border-radius: var(--radius-field);
  background: var(--nt-surface-1);
  position: relative;
}

/* Rojo SOLO para error de validación. */
.linea--error {
  border-color: var(--nt-red);
}

.linea__error {
  grid-column: 1 / -1;
  color: var(--nt-red);
}

/* --- COMBOBOX --- */
.combobox {
  position: relative;
}

.combobox__lista {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  z-index: 30;
  list-style: none;
  background: var(--nt-surface-2);
  border: 1px solid var(--nt-line-strong);
  border-radius: var(--radius-field);
  max-height: 260px;
  overflow-y: auto;
  margin: 0;
}

/* El texto escrito no coincide con el alimento que se guardaría: la fila está
   a punto de registrar otro alimento sin decirlo. Se marca en el borde, no
   con un icono, para que se lea de un vistazo. */
.combobox__input--sin-resolver {
  border-color: var(--nt-line-strong);
  border-style: dashed;
}

.combobox__opcion {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-sm);
  padding: 10px 12px;
  font: 400 14px/1.3 var(--nt-font-sans);
  cursor: pointer;
}

.combobox__opcion:hover {
  background: var(--nt-glass);
}

.combobox__opcion .num {
  font-size: 12px;
  color: var(--nt-ink-2);
}

.combobox__vacio {
  padding: 12px;
  color: var(--nt-ink-3);
}

/* --- STEPPER --- */
.stepper {
  display: flex;
  align-items: center;
  gap: 2px;
  border: 1px solid var(--nt-line-strong);
  border-radius: var(--radius-field);
  overflow: hidden;
  height: 44px;
}

.stepper__btn {
  width: 32px;
  height: 100%;
  border: 0;
  background: transparent;
  color: var(--nt-ink-2);
  font-size: 16px;
  cursor: pointer;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.stepper__btn:hover {
  background: var(--nt-glass);
  color: var(--nt-ink);
}

.stepper__valor {
  width: 58px;
  height: 100%;
  border: 0;
  border-radius: 0;
  background: transparent;
  text-align: center;
  padding: 0;
  font-size: 14px;
}

/* --- KCAL EN VIVO --- */
.linea__kcal {
  display: inline-flex;
  align-items: baseline;
  gap: 4px;
  font-size: 15px;
  min-width: 78px;
  justify-content: flex-end;
  color: var(--nt-ink-2);
}

.linea__kcal--vacio {
  color: var(--nt-ink-3);
}

/* --- ELIMINAR: sin borde, rojo en hover --- */
.remove {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border: 0;
  background: transparent;
  color: var(--nt-ink-3);
  cursor: pointer;
  transition: color var(--nt-t-fast) var(--nt-ease);
}

.remove:hover {
  color: var(--nt-red);
}

/* --- TOTAL --- */
.total {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  padding-top: var(--space-md);
  border-top: 1px solid var(--nt-line);
}

.total__cabeza {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-sm);
}

.total__valor {
  font-family: var(--nt-font-dot);
  font-size: clamp(40px, 7vw, 64px);
  line-height: 0.9;
  font-variant-numeric: tabular-nums;
}

/* --- BARRA SEGMENTADA: gris lo previo, blanco lo nuevo --- */
.barra__track {
  display: flex;
  height: 10px;
  gap: 2px;
  background: var(--nt-surface-3);
}

.barra__previo {
  background: var(--nt-ink-3);
  transition: width var(--nt-t-base) var(--nt-ease);
}

.barra__nueva {
  background: var(--nt-ink);
  transition: width var(--nt-t-base) var(--nt-ease);
}

.barra__pie {
  display: flex;
  gap: var(--space-sm);
  color: var(--nt-ink-3);
}

.barra__pie-warn {
  border: 1px dashed var(--nt-line-strong);
  padding: 0 6px;
}

/* --- PIE: CTA anclado + estados --- */
.registro__pie {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-md);
  flex-wrap: wrap;
  padding-top: var(--space-sm);
  border-top: 1px solid var(--nt-line);
}

.registro__pie .form-error,
.registro__pie .form-ok {
  margin-right: auto;
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

@media (max-width: 720px) {
  .linea {
    grid-template-columns: 1fr auto;
    grid-template-areas:
      'combo remove'
      'stepper unidad'
      'kcal kcal';
  }

  .combobox { grid-area: combo; }
  .remove { grid-area: remove; }
  .stepper { grid-area: stepper; }
  .select { grid-area: unidad; }
  .linea__kcal { grid-area: kcal; justify-content: flex-start; }
}
</style>