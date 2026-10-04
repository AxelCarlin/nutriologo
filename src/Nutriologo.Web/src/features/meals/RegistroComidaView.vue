<template>
  <section>
    <h2>Registrar comida</h2>
    <form @submit.prevent="registrar">
      <label>Nombre <input v-model="form.nombre" /></label>
      <div v-for="(it, i) in form.items" :key="i" style="display:flex;gap:4px">
        <select v-model="it.nombreAlimento">
          <option v-for="a in alimentos" :key="a.id" :value="a.nombre">{{ a.nombre }}</option>
        </select>
        <input v-model.number="it.cantidad" type="number" step="0.1" />
        <input v-model="it.unidad" style="width:60px" placeholder="G" />
        <button type="button" @click="form.items.splice(i,1)">✕</button>
      </div>
      <button type="button" @click="form.items.push({ nombreAlimento: alimentos[0]?.nombre ?? '', cantidad: 100, unidad: 'G' })">+ alimento</button>
      <button :disabled="loading">Registrar</button>
    </form>
    <p v-if="loading">Registrando…</p>
    <p v-if="ok">Comida registrada (Id {{ ok }}).</p>
    <p v-if="error" class="error">{{ error }}</p>

    <h3>Balance del día</h3>
    <p>{{ balance }} kcal consumidas</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const alimentos = ref<any[]>([])
const form = ref({ idUsuario: 1, fechaConsumo: new Date().toISOString(), nombre: '', items: [{ nombreAlimento: '', cantidad: 100, unidad: 'G' }] })
const loading = ref(false); const error = ref(''); const ok = ref<number | null>(null); const balance = ref<number | null>(null)

onMounted(async () => { alimentos.value = await apiFetch('/alimentos'); form.value.items[0]!.nombreAlimento = alimentos.value[0]?.nombre ?? '' })

async function registrar() {
  loading.value = true; error.value = ''
  try {
    const r = await apiFetch<{ id: number }>('/comidas', { method: 'POST', body: JSON.stringify(form.value) })
    ok.value = r.id
    const b = await apiFetch<{ caloriasConsumidas: number }>(`/balance?idUsuario=1&fecha=${new Date().toISOString().slice(0,10)}`)
    balance.value = b.caloriasConsumidas
  } catch (e: any) { error.value = e?.message ?? 'Error' }
  finally { loading.value = false }
}
</script>
