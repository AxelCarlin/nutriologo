const API_BASE = import.meta.env.VITE_API_URL ?? '/api'

export interface ApiError {
  status: number
  message: string
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    ...init,
  })
  if (!res.ok) {
    const body = await res.text().catch(() => '')
    throw { status: res.status, message: body || res.statusText } satisfies ApiError
  }
  return res.json() as Promise<T>
}
