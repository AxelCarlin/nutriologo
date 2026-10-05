/**
 * Fetches data from the API endpoint.
 * @param endpoint - The API endpoint path (e.g., '/balance?fecha=2024-01-15')
 * @param options - Optional fetch options including headers
 * @returns Parsed JSON response
 */
export async function apiFetch<T>(endpoint: string, options?: {
  headers?: Record<string, string>
  method?: 'GET' | 'POST' | 'DELETE'
  body?: any
}): Promise<T> {
  const url = `/api${endpoint}`
  
  const defaultHeaders: HeadersInit = {
    'Content-Type': 'application/json',
    ...options?.headers,
  }

  // Add JWT token if available
  const token = window.localStorage.getItem('nutriologo_token')
  if (token) {
    defaultHeaders['Authorization'] = `Bearer ${token}`
  }

  const config: RequestInit = {
    method: options?.method || 'GET',
    headers: defaultHeaders,
    // El body llega ya serializado desde el caller (JSON.stringify).
    // Re-serializar aqui producia un string JSON quoted que el backend
    // no podia deserializar.
    ...(options?.body ? { body: options.body } : {}),
  }

  const response = await fetch(url, config)

  if (!response.ok) {
    // Handle 401 - token expired or invalid
    if (response.status === 401) {
      // Clear invalid token
      window.localStorage.removeItem('nutriologo_token')
      window.localStorage.removeItem('nutriologo_nombre')
      // Redirect to login
      window.location.href = '/login'
    }
    const errorText = await response.text()
    throw {
      status: response.status,
      message: errorText || 'Error en la petición API',
    }
  }

  // 204 Sin contenido (p.ej. DELETE): no hay JSON que parsear.
  if (response.status === 204) return null as T

  return response.json()
}

/**
 * Checks if user is authenticated via JWT token.
 */
export function isAuthenticated(): boolean {
  return window.localStorage.getItem('nutriologo_token') !== null
}

/**
 * Gets the user name from localStorage.
 */
export function getUserName(): string | null {
  return window.localStorage.getItem('nutriologo_nombre') || null
}
