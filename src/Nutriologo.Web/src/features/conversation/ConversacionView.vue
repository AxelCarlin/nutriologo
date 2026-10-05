<template>
  <div class="chat-screen" :class="{ 'chat-screen--vacio': !hayMensajes }">
    <!-- Topbar: 48px, sin caja, solo hairline. Mismo eje 720px que el resto. -->
    <header class="topbar">
      <div class="topbar__inner">
        <!-- No repite el wordmark del sidebar: aqui va el ESTADO del agente. -->
        <p class="orb-state" :data-state="estadoVisual">
          <span class="orb-state__dot" aria-hidden="true" />
          <span>{{ ETIQUETA_ESTADO[estadoVisual] }}</span>
        </p>

        <div class="topbar__acciones">
          <!-- Indicador LLM: punto + modelo. Rojo solo si el servidor no responde. -->
          <span class="llm" :class="{ 'llm--down': llmCaido }" :title="llmCaido ? 'El modelo no responde' : 'Ollama en local'">
            <span class="llm__dot" aria-hidden="true" />
            <span class="llm__model">{{ llmCaido ? 'SIN CONEXIÓN' : 'LOCAL' }}</span>
          </span>
          <button class="btn-icon" type="button" title="Nueva conversación" aria-label="Nueva conversación" @click="nueva">
            <MessageSquarePlus :size="18" :stroke-width="1.5" />
          </button>
        </div>
      </div>
    </header>

    <!-- ============ ESTADO VACÍO: orbe centrado + guía ============ -->
    <template v-if="!hayMensajes">
      <div class="vacio">
        <div class="vacio__orbe">
          <Orb :state="orbState" variante="hero" />
          <p class="orb-state" :data-state="estadoVisual">
            <span class="orb-state__dot" aria-hidden="true" />
            <span>{{ ETIQUETA_ESTADO[estadoVisual] }}</span>
          </p>
        </div>
        <h1 class="vacio__titulo">¿qué comiste hoy?</h1>
        <div class="chips">
          <button
            v-for="s in sugerencias"
            :key="s.texto"
            type="button"
            class="chip"
            @click="usarSugerencia(s.texto)"
          >
            {{ s.texto }}
          </button>
        </div>
      </div>
    </template>

    <!-- ============ CONVERSACIÓN ============ -->
    <template v-else>
      <div class="conversa">
        <div class="conversa__avatar">
          <Orb :state="orbState" variante="avatar" />
        </div>
        <div class="chat-scroll">
          <div
            v-for="m in mensajes"
            :key="m.id"
            class="msg"
            :class="m.rol === 'user' ? 'msg--user' : 'msg--agent'"
          >
            <p v-if="m.rol !== 'user'" class="label msg__meta">NUTRIÓLOGO</p>
            <template v-if="m.rol === 'user'">{{ m.texto }}</template>
            <template v-else>
              {{ m.texto }}
              <span v-if="m.fallback" class="msg-fallback" title="Respuesta de respaldo (modelo no disponible)">
                <span aria-hidden="true">⚡</span>
              </span>
              <div class="msg-actions">
                <button type="button" @click="copiar(m.texto)">
                  <Copy :size="14" :stroke-width="1.5" /> Copiar
                </button>
              </div>
            </template>
          </div>
          <p v-if="isError" class="msg-status msg-status--error">{{ error }}</p>
          <!-- Sin recordatorio extra: la pregunta del asistente YA esta como
               mensaje. Antes se mostraba dos veces, y una era generica. -->
          <p v-if="isThinkingOrSpeaking" class="msg-status thinking-dots">Analizando</p>
          <p v-if="exito" class="msg-status msg-status--ok">{{ exito }}</p>
        </div>
      </div>
    </template>

    <!-- ========== COMPOSER: un solo pill, 720px ========== -->
    <form class="composer" @submit.prevent="enviar">
      <div class="composer-box">
        <RouterLink
          to="/registrar"
          class="composer-btn"
          title="Registro manual"
          aria-label="Registro manual"
        >
          <Plus :size="18" :stroke-width="1.5" />
        </RouterLink>
        <input
          v-model="texto"
          :disabled="enviando"
          placeholder="Escribe lo que comiste…"
          aria-label="Mensaje al asistente"
        />
        <button
          class="send-btn"
          type="submit"
          :disabled="enviando || !texto.trim()"
          aria-label="Enviar"
        >
          <ArrowUp :size="18" :stroke-width="2" />
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ArrowUp, Copy, MessageSquarePlus, Plus } from 'lucide-vue-next'
import { computed, ref, nextTick } from 'vue'
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
const isThinkingOrSpeaking = computed(() => orbState.value === 'thinking' || orbState.value === 'speaking')

const hayMensajes = computed(() => mensajes.value.length > 0)
const llmCaido = computed(() => conversationState.value === 'error')

const ETIQUETA_ESTADO: Record<string, string> = {
  idle: 'LISTO',
  listening: 'ESCUCHANDO',
  thinking: 'PENSANDO',
  speaking: 'RESPONDIENDO',
  error: 'SIN RESPUESTA',
}

const estadoVisual = computed(() =>
  conversationState.value === 'error' ? 'error' : orbState.value,
)

// Sugerencias del estado vacío: cubren las tres intenciones reales del LLM.
const sugerencias = [
  { texto: 'Registrar desayuno', prompt: 'Comí un desayuno de huevos con tortillas' },
  { texto: '¿Cuántas calorías llevo?', prompt: '¿Cuántas calorías llevo hoy?' },
  { texto: 'Qué cenar hoy', prompt: '¿Qué puedo cenar hoy?' },
]

function usarSugerencia(prompt: string) {
  texto.value = prompt
}

// Mensajes del chat - límite máximo para mantener performance
const MAX_HISTORIAL = 100

// Mensajes del chat
const mensajes = ref<{ id: number; rol: string; texto: string; fallback?: boolean }[]>([])
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

// Función para resumir historial cuando se acerca al límite
function resumirContexto(historialActual: Array<{ rol: string; texto: string }>): Array<{ rol: string; texto: string }> {
  // Mantener solo los mensajes más relevantes (primeros y últimos)
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

    // 6. Manejar respuesta del LLM.
    // El contrato real del backend es: registrado | respuesta | aclaracion.
    // Se comparaba contra 'pregunta', que el backend nunca emite: toda
    // aclaracion caia en el else y se pintaba "Respuesta inesperada".
    if (r.tipo === 'registrado') {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: 'Comida registrada correctamente.' })
      exito.value = `Comida #${r.id} registrada.`
      conversationState.value = 'active'
      orbState.value = 'speaking'
    } else if (r.tipo === 'respuesta') {
      // Indicador sutil de fallback (solo debug): r.fallback === true
      const txt = r.texto ?? ''
      const esFallback = r.fallback === true
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: txt, fallback: esFallback })
      conversationState.value = 'active'
      orbState.value = 'speaking'
    } else if (r.tipo === 'aclaracion') {
      // El LLM necesita mas informacion. NO es un error: es una ronda mas
      // del dialogo, asi que el estado es 'awaiting-clarification'.
      mensajes.value.push({
        id: nextId++,
        rol: 'assistant',
        texto: r.pregunta ?? needsClarificationMessage,
      })
      conversationState.value = 'awaiting-clarification'
      orbState.value = 'idle'
    } else {
      // Solo llega aqui si el backend agrega un tipo nuevo: se muestra el
      // contrato crudo en vez de una frase generica que oculta el problema.
      mensajes.value.push({
        id: nextId++,
        rol: 'assistant',
        texto: `Respuesta con formato no reconocido (tipo: ${String(r?.tipo ?? 'sin tipo')}).`,
      })
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
    const scroller = document.querySelector('.chat-scroll')
    scroller?.scrollTo({ top: scroller.scrollHeight })
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

<!-- Estilos en styles/components/chat.css + composer.css (tokens Nothing). -->