<template>
  <div v-if="isAuthed()" class="app-shell">
    <div v-if="drawer" class="scrim" @click="drawer = false"></div>
    <Sidebar :open="drawer" :expanded="true" @navigate="drawer = false" @logout="salir" />
    <div class="app-content">
      <div class="topbar">
        <button class="btn-icon glass" type="button" aria-label="Abrir menú" @click="drawer = true">
          <PanelLeft :size="20" :stroke-width="1.5" />
        </button>
        <span class="topbar-title">Nutriólogo</span>
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
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { isAuthed, logout } from './shared/api/auth'
import Sidebar from './shared/components/Sidebar.vue'

const router = useRouter()
const drawer = ref(false)

function salir() {
  logout()
  drawer.value = false
  router.replace('/login')
}
</script>
