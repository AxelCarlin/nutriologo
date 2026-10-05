<script setup lang="ts">
import { computed, ref, watch } from 'vue'

/**
 * AppDate — selector de fecha con formato DD MMM AAAA explícito.
 *
 * Existe porque <input type="date"> nativo muestra "10/04/2026" para el 4 de
 * octubre: mes/día/año según el navegador. En México se lee al revés. Acá el
 * placeholder dice "04 OCT 2026" y el valor viaja como YYYY-MM-DD, que es lo
 * único que la API entiende.
 *
 * Envía string, no Date: evita el desfase de zona horaria que ocurre al
 * pasar por new Date("2026-10-04") en UTC.
 */
const props = withDefaults(
  defineProps<{
    modelValue: string | null
    label?: string
    error?: string
  }>(),
  { label: 'Fecha', error: undefined },
)

const emit = defineEmits<{ 'update:modelValue': [string | null] }>()

const MESES = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC']

const partes = computed(() => {
  const v = props.modelValue
  if (!v) return { dia: '', mes: '', anio: '' }
  const [a, m, d] = v.split('-')
  return { dia: d ?? '', mes: m ?? '', anio: a ?? '' }
})

const anios = computed(() => {
  const actual = new Date().getFullYear()
  // 120 años hacia atrás: cubre el rango de vida útil de una app de salud.
  return Array.from({ length: 121 }, (_, i) => actual - 115 + i)
})

/**
 * Estado local de los tres selectores.
 *
 * BUG QUE ARREGLA: antes `emitir` leía las otras dos partes desde `partes`,
 * que deriva de `modelValue`. Como solo se emitía cuando las tres estaban
 * completas, elegir el día emitía `null` y el día se perdía; al elegir el
 * año, `partes.dia` seguía vacío y la fecha nunca se completaba. El usuario
 * veía los 3 selectores llenos y el formulario decía "FECHA REQUERIDA".
 *
 * Ahora la selección parcial vive acá y se emite solo cuando las 3 están.
 */
const sel = ref({ dia: '', mes: '', anio: '' })

// Sincroniza el estado local cuando el padre cambia la fecha (reset, hydrate).
watch(
  () => props.modelValue,
  (v) => {
    sel.value = v
      ? (({ dia, mes, anio }) => ({ dia, mes, anio }))(partes.value)
      : { dia: '', mes: '', anio: '' }
  },
  { immediate: true },
)

function setParte(campo: 'dia' | 'mes' | 'anio', valor: string) {
  sel.value = { ...sel.value, [campo]: valor }
  const { dia, mes, anio } = sel.value
  if (dia && mes && anio) {
    emit('update:modelValue', `${anio}-${mes.padStart(2, '0')}-${dia.padStart(2, '0')}`)
  }
  // Con selección parcial el valor del padre se mantiene: no se emite `null`
  // porque eso borraría las partes ya elegidas.
}

/** Días válidos del mes seleccionado: 31 FEB no existe. */
const diasDelMes = computed(() => {
  const { mes, anio } = sel.value
  if (!mes || !anio) return Array.from({ length: 31 }, (_, i) => String(i + 1))
  const dias = new Date(Number(anio), Number(mes), 0).getDate()
  return Array.from({ length: dias }, (_, i) => String(i + 1))
})

const texto = computed(() => {
  const { dia, mes, anio } = sel.value
  if (!dia || !mes || !anio) return ''
  return `${dia.padStart(2, '0')} ${MESES[Number(mes) - 1]} ${anio}`
})
</script>

<template>
  <div class="fecha">
    <span class="label">{{ label }}</span>
    <div class="fecha__caja" :class="{ 'fecha__caja--error': error }">
      <select
        class="fecha__sel"
        :aria-label="`${label}: día`"
        :value="sel.dia"
        @change="setParte('dia', ($event.target as HTMLSelectElement).value)"
      >
        <option value="">DD</option>
        <option v-for="d in diasDelMes" :key="d" :value="d.padStart(2, '0')">{{ d }}</option>
      </select>
      <select
        class="fecha__sel"
        :aria-label="`${label}: mes`"
        :value="sel.mes"
        @change="setParte('mes', ($event.target as HTMLSelectElement).value)"
      >
        <option value="">MM</option>
        <option v-for="(m, i) in MESES" :key="m" :value="String(i + 1).padStart(2, '0')">{{ m }}</option>
      </select>
      <select
        class="fecha__sel fecha__sel--anio"
        :aria-label="`${label}: año`"
        :value="sel.anio"
        @change="setParte('anio', ($event.target as HTMLSelectElement).value)"
      >
        <option value="">AAAA</option>
        <option v-for="a in anios" :key="a" :value="String(a)">{{ a }}</option>
      </select>
    </div>
    <p v-if="texto || sel.dia || sel.mes || sel.anio" class="fecha__res label">
      {{ texto || 'SELECCIONA DÍA, MES Y AÑO' }}
    </p>
    <p v-if="error" class="fecha__error label">{{ error }}</p>
  </div>
</template>

<style scoped>
.fecha {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.fecha__caja {
  display: grid;
  grid-template-columns: 1fr 1fr 1.4fr;
  gap: 2px;
  padding: 3px;
  border: 1px solid var(--nt-line-strong);
  border-radius: var(--radius-field);
}

.fecha__caja--error {
  border-color: var(--nt-red);
}

.fecha__sel {
  height: 36px;
  border: 0;
  border-radius: calc(var(--radius-field) - 4px);
  background: transparent;
  color: var(--nt-ink);
  font: 400 12px/1 var(--nt-font-mono);
  letter-spacing: 0.06em;
  text-align: center;
  cursor: pointer;
  appearance: none;
  -webkit-appearance: none;
  outline: none;
}

.fecha__sel:hover {
  background: var(--nt-glass);
}

.fecha__sel option {
  background: var(--nt-surface-1);
  color: var(--nt-ink);
}

.fecha__res {
  color: var(--nt-ink-3);
}

.fecha__error {
  color: var(--nt-red);
}
</style>