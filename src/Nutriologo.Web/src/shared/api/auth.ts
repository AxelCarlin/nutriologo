import { ref } from 'vue'
import { apiFetch } from './client'

const KEY = 'nutriologo_token'
const NAME = 'nutriologo_nombre'

export const token = ref<string | null>(localStorage.getItem(KEY))
export const userName = ref<string | null>(localStorage.getItem(NAME))

export const isAuthed = () => token.value !== null

export async function login(correo: string, password: string) {
  const r = await apiFetch<{ id: number; nombre: string; token: string }>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ correo, password }),
  })
  setSession(r.token, r.nombre)
}

export async function registro(nombre: string, correo: string, password: string) {
  const r = await apiFetch<{ id: number; token: string }>('/auth/registro', {
    method: 'POST',
    body: JSON.stringify({ nombre, correo, password }),
  })
  setSession(r.token, nombre)
}

export function logout() {
  token.value = null
  userName.value = null
  localStorage.removeItem(KEY)
  localStorage.removeItem(NAME)
}

function setSession(t: string, nombre: string) {
  token.value = t
  userName.value = nombre
  localStorage.setItem(KEY, t)
  localStorage.setItem(NAME, nombre)
}
