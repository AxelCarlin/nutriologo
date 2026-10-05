<template>
  <div v-if="isAuthed()" class="app-shell">
    <div v-if="drawer" class="scrim" @click="drawer = false"></div>
    <Sidebar :open="drawer" :expanded="true" @navigate="drawer = false" @logout="salir" />
    <div class="app-content">
      <div class="app-topbar">
        <button class="btn-icon" type="button" aria-label="Abrir menú" @click="drawer = true">
          <PanelLeft :size="18" :stroke-width="1.5" />
        </button>
        <span class="app-topbar__title">{{ tituloPantalla }}</span>
      </div>
      <main class="app-main">
        <RouterView />
      </main>
    </div>
  </div>
  <RouterView v-else />
</template>

<script setup lang="ts">
import { PanelLeft } from 'lucide-vue-next'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { isAuthed, logout } from './shared/api/auth'
import Sidebar from './shared/components/Sidebar.vue'

const router = useRouter()
const route = useRoute()
const drawer = ref(false)

/**
 * La barra móvil muestra la pantalla actual, no la app: el icono ya abre el
 * menú (con aria-label) y la marca vive en el sidebar y el login.
 */
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

function salir() {
  logout()
  drawer.value = false
  router.replace('/login')
}
</script>
