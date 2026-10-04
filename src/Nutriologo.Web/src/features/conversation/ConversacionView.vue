<template>
  <div class="chat-screen">
    <header class="chat-header">
      <div class="chip-model">
        <span>Nutriólogo</span>
        <span aria-hidden="true">⌃</span>
      </div>
      <div class="header-actions">
        <button class="btn-icon" type="button" title="Nueva conversación" aria-label="Nueva conversación" @click="nueva">
          <MessageSquarePlus :size="20" :stroke-width="1.5" />
        </button>
      </div>
    </header>

    <Orb :state="orbState" />

    <div class="chat-scroll">
      <div
        v-for="m in mensajes"
        :key="m.id"
        :class="m.rol === 'user' ? 'msg-user' : 'msg-agent'"
      >
        <template v-if="m.rol === 'user'">{{ m.texto }}</template>
        <template v-else>
          {{ m.texto }}
          <div class="msg-actions">
            <button type="button" @click="copiar(m.texto)">
              <Copy :size="14" :stroke-width="1.5" /> Copiar
            </button>
          </div>
        </template>
      </div>
      <p v-if="isError" class="msg-status msg-status--error">{{ error }}</p>
      <p v-if="isAwaitingClarification" class="msg-status msg-status--warning">
        {{ needsClarificationMessage }}
      </p>
      <p v-if="isThinkingOrSpeaking" class="msg-status thinking-dots">Analizando</p>
      <p v-if="exito" class="msg-status msg-status--ok">{{ exito }}</p>
    </div>

    <form class="composer" @submit.prevent="enviar">
      <div class="composer-box">
        <input
          v-model="texto"
          :disabled="enviando"
          placeholder="Ej: Comí dos huevos y dos tortillas"
          aria-label="Mensaje al asistente"
        />
        <div class="composer-tools">
          <RouterLink to="/registrar" title="Registro manual" aria-label="Registro manual">
            <Plus :size="16" :stroke-width="1.5" />
          </RouterLink>
        </div>
      </div>
      <button class="send-btn" type="submit" :disabled="enviando || !texto.trim()" aria-label="Enviar">
        <ArrowUp :size="20" :stroke-width="2" />
      </button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ArrowUp, Copy, MessageSquarePlus, Plus } from 'lucide-vue-next'
import { ref, computed, nextTick } from 'vue'
import { apiFetch } from '../../shared/api/client'
import Orb from '../../shared/components/Orb.vue'

// Tipos de estado
type AgentState = 'idle' | 'listening' | 'thinking' | 'speaking'
type ConversationState = 'empty' | 'active' | 'error' | 'awaiting-clarification'

// Estado de la conversación usando ref de Vue
const conversationState = ref<ConversationState>('empty')

// AgentState para el Orb (estados técnicos)
const orbState = ref<AgentState>('idle')

// Helper computeds for template access
const isError = computed(() => conversationState.value === 'error')
const isAwaitingClarification = computed(() => conversationState.value === 'awaiting-clarification')
const isThinkingOrSpeaking = computed(() => orbState.value === 'thinking' || orbState.value === 'speaking')

// Mensajes del chat - límite máximo para mantener performance
const MAX_HISTORIAL = 100

// Mensajes del chat
const mensajes = ref<{ id: number; rol: string; texto: string }[]>([])
const texto = ref('')
const enviando = ref(false)
const error = ref('')
const exito = ref('')
let nextId = 1

// Plantilla de aclaración cuando el LLM necesita más info
const needsClarificationMessage =
  'Necesito más información sobre tu comida para darte una recomendación adecuada.'

// Configuración de contexto LLM
const MAX_MENSAJES_HISTORIAL = 6 // Siempre enviamos últimos 6 al LLM

function copiar(t: string) {
  navigator.clipboard?.writeText(t).catch(() => {})
}

function nueva() {
  mensajes.value = []
  error.value = ''
  exito.value = ''
  conversationState.value = 'empty'
  orbState.value = 'idle'
}

// Función para resumir historial cuando se acerca el límite
function resumirContexto(historialActual: Array<{ rol: string; texto: string }>): Array<{ rol: string; texto: string }> {
  if (historialActual.length <= MAX_MENSAJES_HISTORIAL) {
    return historialActual
  }

  // Tomar solo los mensajes más relevantes (primeros y últimos)
  const primeros = historialActual.slice(0, 2)
  const ultimos = historialActual.slice(-2)

  // Construir un resumen de temas únicos
  const temasSet = new Set<string>()
  for (const m of historialActual) {
    const textoLower = (m.texto || '').toLowerCase()
    // Detectar temas nutricionales comunes
    const matches = textoLower.match(
      /(huevos?|tortilla|pollo|carne|pescado|arroz|frijoles|ensalada|fruta|verdura)/gi
    )
    if (matches) {
      matches.forEach((m: string) => temasSet.add(m.toLowerCase()))
    }
  }

  // Construir resumen compacto
  const temasArray = Array.from(temasSet).slice(0, 3)
  const resumenTexto =
    temasArray.length > 0
      ? `Conversación previa sobre: ${temasArray.join(', ')}. `
      : 'Conversación previa sobre alimentos.'

  // Retornar estructura: primeros + resumen + últimos
  return [
    ...primeros,
    { rol: 'system', texto: resumenTexto },
    ...ultimos,
  ]
}

// Función para normalizar historial antes de enviar
function normalizarHistorial(): Array<{ rol: string; texto: string }> {
  // Mantener solo los últimos MAX_MENSAJES_HISTORIAL mensajes
  const historialReciente = mensajes.value.slice(-MAX_MENSAJES_HISTORIAL)

  // Verificar si necesita resumen
  if (historialReciente.length <= MAX_MENSAJES_HISTORIAL) {
    return historialReciente.map((m: { rol: string; texto: string }) => ({
      rol: m.rol,
      texto: m.texto || '',
    }))
  }

  // Aplicar resumen
  return resumirContexto(historialReciente)
}

async function enviar() {
  if (!texto.value.trim() || enviando.value) return

  // 1. Mostrar mensaje de usuario
  mensajes.value.push({ id: nextId++, rol: 'user', texto: texto.value })

  // 2. Mantener historial dentro de límites
  if (mensajes.value.length > MAX_HISTORIAL) {
    mensajes.value = mensajes.value.slice(-MAX_HISTORIAL)
  }

  // 3. Normalizar y preparar historial para el LLM
  const historial = normalizarHistorial()

  // 4. Actualizar estados
  conversationState.value = 'active'
  orbState.value = 'thinking'
  error.value = ''

  // 5. Enviar al LLM
  try {
    const r = await apiFetch<any>('/conversacion/mensaje', {
      method: 'POST',
      body: JSON.stringify({ texto: texto.value, historial }),
    })

    // 6. Manejar respuesta del LLM
    if (r.tipo === 'registrado') {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: 'Comida registrada correctamente.' })
      exito.value = `Comida #${r.id} registrada.`
      conversationState.value = 'active'
      orbState.value = 'speaking'
    } else if (r.tipo === 'respuesta') {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: r.texto ?? '' })
      conversationState.value = 'active'
      orbState.value = 'speaking'
    } else if (r.tipo === 'pregunta') {
      // El LLM necesita más información
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: r.pregunta ?? needsClarificationMessage })
      conversationState.value = 'awaiting-clarification'
      orbState.value = 'idle'
    } else {
      // Respuesta desconocida
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: 'Respuesta inesperada del asistente.' })
      conversationState.value = 'error'
      orbState.value = 'idle'
    }
  } catch (e: any) {
    // Error 503 del backend (modelo calentando/ocioso)
    if (e?.status === 503) {
      error.value = 'El modelo está ocupado. Espera unos segundos e intentalo de nuevo.'
      conversationState.value = 'error'
    } else {
      error.value = e?.message ?? 'Error inesperado al comunicarse con el asistente'
      conversationState.value = 'error'
    }
  } finally {
    enviando.value = false
    texto.value = ''
    await nextTick()
    window.scrollTo({ top: document.body.scrollHeight })
    // Resetear a idle después de un breve retraso
    setTimeout(() => {
      if (conversationState.value !== 'awaiting-clarification') {
        orbState.value = 'idle'
      }
      if (exito.value && !/Comida #/.test(exito.value)) {
        // Auto-ocultar éxito después de un tiempo
        setTimeout(() => { exito.value = '' }, 5000)
      }
    }, 1200)
  }
}
</script>

<style>
/* Chat screen styles would go here, inheriting Aurenix tokens */
</style>
