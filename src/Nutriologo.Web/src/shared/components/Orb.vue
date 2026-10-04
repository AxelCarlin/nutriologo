<template>
  <div class="orb-stage" role="status" :aria-label="`Asistente: ${state}`">
    <div class="orb-ring orb-ring--1" aria-hidden="true"></div>
    <div class="orb-ring orb-ring--2" aria-hidden="true"></div>
    <div class="orb" :data-state="state" aria-hidden="true"></div>
  </div>
</template>

<script setup lang="ts">
export type OrbState = 'idle' | 'listening' | 'thinking' | 'speaking'

withDefaults(defineProps<{ state?: OrbState }>(), { state: 'idle' })
</script>
