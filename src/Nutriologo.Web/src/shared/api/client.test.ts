import { describe, it, expect } from 'vitest'
import { apiFetch } from './client'

describe('apiFetch', () => {
  it('existe y es función', () => {
    expect(typeof apiFetch).toBe('function')
  })
})
