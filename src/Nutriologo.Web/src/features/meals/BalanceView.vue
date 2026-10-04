<template>
  <section class="form-screen">
    <h2>Balance</h2>
    <form class="form-screen" @submit.prevent="cargar">
      <div class="field">
        <label for="b-fecha">Fecha</label>
        <input id="b-fecha" v-model="fecha" type="date" required />
      </div>
      <div class="form-actions"><button class="btn-primary">Consultar</button></div>
    </form>
    <dl v-if="balance !== null" class="result-grid">
      <dt>Consumidas</dt><dd>{{ balance }} kcal</dd>
    </dl>
    <p v-if="error" class="form-error">{{ error }}</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const fecha = ref(new Date().toISOString().slice(0, 10))
const balance = ref<number | null>(null)
const error = ref('')

async function cargar() {
  error.value = ''
  try {
    const b = await apiFetch<{ caloriasConsumidas: number }>(`/balance?fecha=${fecha.value}`)
    balance.value = b.caloriasConsumidas
  } catch (e: any) {
    error.value = e?.message ?? 'Error'
  }
}
onMounted(cargar)
</script>
