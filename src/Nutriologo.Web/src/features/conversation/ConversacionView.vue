<template>
  <section>
    <h2>Chat</h2>
    <ul>
      <li v-for="m in mensajes" :key="m.id" :class="m.rol">
        <strong>{{ m.rol === 'user' ? 'Tú' : 'Asistente' }}:</strong> {{ m.texto }}
      </li>
    </ul>
    <form @submit.prevent="enviar">
      <input v-model="texto" placeholder="Ej: Comí dos huevos y dos tortillas" />
      <button :disabled="enviando">Enviar</button>
    </form>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="exito" class="ok">✅ {{ exito }}</p>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const mensajes = ref<{ id: number; rol: string; texto: string }[]>([])
const texto = ref(''); const enviando = ref(false); const error = ref(''); const exito = ref('')
let nextId = 1

async function enviar() {
  if (!texto.value.trim()) return
  mensajes.value.push({ id: nextId++, rol: 'user', texto: texto.value })
  enviando.value = true; error.value = ''; exito.value = ''
  try {
    const r = await apiFetch<any>('/conversacion/mensaje', {
      method: 'POST',
      body: JSON.stringify({ idUsuario: 1, texto: texto.value }),
    })
    if (r.tipo === 'registrado') {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: 'Comida registrada correctamente.' })
      exito.value = `Comida #${r.id} registrada.`
    } else {
      mensajes.value.push({ id: nextId++, rol: 'assistant', texto: r.pregunta ?? 'Necesito más información.' })
    }
  } catch (e: any) { error.value = e?.message ?? 'Error' }
  finally { enviando.value = false; texto.value = '' }
}
</script>

<style scoped>
.user { color: #555 } .assistant { color: #2456a6 } .error { color: #c00 } .ok { color: #2a7 }
</style>
