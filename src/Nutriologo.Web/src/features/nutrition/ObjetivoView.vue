<template>
  <section class="form-screen">
    <h2>Objetivo nutricional</h2>
    <form class="form-screen" @submit.prevent="calcular">
      <div class="field"><label for="o-peso">Peso (kg)</label><input id="o-peso" v-model.number="form.pesoKg" type="number" step="0.1" required /></div>
      <div class="field"><label for="o-altura">Altura (cm)</label><input id="o-altura" v-model.number="form.alturaCm" type="number" required /></div>
      <div class="field"><label for="o-edad">Edad</label><input id="o-edad" v-model.number="form.edadAnios" type="number" required /></div>
      <div class="field"><label for="o-sexo">Sexo</label>
        <select id="o-sexo" v-model.number="form.sexo"><option :value="0">Masculino</option><option :value="1">Femenino</option></select>
      </div>
      <div class="field"><label for="o-factor">Factor actividad</label><input id="o-factor" v-model.number="form.factorActividad" type="number" step="0.001" required /></div>
      <div class="field"><label for="o-tipo">Tipo objetivo</label>
        <select id="o-tipo" v-model.number="form.tipoObjetivo"><option :value="0">Perder</option><option :value="1">Mantener</option><option :value="2">Ganar</option></select>
      </div>
      <div class="field"><label for="o-pesoobj">Peso objetivo (kg)</label><input id="o-pesoobj" v-model.number="form.pesoObjetivoKg" type="number" step="0.1" /></div>
      <div class="field"><label for="o-sem">Semanas</label><input id="o-sem" v-model.number="form.semanasObjetivo" type="number" /></div>
      <div class="form-actions"><button class="btn-primary" :disabled="loading">Calcular</button></div>
    </form>
    <p v-if="loading" class="msg-status">Calculando…</p>
    <p v-if="error" class="form-error">{{ error }}</p>
    <dl v-if="resultado" class="result-grid">
      <dt>BMR</dt><dd>{{ resultado.metabolismoBasal }}</dd>
      <dt>TDEE</dt><dd>{{ resultado.gastoEnergeticoTotal }}</dd>
      <dt>Meta calórica</dt><dd>{{ resultado.metaCaloricaEstimada }}</dd>
      <dt>Déficit diario</dt><dd>{{ resultado.deficitDiarioRequerido }}</dd>
      <dt>Ritmo semanal</dt><dd>{{ resultado.ritmoSemanalKg }} kg</dd>
    </dl>
    <h3 class="section-title">Evaluación de riesgo</h3>
    <dl v-if="resultado?.evaluacionObjetivo" class="result-grid">
      <dt>Objetivo</dt><dd>{{ resultado.evaluacionObjetivo.descripcion }} (nivel {{ resultado.evaluacionObjetivo.idNivelRiesgo }})</dd>
    </dl>
    <dl v-if="resultado?.evaluacionMetaCalorica" class="result-grid">
      <dt>Meta calórica</dt><dd>{{ resultado.evaluacionMetaCalorica.descripcion }} (nivel {{ resultado.evaluacionMetaCalorica.idNivelRiesgo }}) — déficit {{ resultado.evaluacionMetaCalorica.deficitCalculado }} kcal ({{ (resultado.evaluacionMetaCalorica.porcentajeDeficit*100).toFixed(1) }}%)</dd>
    </dl>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { apiFetch } from '../../shared/api/client'

const form = ref({ pesoKg: 92, alturaCm: 175, edadAnios: 35, sexo: 0, factorActividad: 1.55, tipoObjetivo: 0, pesoObjetivoKg: 82, semanasObjetivo: 22 })
const loading = ref(false); const error = ref(''); const resultado = ref<any>(null)

async function calcular() {
  loading.value = true; error.value = ''
  try { resultado.value = await apiFetch('/objetivo/calcular', { method: 'POST', body: JSON.stringify(form.value) }) }
  catch (e: any) { error.value = e?.message ?? 'Error'; resultado.value = null }
  finally { loading.value = false }
}
</script>
