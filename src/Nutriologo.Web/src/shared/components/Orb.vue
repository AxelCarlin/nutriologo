<template>
  <div
    class="orb-stage"
    :class="`orb-stage--${variante ?? 'hero'}`"
    role="status"
    :aria-label="`Asistente: ${stateLabel}`"
  >
    <div class="orb-ring orb-ring--1" aria-hidden="true"></div>
    <div class="orb-ring orb-ring--2" aria-hidden="true"></div>
    <div class="orb" :data-state="stateKey"></div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

// AgentState: estados técnicos del agente
type AgentState = 'idle' | 'listening' | 'thinking' | 'speaking'

// ConversationState: estados de la conversación desde el punto de vista del usuario
type ConversationState = 'empty' | 'active' | 'error' | 'awaiting-clarification'

// Export types for use in other modules
export type { AgentState, ConversationState }

// Props with defaults
const props = defineProps<{
  state?: AgentState | ConversationState
  // 'hero' = centro del estado vacío (200px). 'avatar' = 56px en conversación.
  variante?: 'hero' | 'avatar'
}>()

// Computed: determinar el label basándose en el tipo de state
const stateLabel = computed(() => {
  const s = props.state
  if (!s) return 'idle'
  // Si es un AgentState, mostrar nombre técnico
  if (s === 'idle' || s === 'listening' || s === 'thinking' || s === 'speaking') {
    return s
  }
  // Si es un ConversationState, mostrar label amigable
  return { empty: 'Sin conversación', active: 'Conversando', error: 'Error', 'awaiting-clarification': 'Esperando aclaración' }[s]
})

// Computed: key para el data-state del orb (siempre AgentState)
const stateKey = computed(() => {
  const s = props.state
  if (!s) return 'idle'
  // El Orb usa AgentState para las clases visuales
  if (s === 'idle' || s === 'listening' || s === 'thinking' || s === 'speaking') {
    return s
  }
  // Para ConversationState, mapear a estados visuales del orb
  return { empty: 'idle', active: 'speaking', error: 'idle', 'awaiting-clarification': 'thinking' }[s]
})
</script>

<!-- Estilos en styles/components/orb.css (malla de puntos monocroma). -->