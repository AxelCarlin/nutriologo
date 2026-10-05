<script setup lang="ts">
import { computed } from 'vue'

/**
 * AppField — input numérico con unidad dentro y stepper propio.
 *
 * Sustituye <input type="number"> nativo: sus spinners son diminutos, grises y
 * no se alinean con nada. Acá el stepper tiene 32px de área de toque y el valor
 * va en mono centrado.
 */
const props = withDefaults(
  defineProps<{
    modelValue: number | null
    label: string
    unidad?: string
    min?: number
    max?: number
    step?: number
    error?: string
    hint?: string
    placeholder?: string
  }>(),
  { unidad: '', step: 1, min: undefined, max: undefined, error: undefined, hint: undefined, placeholder: undefined },
)

const emit = defineEmits<{ 'update:modelValue': [number | null] }>()

const fueraDeRango = computed(() => {
  const v = props.modelValue
  if (v == null) return false
  if (props.min != null && v < props.min) return true
  if (props.max != null && v > props.max) return true
  return false
})

const rangoTexto = computed(() => {
  if (props.min == null && props.max == null) return null
  return `RANGO ${props.min ?? '−'}–${props.max ?? '−'} ${props.unidad}`.trim()
})

function ajustar(delta: number) {
  const base = props.modelValue ?? (props.min ?? 0)
  let v = base + delta * props.step
  if (props.min != null) v = Math.max(props.min, v)
  if (props.max != null) v = Math.min(props.max, v)
  emit('update:modelValue', Math.round(v * 1000) / 1000)
}

function alTeclear(e: Event) {
  const el = e.target as HTMLInputElement
  const raw = el.value.trim()
  if (raw === '') {
    emit('update:modelValue', null)
    return
  }
  const n = Number(raw.replace(',', '.'))
  if (Number.isFinite(n)) {
    emit('update:modelValue', n)
    return
  }
  // No es un número: revierte el texto para no dejar basura en el input.
  // type="text" (no "number") porque el nativo no permite placeholder y
  // muestra spinners; el formateo vive en alTeclear.
  el.value = props.modelValue == null ? '' : String(props.modelValue)
  emit('update:modelValue', props.modelValue)
}
</script>

<template>
  <div class="campo">
    <span class="label">{{ label }}</span>
    <div class="campo__caja" :class="{ 'campo__caja--error': error || fueraDeRango }">
      <button
        type="button"
        class="stepper__btn"
        :aria-label="`Restar ${step} a ${label}`"
        :disabled="fueraDeRango && (modelValue ?? 0) <= (min ?? -Infinity)"
        @click="ajustar(-1)"
      >
        −
      </button>
      <input
        class="campo__input num"
        type="text"
        inputmode="decimal"
        :value="modelValue ?? ''"
        :placeholder="placeholder"
        :aria-label="label"
        @input="alTeclear"
      />
      <button
        type="button"
        class="stepper__btn"
        :aria-label="`Sumar ${step} a ${label}`"
        :disabled="fueraDeRango && (modelValue ?? 0) >= (max ?? Infinity)"
        @click="ajustar(1)"
      >
        +
      </button>
    </div>
    <p v-if="error" class="campo__error label">{{ error }}</p>
    <p v-else-if="fueraDeRango" class="campo__error label">FUERA DE RANGO · {{ rangoTexto }}</p>
    <p v-else-if="hint" class="campo__hint label">{{ hint }}</p>
    <p v-else-if="unidad && rangoTexto" class="campo__hint label">{{ rangoTexto }}</p>
  </div>
</template>

<style scoped>
.campo {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.campo__caja {
  display: flex;
  align-items: center;
  height: 44px;
  border: 1px solid var(--nt-line-strong);
  border-radius: var(--radius-field);
  overflow: hidden;
}

.campo__caja:focus-within {
  border-color: var(--nt-ink);
}

.campo__caja--error {
  border-color: var(--nt-red);
}

.campo__input {
  flex: 1;
  min-width: 0;
  height: 100%;
  padding: 0 8px;
  border: 0;
  background: transparent;
  color: var(--nt-ink);
  /* 16px: debajo de eso iOS hace zoom al enfocar y la pantalla salta. */
  font: 400 16px/1 var(--nt-font-mono);
  text-align: center;
  outline: none;
}

.campo__input::placeholder {
  color: var(--nt-ink-3);
  font: 400 13px/1 var(--nt-font-mono);
}

.stepper__btn {
  /* 44×44 reales (el contenedor mide 44px de alto): área táctil mínima. */
  width: 44px;
  height: 100%;
  border: 0;
  background: transparent;
  color: var(--nt-ink-2);
  font-size: 16px;
  cursor: pointer;
  flex-shrink: 0;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.stepper__btn:hover:not(:disabled) {
  background: var(--nt-glass);
  color: var(--nt-ink);
}

.stepper__btn:disabled {
  opacity: 0.3;
  cursor: default;
}

.campo__error {
  color: var(--nt-red);
}

.campo__hint {
  color: var(--nt-ink-3);
}
</style>