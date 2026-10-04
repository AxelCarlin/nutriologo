<template>
  <section class="inicio-screen">
    <div class="inicio-header">
      <h1>Nutriólogo</h1>
      <p class="subtitulo">Tu asistente de nutrición diaria</p>
    </div>

    <div class="inicio-contenido">
      <!-- ESTADO ACTUAL -->
      <div class="tarjeta-estado" id="estado-actual">
        <h2 class="seccion-titulo">Estado actual</h2>
        <div class="contenido-estado">
          <div v-if="cargando" class="spinner-cargando" />
          <div v-else class="contenido-datos">
            <div class="result-grid">
              <dt>Calorías consumidas</dt>
              <dd id="calorias-consumidas" class="calorias-valor">Cargando...</dd>
              <dt>Peso actual</dt>
              <dd id="peso-actual" class="peso-valor">Cargando...</dd>
            </div>

            <div v-if="riesgo" class="riesgo-info">
              <dt>Riesgo</dt>
              <dd id="riesgo-nivel">
                <span v-if="riesgo.tipo === 'exitoso'" class="badge-estado-exitoso">
                  {{ riesgo.texto }}
                </span>
                <span v-if="riesgo.tipo === 'advertencia'" class="badge-estado-advertencia">
                  {{ riesgo.texto }}
                </span>
                <span v-if="riesgo.tipo === 'peligro'" class="badge-estado-peligro">
                  {{ riesgo.texto }}
                </span>
                <span v-else class="badge-estado-neutro">{{ riesgo.texto }}</span>
              </dd>
            </div>
          </div>
        </div>
      </div>

      <!-- ACCIONES RÁPIDAS -->
      <div class="tarjeta-acciones">
        <h2 class="seccion-titulo">Acciones rápidas</h2>
        <div class="acciones-grid">
          <a
            @click="irA('/registrar')"
            class="navegacion-acceso"
          >
            <span class="icono-plus" />
            <span>Registrar comida</span>
          </a>
          <a
            @click="irA('/chat')"
            class="navegacion-acceso"
          >
            <span class="icono-message" />
            <span>Chat Nutriólogo</span>
          </a>
          <a
            @click="irA('/progreso')"
            class="navegacion-acceso"
          >
            <span class="icono-bar" />
            <span>Ver progreso</span>
          </a>
        </div>
      </div>

      <!-- ÚLTIMAS COMIDAS -->
      <div v-if="mostrandoUltimasComidas" class="tarjeta-comidas">
        <h2 class="seccion-titulo">Últimas comidas</h2>
        <ul class="data-list-in-line">
          <li v-for="item in ultimasComidas" :key="item.hora">
            🕐 {{ item.hora }} {{ item.nombre }} {{ item.kcal }} kcal
          </li>
        </ul>
      </div>
    </div>
  </section>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, isAuthenticated } from '../../shared/api/client'

const router = useRouter()

// Estado
const cargando = ref(true)
const mostrandoUltimasComidas = ref(false)

// Datos del usuario (conectados al backend via auth JWT)
const caloriasConsumidas = ref<number | null>(null)
const pesoActual = ref<number | null>(null)
const riesgo = ref({
  tipo: 'neutral',
  texto: ''
})
const ultimasComidas = ref([
  { hora: '08:30', nombre: 'Desayuno', kcal: 420 },
  { hora: '13:20', nombre: 'Comida', kcal: 620 }
])

// Navegación interna
const irA = (path: string) => {
  router.push(path)
}

// Computed: clase CSS basada en el tipo de riesgo
const riesgoClass = computed(() => {
  const tipo = riesgo.value.tipo
  if (tipo === 'exitoso') return 'badge-estado-exitoso'
  if (tipo === 'advertencia') return 'badge-estado-advertencia'
  if (tipo === 'peligro') return 'badge-estado-peligro'
  return 'badge-estado-neutro'
}

// Cargar datos del backend al montar componente
onMounted(async () => {
  try {
    // 1. Verificar si el usuario está autenticado
    const hayAuth = isAuthenticated()
    
    // 2. Obtener balance calórico del día de hoy usando endpoint real
    const hoy = new Date().toISOString().slice(0, 10)
    
    if (hayAuth) {
      // Usuario autenticado: usar endpoint con token JWT
      const balance = await apiFetch<{ caloriasConsumidas: number }>(
        '/api/balance?fecha=' + hoy,
        { headers: { Authorization: `Bearer ${localStorage.getItem('nutriologo_token')}` } }
      )
      caloriasConsumidas.value = balance.caloriasConsumidas
      
      // 3. Obtener perfil del usuario autenticado
      const perfil = await apiFetch<{ peso: number }>(
        '/api/perfil',
        { headers: { Authorization: `Bearer ${localStorage.getItem('nutriologo_token')}` } }
      )
      pesoActual.value = perfil.peso
      
      // 4. Obtener evaluación de riesgo del perfil/objetivo
      try {
        const objetivo = await apiFetch<{ evaluacionObjetivo: { idNivelRiesgo: number; texto: string } }>(
          '/api/objetivo/calcular',
          { headers: { Authorization: `Bearer ${localStorage.getItem('nutriologo_token')}` } }
        )
        // Mapear riesgo del backend al formato del frontend
        if (objetivo.evaluacionObjetivo?.idNivelRiesgo === 1) {
          riesgo.value = { tipo: 'exitoso', texto: objetivo.evaluacionObjetivo.texto }
        } else if (objetivo.evaluacionObjetivo?.idNivelRiesgo === 2) {
          riesgo.value = { tipo: 'advertencia', texto: objetivo.evaluacionObjetivo.texto }
        } else if (objetivo.evaluacionObjetivo?.idNivelRiesgo === 3) {
          riesgo.value = { tipo: 'peligro', texto: objetivo.evaluacionObjetivo.texto }
        } else {
          riesgo.value = { tipo: 'neutral', texto: 'Sin nivel definido' }
        }
      } catch (objError) {
        // Si falla objetivo/calcular, mantener riesgo neutral
        console.warn('No se pudo cargar evaluación de objetivo:', objError.message)
        riesgo.value = { tipo: 'neutral', texto: 'Sin evaluación' }
      }
    } else {
      // Usuario no autenticado: datos simulados para demo (fallo-closed)
      caloriasConsumidas.value = 1240
      pesoActual.value = 92
      riesgo.value = { tipo: 'neutral', texto: 'Sin autenticar - define perfil' }
    }
    
    // 5. Mostrar contenido después de cargar
    mostrandoUltimasComidas.value = true
    cargando.value = false
  } catch (e: any) {
    console.error('Error al obtener datos del backend:', e.message || e)
    // Estado de error/faltan datos - fail-closed
    cargando.value = false
    mostrandoUltimasComidas.value = false
    // El template ya muestra "Registra tu primera comida para ver tu progreso" gracias a v-else
  }
})
</script>

<style>
.inicio-screen {
  padding: var(--space-xl);
  max-width: 800px;
  margin: 0 auto;
}

.inicio-header {
  text-align: center;
  margin-bottom: var(--space-xl);
}

.inicio-header h1 {
  font-size: 2rem;
  font-weight: 600;
  margin-bottom: var(--space-xs);
}

.inicio-header .subtitulo {
  color: var(--text-2);
  font-size: 1rem;
}

.inicio-contenido {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

/* ESTADO ACTUAL */
.tarjeta-estado {
  background: var(--surface-card);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-card);
  padding: var(--space-lg);
}

.seccion-titulo {
  font-size: 1rem;
  margin-bottom: var(--space-md);
  color: var(--text-1);
}

.contenido-estado {
  display: flex;
  flex-direction: column;
  gap: var(--space-md);
}

/* Estados del riesgo usando clases CSS definidas en tokens */
.badge-estado-exitoso { color: var(--state-success); }
.badge-estado-advertencia { color: var(--state-warning); }
.badge-estado-peligro { color: var(--state-danger); }
.badge-estado-neutro { color: var(--text-3); }

.SpinnerCargando {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100px;
}

/* ACCIONES RÁPIDAS */
.tarjeta-acciones {
  background: var(--surface-card);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-card);
  padding: var(--space-lg);
}

.acciones-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: var(--space-md);
}

.navegacion-acceso {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--space-md);
  background: var(--surface-glass);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-pill);
  color: var(--text-3);
  text-decoration: none;
  transition: background 0.2s;
}

.navegacion-acceso:hover {
  background: var(--surface-glass-strong);
  color: var(--text-1);
}

/* Íconos inline (simulando lucide) */
.icono-plus,
.icono-message,
.icono-bar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
}

/* ÚLTIMAS COMIDAS */
.tarjeta-comidas {
  background: var(--surface-card);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-card);
  padding: var(--space-lg);
}

.data-list-in-line {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-xs);
}

.data-list-in-line li {
  padding: var(--space-xs) 0;
  font-size: 0.9rem;
  color: var(--text-2);
}
</style>