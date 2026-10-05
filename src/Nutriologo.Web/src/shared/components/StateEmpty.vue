<script setup lang="ts">


/**
 * Estado vacío explícito. Antes cada vista resolvía "no hay datos" con
 * ausencia de contenido: el usuario no distinguía "cargando" de "vacío" de
 * "rompió".
 */
defineProps<{
  titulo: string
  mensaje: string
  cargando?: boolean
}>()
</script>

<template>
  <div v-if="cargando" class="vacio__bits" role="status" aria-label="Cargando">
    <span v-for="n in 3" :key="n" class="bit" :style="{ animationDelay: `${(n - 1) * 160}ms` }" />
  </div>
  <div v-else class="vacio">
    <p class="label">{{ titulo }}</p>
    <p class="vacio__msg">
      <slot>{{ mensaje }}</slot>
    </p>
    <slot name="accion" />
  </div>
</template>

<style scoped>
.vacio {
  display: flex;
  flex-direction: column;
  gap: var(--nt-s-2, 8px);
  padding: var(--nt-s-5, 24px) 0;
}

.vacio__msg {
  font: 400 14px/1.5 var(--nt-font-sans);
  color: var(--nt-ink-3);
  max-width: 52ch;
}

.vacio__bits {
  display: flex;
  gap: 8px;
  padding: 48px 0;
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
</style>