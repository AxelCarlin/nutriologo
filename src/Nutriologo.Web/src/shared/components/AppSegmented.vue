<script setup lang="ts">
import { computed } from 'vue'

/**
 * AppSegmented — control segmentado.
 *
 * Reemplaza <select> para 2–5 opciones. Un select de 3 opciones es un click de
 * más y esconde las opciones; el segmented las muestra todas.
 *
 * Nothing: mono MAYÚSCULAS, tracking 0.08em, activo = relleno blanco.
 */
/**
 * El hint se usa SOLO como tooltip del botón (`title`).
 *
 * Antes también se imprimía como línea visible debajo del control. Eso
 * duplicaba el texto cuando la vista ya muestra su propia descripción
 * debajo: en Objetivo aparecía "3–5 días de ejercicio por semana" dos veces,
 * una sin el factor y otra con él.
 *
 * `descripcion` opt-in por si alguna vista quiere el hint en línea.
 */
const props = withDefaults(
  defineProps<{
    modelValue: string | number | null
    options: { value: string | number; label: string; hint?: string; meta?: string }[]
    label?: string
    descripcion?: boolean
    /**
     * 'pildora' (default): 2–4 opciones en una fila.
     * 'lista': filas verticales de 48px con descripción visible. Para 5+
     * opciones que no caben en 372px: un pill que envuelve a dos filas se
     * deforma y los targets quedan pegados.
     */
    variante?: 'pildora' | 'lista'
  }>(),
  { label: undefined, descripcion: false, variante: 'pildora' },
)

const emit = defineEmits<{ 'update:modelValue': [string | number | null] }>()

const hintActiva = computed(
  () => props.options.find((o) => o.value === props.modelValue)?.hint,
)
</script>

<template>
  <div class="seg">
    <!-- Variante pildora: fila única, hint solo como tooltip. -->
    <div v-if="variante === 'pildora'" class="seg__track" role="radiogroup" :aria-label="label">
      <button
        v-for="o in options"
        :key="o.value"
        type="button"
        class="seg__btn"
        role="radio"
        :aria-checked="modelValue === o.value"
        :title="o.hint"
        @click="emit('update:modelValue', o.value)"
      >
        {{ o.label }}
      </button>
    </div>
    <!-- Variante lista: una fila de 48px por opción, descripción visible.
         La descripción ya no vive en un párrafo aparte que duplique texto. -->
    <div v-else class="seg__list" role="radiogroup" :aria-label="label">
      <button
        v-for="o in options"
        :key="o.value"
        type="button"
        class="seg__row"
        role="radio"
        :aria-checked="modelValue === o.value"
        @click="emit('update:modelValue', o.value)"
      >
        <span class="seg__dot" aria-hidden="true" />
        <span class="seg__row-text">
          <span class="seg__row-label">{{ o.label }}</span>
          <span v-if="o.hint" class="seg__row-hint">{{ o.hint }}</span>
        </span>
        <span v-if="o.meta" class="seg__row-meta num">{{ o.meta }}</span>
      </button>
    </div>
    <p v-if="descripcion && hintActiva" class="seg__hint label">{{ hintActiva }}</p>
  </div>
</template>

<style scoped>
.seg {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.seg__track {
  display: flex;
  gap: 2px;
  padding: 3px;
  border: 1px solid var(--nt-line-strong);
  border-radius: 999px;
  flex-wrap: wrap;
}

.seg__btn {
  flex: 1;
  min-width: 64px;
  height: 34px;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: var(--nt-ink-2);
  font: 400 11px/1 var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: background var(--nt-t-fast) var(--nt-ease),
    color var(--nt-t-fast) var(--nt-ease);
}

.seg__btn:hover {
  color: var(--nt-ink);
}

.seg__btn[aria-checked='true'] {
  background: var(--nt-ink);
  color: var(--nt-bg);
}

.seg__hint {
  color: var(--nt-ink-2);
}

/* --- Variante lista: filas de 48px, filete, sin pill --- */
.seg__list {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--nt-line);
}

.seg__row {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 48px;
  padding: 10px 0;
  border: 0;
  border-bottom: 1px solid var(--nt-line);
  background: transparent;
  color: var(--nt-ink-2);
  text-align: left;
  cursor: pointer;
  width: 100%;
}

/* Activo: punto blanco + texto blanco. Mismo lenguaje que el nav. */
.seg__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: transparent;
  border: 1px solid var(--nt-line-strong);
  flex-shrink: 0;
  transition: background var(--nt-t-fast) var(--nt-ease),
    border-color var(--nt-t-fast) var(--nt-ease);
}

.seg__row[aria-checked='true'] {
  color: var(--nt-ink);
}

.seg__row[aria-checked='true'] .seg__dot {
  background: var(--nt-ink);
  border-color: var(--nt-ink);
}

.seg__row-text {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.seg__row-label {
  font: 400 11px/1.2 var(--nt-font-mono);
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.seg__row-hint {
  font: 400 12px/1.4 var(--nt-font-sans);
  color: var(--nt-ink-2);
  text-transform: none;
  letter-spacing: 0;
}

.seg__row-meta {
  font-size: 13px;
  color: var(--nt-ink-2);
  flex-shrink: 0;
}

.seg__row[aria-checked='true'] .seg__row-meta {
  color: var(--nt-ink);
}
</style>