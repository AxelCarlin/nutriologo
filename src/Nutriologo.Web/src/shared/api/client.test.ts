import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from './client'

describe('apiFetch', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
    localStorage.setItem('nutriologo_token', 'tok123')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('antepone /api exactamente una vez', async () => {
    // El bug original pasaba '/api/balance' desde Inicio.vue sobre un cliente
    // que ya anteponia '/api' -> /api/api/balance -> 404.
    ;(fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => ({}),
    })

    await apiFetch('/balance?fecha=2026-10-04')

    const [url] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/api/balance?fecha=2026-10-04')
  })

  it('manda el body tal cual, sin re-serializar', async () => {
    // Los callers ya pasan JSON.stringify; un segundo stringify lo convertia
    // en un string JSON quoted que el backend no deserializaba.
    ;(fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => ({}),
    })

    await apiFetch('/comidas', { method: 'POST', body: JSON.stringify({ Nombre: 'x' }) })

    const [, config] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(config.body).toBe('{"Nombre":"x"}')
    expect(config.body.startsWith('"')).toBe(false)
  })

  it('inyecta el JWT del localStorage', async () => {
    ;(fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: true,
      json: async () => ({}),
    })

    await apiFetch('/panel')

    const [, config] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(config.headers['Authorization']).toBe('Bearer tok123')
  })

  it('lanza {status, message} en error', async () => {
    ;(fetch as ReturnType<typeof vi.fn>).mockResolvedValue({
      ok: false,
      status: 500,
      text: async () => 'boom',
    })

    // El catch de Inicio.vue lee e.status, no e.message sobre una Error.
    await expect(apiFetch('/panel')).rejects.toMatchObject({
      status: 500,
      message: 'boom',
    })
  })
})