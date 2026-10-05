import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiFetch } from '../api/client'

export interface Sexo {
  id: number
  nombre: string
}
export interface NivelActividad {
  id: number
  codigo: string
  nombre: string
  factor: number
  descripcion: string
}
export interface TipoMedicion {
  id: number
  codigo: string
  nombre: string
  unidad: string
  min: number
  max: number
}
export interface Catalogos {
  sexos: Sexo[]
  actividad: NivelActividad[]
  tiposMedicion: TipoMedicion[]
}
export interface Perfil {
  id: number
  idSexoBiologico: number
  idNivelActividadFisica: number
  fechaNacimiento: string
  altura: number
  alturaUnidad: string
}

/**
 * Store compartido de catálogos y perfil.
 *
 * Los catálogos se cargaban por separado en tres vistas y el factor 1.55
 * estaba duplicado. Un endpoint + un store los elimina de raíz.
 *
 * El perfil vive acá para que Objetivo no vuelva a pedir altura/edad/sexo/
 * actividad: los lee de acá y solo permite editarlos en Perfil.
 */
export const useProfileStore = defineStore('profile', () => {
  const catalogos = ref<Catalogos | null>(null)
  const perfil = ref<Perfil | null>(null)
  const cargandoCatalogos = ref(false)
  const cargandoPerfil = ref(false)

  const sexos = computed(() => catalogos.value?.sexos ?? [])
  const actividad = computed(() => catalogos.value?.actividad ?? [])
  const tiposMedicion = computed(() => catalogos.value?.tiposMedicion ?? [])

  const actividadActual = computed(
    () => actividad.value.find((a) => a.id === perfil.value?.idNivelActividadFisica) ?? null,
  )
  const sexoActual = computed(
    () => sexos.value.find((s) => s.id === perfil.value?.idSexoBiologico) ?? null,
  )

  /** Edad derivada del perfil. Base para IMC y TMB en la vista de Perfil. */
  const edad = computed(() => {
    if (!perfil.value?.fechaNacimiento) return null
    const n = new Date(perfil.value.fechaNacimiento)
    const hoy = new Date()
    let a = hoy.getFullYear() - n.getFullYear()
    const m = hoy.getMonth() - n.getMonth()
    if (m < 0 || (m === 0 && hoy.getDate() < n.getDate())) a--
    return a
  })

  async function cargarCatalogos() {
    if (catalogos.value || cargandoCatalogos.value) return
    cargandoCatalogos.value = true
    try {
      catalogos.value = await apiFetch<Catalogos>('/catalogos')
    } finally {
      cargandoCatalogos.value = false
    }
  }

  async function cargarPerfil() {
    if (cargandoPerfil.value) return
    cargandoPerfil.value = true
    try {
      perfil.value = await apiFetch<Perfil | null>('/perfil')
    } catch {
      perfil.value = null
    } finally {
      cargandoPerfil.value = false
    }
  }

  /** Ambos en paralelo: las 3 vistas montan esperando los dos. */
  async function cargarTodo() {
    await Promise.all([cargarCatalogos(), cargarPerfil()])
  }

  async function guardarPerfil(datos: Omit<Perfil, 'id' | 'alturaUnidad'>) {
    const r = await apiFetch<{ id: number }>('/perfil', {
      method: 'POST',
      body: JSON.stringify({
        IdSexoBiologico: datos.idSexoBiologico,
        IdNivelActividadFisica: datos.idNivelActividadFisica,
        FechaNacimiento: datos.fechaNacimiento,
        Altura: datos.altura,
        AlturaUnidad: 'cm',
      }),
    })
    await cargarPerfil()
    return r.id
  }

  return {
    catalogos,
    perfil,
    sexos,
    actividad,
    tiposMedicion,
    actividadActual,
    sexoActual,
    edad,
    cargandoCatalogos,
    cargandoPerfil,
    cargarCatalogos,
    cargarPerfil,
    cargarTodo,
    guardarPerfil,
  }
})