<template>
  <aside class="sidebar" :class="{ open }" aria-label="Navegación principal">
    <div class="sidebar-brand">
      <span class="sidebar-logo">N</span>
      <span v-if="expanded" class="sidebar-brand-name">Nutriólogo</span>
    </div>
    <span v-if="expanded" class="sidebar-label">Navegación</span>
    <nav class="sidebar-nav">
      <RouterLink
        v-for="item in items"
        :key="item.to"
        :to="item.to"
        class="sidebar-link"
        :title="item.label"
        @click="$emit('navigate')"
      >
        <component :is="item.icon" :size="16" :stroke-width="1.5" />
        <span v-if="expanded" class="sidebar-link__label">{{ item.label }}</span>
      </RouterLink>
    </nav>
    <div class="sidebar-footer">
      <div v-if="expanded && userName" class="sidebar-user">{{ userName }}</div>
      <button type="button" class="sidebar-link sidebar-logout" title="Cerrar sesión" @click="$emit('logout')">
        <LogOut :size="16" :stroke-width="1.5" />
        <span v-if="expanded" class="sidebar-link__label">Salir</span>
      </button>
    </div>
  </aside>
</template>

<script setup lang="ts">
import {
  BarChart3,
  Home,
  LogOut,
  MessageCircle,
  PlusCircle,
  Ruler,
  Target,
  User,
} from 'lucide-vue-next'
import { userName } from '../../shared/api/auth'

defineProps<{ open: boolean; expanded: boolean }>()
defineEmits(['navigate', 'logout'])

const items = [
  { to: '/', label: 'Inicio', icon: Home },
  { to: '/chat', label: 'Chat', icon: MessageCircle },
  { to: '/registrar', label: 'Registrar', icon: PlusCircle },
  { to: '/objetivo', label: 'Objetivo', icon: Target },
  { to: '/mediciones', label: 'Mediciones', icon: Ruler },
  { to: '/perfil', label: 'Perfil', icon: User },
  { to: '/balance', label: 'Balance', icon: BarChart3 },
]
</script>
