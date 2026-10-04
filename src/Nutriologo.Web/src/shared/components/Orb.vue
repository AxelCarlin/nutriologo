<template>
  <div class="orb-stage" role="status" :aria-label="`Asistente: ${stateLabel}`"></div>
  <div class="orb-ring orb-ring--1" aria-hidden="true"></div>
  <div class="orb-ring orb-ring--2" aria-hidden="true"></div>
  <div class="orb" :data-state="stateKey"></div>
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

<style>
.orb-stage {
  width: 48px;
  height: 48px;
  position: relative;
}

.orb-ring {
  position: absolute;
  width: 100%;
  height: 100%;
  border: 2px solid var(--orb-ring-color, var(--border-default));
  border-radius: 50%;
  top: 0;
  left: 0;
}

.orb-ring--1 {
  animation: orb-spin 2s linear infinite;
}

.orb-ring--2 {
  animation: orb-spin 3s linear infinite reverse;
  animation-delay: 1s;
}

.orb {
  position: absolute;
  width: 24px;
  height: 24px;
  background: var(--orb-color, var(--surface-glass));
  border: 2px solid var(--border-default);
  border-radius: 50%;
  top: 12px;
  left: 12px;
  transition: background 0.2s;
}

@keyframes orb-spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>