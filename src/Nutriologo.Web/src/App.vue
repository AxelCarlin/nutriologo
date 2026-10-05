<template>
  <div v-if="isAuthed()" class="app-shell">
    <div v-if="drawer" class="scrim" @click="drawer = false"></div>
    <Sidebar
      ref="sidebarRef"
      :open="drawer"
      :expanded="true"
      :rail="rail"
      :drawer="drawer"
      @navigate="drawer = false"
      @logout="salir"
      @nueva-conversacion="nuevaConversacion"
      @abrir-paleta="paletaAbierta = true"
    />
    <div class="app-content">
      <div class="app-topbar">
        <button class="btn-icon" type="button" aria-label="Abrir menú" :aria-expanded="drawer" aria-controls="sidebar" @click="drawer = true">
          <PanelLeft :size="18" :stroke-width="1.5" />
        </button>
        <span class="app-topbar__title">{{ tituloPantalla }}</span>
      </div>
      <main class="app-main">
        <RouterView />
      </main>
    </div>

    <!-- Paleta de comandos -->
    <div
      v-if="paletaAbierta"
      class="pal"
      role="dialog"
      aria-modal="true"
      aria-label="Paleta de comandos"
      @click.self="paletaAbierta = false"
    >
      <div class="pal__box">
        <input
          ref="paletaInputRef"
          v-model="paletaQuery"
          class="pal__input"
          role="combobox"
          aria-expanded="true"
          aria-controls="pal-lista"
          aria-autocomplete="list"
          :aria-activedescendant="paletaOpciones.length ? `pal-op-${paletaSeleccionada}` : undefined"
          placeholder="Escribe un comando"
          autocomplete="off"
          aria-label="Comando"
          @input="paletaSeleccionada = 0"
          @keydown="manejadorPaleta"
        />
        <div id="pal-lista" class="pal__lista" role="listbox" aria-label="Resultados">
          <div
            v-for="(op, i) in paletaOpciones"
            :key="op.etiqueta"
            :id="`pal-op-${i}`"
            class="pal__op"
            :class="{ 'pal__op--sel': i === paletaSeleccionada }"
            role="option"
            :aria-selected="i === paletaSeleccionada"
            @click="ejecutarPaleta(i)"
          >
            {{ op.etiqueta }}
          </div>
          <p v-if="!paletaOpciones.length" class="pal__vacio">[Sin resultados]</p>
        </div>
        <p class="pal__ayuda">↑↓ navegar · Enter ejecutar · Esc cerrar</p>
      </div>
    </div>
  </div>
  <RouterView v-else />
</template>

<script setup lang="ts">
import { PanelLeft } from 'lucide-vue-next'
import { computed, nextTick, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { isAuthed, logout } from './shared/api/auth'
import Sidebar from './shared/components/Sidebar.vue'

const router = useRouter()
const route = useRoute()
const drawer = ref(false)
const rail = ref(false)
const paletaAbierta = ref(false)
const paletaQuery = ref('')
const paletaSeleccionada = ref(0)
const paletaInputRef = ref<HTMLInputElement | null>(null)

const TITULOS: Record<string, string> = {
  home: 'INICIO',
  chat: 'CHAT',
  registrar: 'REGISTRAR',
  objetivo: 'OBJETIVO',
  mediciones: 'MEDICIONES',
  perfil: 'PERFIL',
  balance: 'BALANCE',
}
const tituloPantalla = computed(() =>
  typeof route.name === 'string' ? (TITULOS[route.name] ?? 'NUTRIÓLOGO') : 'NUTRIÓLOGO',
)

// ════════════════════════════════════════════════════════════
// MODO RIEL: ≤ 900px
// ════════════════════════════════════════════════════════════

function sincronizarRiel() {
  rail.value = window.innerWidth <= 900
}

sincronizarRiel()
window.addEventListener('resize', sincronizarRiel)

// ════════════════════════════════════════════════════════════
// PALETA DE COMANDOS
// ════════════════════════════════════════════════════════════

interface OpcionPaleta {
  etiqueta: string
  accion: () => void
}

const rutas: { to: string; etiqueta: string }[] = [
  { to: '/', etiqueta: 'Ir a Inicio' },
  { to: '/chat', etiqueta: 'Ir a Chat' },
  { to: '/registrar', etiqueta: 'Registrar comida' },
  { to: '/objetivo', etiqueta: 'Ir a Objetivo' },
  { to: '/mediciones', etiqueta: 'Ir a Mediciones' },
  { to: '/balance', etiqueta: 'Ir a Balance' },
  { to: '/perfil', etiqueta: 'Ir a Perfil' },
]

const paletaOpciones = computed<OpcionPaleta[]>(() => {
  const q = paletaQuery.value.trim().toLowerCase()
  const lista = rutas.filter(r => r.etiqueta.toLowerCase().includes(q))
  return lista.map(r => ({
    etiqueta: r.etiqueta,
    accion: () => { void router.push(r.to) },
  }))
})

function abrirPaleta() {
  paletaAbierta.value = true
  paletaQuery.value = ''
  paletaSeleccionada.value = 0
  void nextTick(() => paletaInputRef.value?.focus())
}

function cerrarPaleta() {
  paletaAbierta.value = false
  paletaInputRef.value?.blur()
}

function ejecutarPaleta(i: number) {
  const op = paletaOpciones.value[i]
  if (!op) return
  cerrarPaleta()
  op.accion()
}

function manejadorPaleta(e: KeyboardEvent) {
  const n = paletaOpciones.value.length
  if (e.key === 'ArrowDown' && n) {
    paletaSeleccionada.value = (paletaSeleccionada.value + 1) % n
    e.preventDefault()
  } else if (e.key === 'ArrowUp' && n) {
    paletaSeleccionada.value = (paletaSeleccionada.value - 1 + n) % n
    e.preventDefault()
  } else if (e.key === 'Enter') {
    ejecutarPaleta(paletaSeleccionada.value)
  } else if (e.key === 'Escape') {
    cerrarPaleta()
  } else if (e.key === 'Tab') {
    e.preventDefault()
  }
}

// Atajos globales: ⌘K / Ctrl+K abre la paleta
window.addEventListener('keydown', (e: KeyboardEvent) => {
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault()
    paletaAbierta.value ? cerrarPaleta() : abrirPaleta()
  }
})

// ════════════════════════════════════════════════════════════
// ACCIONES DEL SIDEBAR
// ════════════════════════════════════════════════════════════

function nuevaConversacion() {
  void router.push('/chat')
  drawer.value = false
}

function salir() {
  logout()
  drawer.value = false
  void router.replace('/login')
}
</script>

<style>
/* Paleta de comandos */
.pal {
  position: fixed;
  inset: 0;
  z-index: 30;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  padding-top: 90px;
  background: color-mix(in srgb, var(--bg) 88%, transparent);
}

.pal__box {
  width: min(420px, calc(100% - 32px));
  border: 1px solid var(--t1);
  background: var(--bg);
  animation: pal-entrada .3s var(--fl);
}

@keyframes pal-entrada {
  from { opacity: 0; transform: translateY(-8px); }
}

.pal__input {
  width: 100%;
  background: none;
  border: 0;
  border-bottom: 1px solid var(--bd2);
  padding: 16px;
  font: 300 16px 'Space Grotesk', sans-serif;
  color: var(--t1);
  outline: 0;
  box-sizing: border-box;
}

.pal__lista {
  max-height: 300px;
  overflow-y: auto;
}

.pal__op {
  display: flex;
  align-items: center;
  height: 44px;
  padding: 0 16px;
  font: 400 11px/1 var(--nd-font-mono);
  letter-spacing: .08em;
  text-transform: uppercase;
  color: var(--t2);
  cursor: pointer;
}

.pal__op--sel {
  background: var(--t1);
  color: var(--bg);
}

.pal__vacio {
  padding: 16px;
  font: 400 11px/1 var(--nd-font-mono);
  color: var(--t3);
  margin: 0;
}

.pal__ayuda {
  padding: 10px 16px;
  border-top: 1px solid var(--bd);
  font: 400 11px/1 var(--nd-font-mono);
  color: var(--t3);
  margin: 0;
}

@media (prefers-reduced-motion: reduce) {
  .pal__box { animation: none; }
}
</style>
