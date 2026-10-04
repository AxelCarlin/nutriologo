<template>
  <div class="chat-screen auth-screen">
    <header class="chat-header">
      <div class="chip-model"><span>Nutriólogo</span></div>
    </header>
    <div class="glass-card auth-card">
      <div class="auth-tabs">
        <button type="button" :class="{ active: modo === 'login' }" @click="modo = 'login'">Entrar</button>
        <button type="button" :class="{ active: modo === 'registro' }" @click="modo = 'registro'">Crear cuenta</button>
      </div>
      <form class="form-screen" @submit.prevent="submit">
        <div v-if="modo === 'registro'" class="field">
          <label for="a-nombre">Nombre</label>
          <input id="a-nombre" v-model="nombre" autocomplete="name" required />
        </div>
        <div class="field">
          <label for="a-correo">Correo</label>
          <input id="a-correo" v-model="correo" type="email" autocomplete="email" required />
        </div>
        <div class="field">
          <label for="a-pass">Contraseña (mínimo 8 caracteres)</label>
          <input id="a-pass" v-model="password" type="password" autocomplete="current-password" minlength="8" required />
        </div>
        <div class="form-actions">
          <button class="btn-primary btn-primary--block" :disabled="loading">
            {{ modo === 'login' ? 'Entrar' : 'Crear cuenta' }}
          </button>
        </div>
      </form>
      <p v-if="loading" class="msg-status">Verificando…</p>
      <p v-if="error" class="form-error">{{ error }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { login, registro } from '../../shared/api/auth'

const router = useRouter()
const modo = ref<'login' | 'registro'>('login')
const nombre = ref('')
const correo = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function submit() {
  loading.value = true
  error.value = ''
  try {
    if (modo.value === 'login') await login(correo.value, password.value)
    else await registro(nombre.value, correo.value, password.value)
    router.replace('/chat')
  } catch (e: any) {
    error.value =
      e?.status === 401 ? 'Correo o contraseña incorrectos.' : (e?.message ?? 'Error de conexión')
  } finally {
    loading.value = false
  }
}
</script>
