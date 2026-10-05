import { mount, RouterLinkStub } from '@vue/test-utils'
import { beforeEach, describe, expect, it } from 'vitest'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
// isAuthed() lee un ref hidratado en el import: en tests hay que setearlo
// directo, porque localStorage se escribe después del import.
import { token } from './shared/api/auth'

/**
 * La barra móvil muestra la pantalla actual, no la marca: el icono ya abre
 * el menú y la marca vive en el sidebar y el login.
 */
describe('App topbar', () => {
  beforeEach(() => {
    token.value = 'tok'
  })

  async function montarEn(path: string) {
    const router = createRouter({
      history: createWebHistory(),
      routes: [
        { path: '/login', name: 'login', component: { template: '<div />' } },
        { path: '/objetivo', name: 'objetivo', component: { template: '<div />' } },
        { path: '/chat', name: 'chat', component: { template: '<div />' } },
      ],
    })
    await router.push(path)
    await router.isReady()
    const w = mount(App, {
      global: {
        plugins: [router],
        stubs: { Sidebar: true, RouterView: RouterLinkStub },
      },
    })
    await w.vm.$nextTick()
    return w
  }

  it.each([
    ['/objetivo', 'OBJETIVO'],
    ['/chat', 'CHAT'],
  ])('en %s muestra %s', async (path, titulo) => {
    const w = await montarEn(path)
    expect(w.find('.app-topbar__title').text()).toBe(titulo)
  })

  it('el botón conserva su etiqueta accesible', async () => {
    const w = await montarEn('/objetivo')
    expect(w.find('.app-topbar button').attributes('aria-label')).toBe('Abrir menú')
  })
})
