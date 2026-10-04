<template>
  <section class="form-screen">
    <h2>Mediciones</h2>
    <form class="form-screen" @submit.prevent="agregar">
      <div class="field"><label for="m-tipo">Tipo (Id)</label><input id="m-tipo" v-model.number="form.idTipoMedicionCorporal" type="number" required /></div>
      <div class="field"><label for="m-valor">Valor</label><input id="m-valor" v-model.number="form.valor" type="number" step="0.01" required /></div>
      <div class="field"><label for="m-fecha">Fecha</label><input id="m-fecha" v-model="form.fechaMedicion" type="date" required /></div>
      <div class="form-actions"><button class="btn-primary" :disabled="loading">Registrar</button></div>
    </form>
    <ul class="data-list">
      <li v-for="m in items" :key="m.id">{{ m.fechaMedicion.slice(0,10) }} — tipo {{ m.idTipoMedicionCorporal }}: {{ m.valor }}</li>
    </ul>
    <p v-if="error" class="form-error">{{ error }}</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const items = ref<any[]>([]); const loading = ref(false); const error = ref('')
const form = ref({ idTipoMedicionCorporal: 1, valor: 92, fechaMedicion: new Date().toISOString().slice(0,10), observacion: '' })

async function cargar() { try { items.value = await apiFetch('/mediciones') } catch (e: any) { error.value = e?.message ?? 'Error' } }
async function agregar() {
  loading.value = true
  try { await apiFetch('/mediciones', { method: 'POST', body: JSON.stringify(form.value) }); await cargar() }
  catch (e: any) { error.value = e?.message ?? 'Error' }
  finally { loading.value = false }
}
onMounted(cargar)
</script>
