import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../../features/nutrition/HomeView.vue'
import ObjetivoView from '../../features/nutrition/ObjetivoView.vue'
import MedicionesView from '../../features/nutrition/MedicionesView.vue'
import PerfilView from '../../features/profile/PerfilView.vue'
import RegistroComidaView from '../../features/meals/RegistroComidaView.vue'
import ConversacionView from '../../features/conversation/ConversacionView.vue'
import BalanceView from '../../features/meals/BalanceView.vue'
import LoginView from '../../features/auth/LoginView.vue'
import { isAuthed } from '../../shared/api/auth'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView, meta: { public: true } },
    { path: '/', name: 'home', component: HomeView },
    { path: '/objetivo', name: 'objetivo', component: ObjetivoView },
    { path: '/mediciones', name: 'mediciones', component: MedicionesView },
    { path: '/perfil', name: 'perfil', component: PerfilView },
    { path: '/registrar', name: 'registrar', component: RegistroComidaView },
    { path: '/balance', name: 'balance', component: BalanceView },
    { path: '/chat', name: 'chat', component: ConversacionView },
  ],
})

router.beforeEach((to) => {
  if (!to.meta.public && !isAuthed()) return { path: '/login' }
  if (to.path === '/login' && isAuthed()) return { path: '/chat' }
})
