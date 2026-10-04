<template>
  <section>
    <h2>Mediciones</h2>
    <form @submit.prevent="agregar">
      <label>Tipo (Id) <input v-model.number="form.idTipoMedicionCorporal" type="number" required /></label>
      <label>Valor <input v-model.number="form.valor" type="number" step="0.01" required /></label>
      <label>Fecha <input v-model="form.fechaMedicion" type="date" required /></label>
      <button :disabled="loading">Registrar</button>
    </form>
    <ul>
      <li v-for="m in items" :key="m.id">{{ m.fechaMedicion.slice(0,10) }} — tipo {{ m.idTipoMedicionCorporal }}: {{ m.valor }}</li>
    </ul>
    <p v-if="error" class="error">{{ error }}</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const items = ref<any[]>([]); const loading = ref(false); const error = ref('')
const form = ref({ idUsuario: 1, idTipoMedicionCorporal: 1, valor: 92, fechaMedicion: new Date().toISOString().slice(0,10), observacion: '' })

async function cargar() { try { items.value = await apiFetch('/mediciones?idUsuario=1') } catch (e: any) { error.value = e?.message ?? 'Error' } }
async function agregar() {
  loading.value = true
  try { await apiFetch('/mediciones', { method: 'POST', body: JSON.stringify(form.value) }); await cargar() }
  catch (e: any) { error.value = e?.message ?? 'Error' }
  finally { loading.value = false }
}
onMounted(cargar)
</script>
