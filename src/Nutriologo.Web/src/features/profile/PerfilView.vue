<template>
  <section class="form-screen">
    <h2>Perfil</h2>
    <form class="form-screen" @submit.prevent="guardar">
      <div class="field"><label for="p-fecha">Fecha de nacimiento</label><input id="p-fecha" v-model="form.fechaNacimiento" type="date" required /></div>
      <div class="field"><label for="p-altura">Altura (cm)</label><input id="p-altura" v-model.number="form.altura" type="number" required /></div>
      <div class="field"><label for="p-sexo">Sexo biológico (Id)</label><input id="p-sexo" v-model.number="form.idSexoBiologico" type="number" required /></div>
      <div class="field"><label for="p-act">Nivel de actividad (Id)</label><input id="p-act" v-model.number="form.idNivelActividadFisica" type="number" required /></div>
      <div class="form-actions"><button class="btn-primary" :disabled="loading">Guardar</button></div>
    </form>
    <p v-if="loading" class="msg-status">Guardando…</p>
    <p v-if="ok" class="form-ok">Perfil guardado (Id {{ ok }}).</p>
    <p v-if="error" class="form-error">{{ error }}</p>
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
