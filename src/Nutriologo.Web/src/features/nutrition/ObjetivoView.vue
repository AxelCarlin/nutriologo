<template>
  <section>
    <h2>Objetivo nutricional</h2>
    <form @submit.prevent="calcular">
      <label>Peso (kg) <input v-model.number="form.pesoKg" type="number" step="0.1" required /></label>
      <label>Altura (cm) <input v-model.number="form.alturaCm" type="number" required /></label>
      <label>Edad <input v-model.number="form.edadAnios" type="number" required /></label>
      <label>Sexo
        <select v-model.number="form.sexo"><option :value="0">Masculino</option><option :value="1">Femenino</option></select>
      </label>
      <label>Factor actividad <input v-model.number="form.factorActividad" type="number" step="0.001" required /></label>
      <label>Tipo objetivo
        <select v-model.number="form.tipoObjetivo"><option :value="0">Perder</option><option :value="1">Mantener</option><option :value="2">Ganar</option></select>
      </label>
      <label>Peso objetivo (kg) <input v-model.number="form.pesoObjetivoKg" type="number" step="0.1" /></label>
      <label>Semanas <input v-model.number="form.semanasObjetivo" type="number" /></label>
      <button :disabled="loading">Calcular</button>
    </form>
    <p v-if="loading">Calculando…</p>
    <p v-if="error" class="error">{{ error }}</p>
    <dl v-if="resultado">
      <dt>BMR</dt><dd>{{ resultado.metabolismoBasal }}</dd>
      <dt>TDEE</dt><dd>{{ resultado.gastoEnergeticoTotal }}</dd>
      <dt>Meta calórica</dt><dd>{{ resultado.metaCaloricaEstimada }}</dd>
      <dt>Déficit diario</dt><dd>{{ resultado.deficitDiarioRequerido }}</dd>
      <dt>Ritmo semanal</dt><dd>{{ resultado.ritmoSemanalKg }} kg</dd>
    </dl>
    <h3>Evaluación de riesgo</h3>
    <dl v-if="resultado?.evaluacionObjetivo">
      <dt>Objetivo</dt><dd>{{ resultado.evaluacionObjetivo.descripcion }} (nivel {{ resultado.evaluacionObjetivo.idNivelRiesgo }})</dd>
    </dl>
    <dl v-if="resultado?.evaluacionMetaCalorica">
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
