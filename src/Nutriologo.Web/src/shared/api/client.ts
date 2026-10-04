import { logout, token } from './auth'

const API_BASE = import.meta.env.VITE_API_URL ?? '/api'

export interface ApiError {
  status: number
  message: string
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init?.headers ?? {}) as Record<string, string>),
  }
  if (token.value) headers['Authorization'] = `Bearer ${token.value}`
  const res = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (res.status === 401 && !path.startsWith('/auth/')) {
    logout()
    if (location.pathname !== '/login') location.assign('/login')
    throw { status: 401, message: 'Sesión expirada. Inicia sesión de nuevo.' } satisfies ApiError
  }
  if (!res.ok) {
    const body = await res.text().catch(() => '')
    throw { status: res.status, message: body || res.statusText } satisfies ApiError
  }
  return res.json() as Promise<T>
}
