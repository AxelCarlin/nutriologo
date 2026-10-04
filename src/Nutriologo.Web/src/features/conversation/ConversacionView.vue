<template>
  <div class="chat-screen">
    <header class="chat-header">
      <div class="chip-model">
        <span>Nutriólogo</span>
        <span aria-hidden="true">⌄</span>
      </div>
    </header>

    <Orb :state="orbState" />

    <div ref="scrollEl" class="chat-scroll">
      <div
        v-for="m in mensajes"
        :key="m.id"
        :class="m.rol === 'user' ? 'msg-user' : 'msg-agent'"
      >
        <template v-if="m.rol === 'user'">{{ m.texto }}</template>
        <template v-else>
          {{ m.texto }}
          <div class="msg-actions">
            <button type="button" @click="copiar(m.texto)">Copiar</button>
          </div>
        </template>
      </div>
      <p v-if="enviando" class="msg-status thinking-dots">Analizando</p>
      <p v-if="error" class="msg-status msg-status--error">{{ error }}</p>
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
        <span class="composer-hint">El asistente registra o pide aclaración; nunca inventa datos.</span>
      </div>
      <button class="send-btn" type="submit" :disabled="enviando || !texto.trim()" aria-label="Enviar">
        ↑
      </button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { nextTick, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'
import Orb, { type OrbState } from '../../shared/components/Orb.vue'

const mensajes = ref<{ id: number; rol: string; texto: string }[]>([])
const texto = ref('')
const enviando = ref(false)
const error = ref('')
const exito = ref('')
const orbState = ref<OrbState>('idle')
const scrollEl = ref<HTMLElement | null>(null)
let nextId = 1

function copiar(t: string) {
  navigator.clipboard?.writeText(t).catch(() => {})
}

async function enviar() {
  if (!texto.value.trim() || enviando.value) return
  mensajes.value.push({ id: nextId++, rol: 'user', texto: texto.value })
  enviando.value = true
  orbState.value = 'thinking'
  error.value = ''
  exito.value = ''
  try {
    const r = await apiFetch<any>('/conversacion/mensaje', {
      method: 'POST',
      body: JSON.stringify({ idUsuario: 1, texto: texto.value }),
    })
    orbState.value = 'speaking'
    if (r.tipo === 'registrado') {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: 'Comida registrada correctamente.' })
      exito.value = `Comida #${r.id} registrada.`
    } else {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: r.pregunta ?? 'Necesito más información.' })
    }
  } catch (e: any) {
    error.value = e?.message ?? 'Error'
  } finally {
    enviando.value = false
    texto.value = ''
    await nextTick()
    scrollEl.value?.scrollTo({ top: scrollEl.value.scrollHeight })
    setTimeout(() => { orbState.value = 'idle' }, 1200)
  }
}
</script>
