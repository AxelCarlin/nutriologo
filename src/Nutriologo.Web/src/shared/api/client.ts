import { localStorage } from '@/shared/utils/localStorage'

// Base URL for API calls
const BASE_URL = '/api'

/**
 * Fetches data from the API endpoint.
 * @param endpoint - The API endpoint path (e.g., '/balance?fecha=2024-01-15')
 * @param options - Optional fetch options including headers
 * @returns Parsed JSON response
 */
export async function apiFetch<T>(endpoint: string, options?: {
  headers?: Record<string, string>
  method?: 'GET' | 'POST'
  body?: any
}): Promise<T> {
  const url = `${BASE_URL}${endpoint}`
  
  const defaultHeaders: HeadersInit = {
    'Content-Type': 'application/json',
    ...options?.headers,
  }

  // Add JWT token if available
  const token = localStorage.getItem('nutriologo_token')
  if (token) {
    defaultHeaders['Authorization'] = `Bearer ${token}`
  }

  const config: RequestInit = {
    method: options?.method || 'GET',
    headers: defaultHeaders,
    ...(options?.body ? { body: JSON.stringify(options.body) } : {}),
  }

  const response = await fetch(url, config)

  if (!response.ok) {
    // Handle 401 - token expired or invalid
    if (response.status === 401) {
      // Clear invalid token
      localStorage.removeItem('nutriologo_token')
      localStorage.removeItem('nutriologo_nombre')
      // Redirect to login
      window.location.href = '/login'
    }
    const errorText = await response.text()
    throw {
      status: response.status,
      message: errorText || 'Error en la petición API',
    }
  }

  return response.json()
}

// Helper function to check if user is authenticated
export function isAuthenticated(): boolean {
  return localStorage.getItem('nutriologo_token') !== null
}

// Helper function to get user name
export function getUserName(): string | null {
  return localStorage.getItem('nutriologo_nombre') || null
}