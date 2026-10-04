import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../../features/nutrition/HomeView.vue'
import ObjetivoView from '../../features/nutrition/ObjetivoView.vue'
import MedicionesView from '../../features/nutrition/MedicionesView.vue'
import PerfilView from '../../features/profile/PerfilView.vue'
import RegistroComidaView from '../../features/meals/RegistroComidaView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/objetivo', name: 'objetivo', component: ObjetivoView },
    { path: '/mediciones', name: 'mediciones', component: MedicionesView },
    { path: '/perfil', name: 'perfil', component: PerfilView },
    { path: '/registrar', name: 'registrar', component: RegistroComidaView },
  ],
})
