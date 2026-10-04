<template>
  <section class="form-screen">
    <h2>Registrar comida</h2>
    <form class="form-screen" @submit.prevent="registrar">
      <div class="field"><label for="r-nombre">Nombre</label><input id="r-nombre" v-model="form.nombre" /></div>
      <div v-for="(it, i) in form.items" :key="i" class="item-row">
        <select v-model="it.nombreAlimento" :aria-label="`Alimento ${i + 1}`">
          <option v-for="a in alimentos" :key="a.id" :value="a.nombre">{{ a.nombre }}</option>
        </select>
        <input v-model.number="it.cantidad" type="number" step="0.1" :aria-label="`Cantidad ${i + 1}`" />
        <input v-model="it.unidad" placeholder="G" :aria-label="`Unidad ${i + 1}`" />
        <button class="btn-ghost" type="button" @click="form.items.splice(i,1)" aria-label="Quitar alimento">✕</button>
      </div>
      <div class="form-actions">
        <button class="btn-ghost" type="button" @click="form.items.push({ nombreAlimento: alimentos[0]?.nombre ?? '', cantidad: 100, unidad: 'G' })">+ alimento</button>
        <button class="btn-primary" :disabled="loading">Registrar</button>
      </div>
    </form>
    <p v-if="loading" class="msg-status">Registrando…</p>
    <p v-if="ok" class="form-ok">Comida registrada (Id {{ ok }}).</p>
    <p v-if="error" class="form-error">{{ error }}</p>

    <h3 class="section-title">Balance del día</h3>
    <dl class="result-grid">
      <dt>Consumidas</dt><dd>{{ balance ?? '—' }} kcal</dd>
    </dl>
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
