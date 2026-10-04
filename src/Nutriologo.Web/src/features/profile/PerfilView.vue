<template>
  <section>
    <h2>Perfil</h2>
    <form @submit.prevent="guardar">
      <label>Fecha de nacimiento <input v-model="form.fechaNacimiento" type="date" required /></label>
      <label>Altura (cm) <input v-model.number="form.altura" type="number" required /></label>
      <label>Sexo biológico (Id) <input v-model.number="form.idSexoBiologico" type="number" required /></label>
      <label>Nivel de actividad (Id) <input v-model.number="form.idNivelActividadFisica" type="number" required /></label>
      <button :disabled="loading">Guardar</button>
    </form>
    <p v-if="loading">Guardando…</p>
    <p v-if="ok">Perfil guardado (Id {{ ok }}).</p>
    <p v-if="error" class="error">{{ error }}</p>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const form = ref({ idUsuario: 1, idSexoBiologico: 1, idNivelActividadFisica: 3, fechaNacimiento: '1991-05-10', altura: 175 })
const loading = ref(false); const error = ref(''); const ok = ref<number | null>(null)

async function guardar() {
  loading.value = true; error.value = ''; ok.value = null
  try {
    const r = await apiFetch<{ id: number }>('/perfil', { method: 'POST', body: JSON.stringify(form.value) })
    ok.value = r.id
  } catch (e: any) { error.value = e?.message ?? 'Error' }
  finally { loading.value = false }
}
</script>
