<template>
  <aside
    class="sb"
    :class="{ 'sb--rail': rail, 'sb--drawer': drawer }"
    aria-label="Navegación principal"
  >
    <!-- ══════ ENCABEZADO 76px ══════ -->
    <header class="sb__hd">
      <span class="sb__mono" aria-hidden="true">N</span>
      <span v-if="!rail" class="sb__wm">nutriólogo</span>
    </header>

    <!-- ══════ ACCIONES RÁPIDAS ══════ -->
    <div v-if="!rail" class="sb__acts">
      <button class="sb__act" type="button" @click="$emit('nueva-conversacion')">
        <Plus :size="18" :stroke-width="1.5" />
        <span class="sb__lb">Nueva conversación</span>
        <kbd class="sb__kc" aria-hidden="true">N</kbd>
      </button>
      <button class="sb__act" type="button" @click="$emit('abrir-paleta')">
        <Search :size="18" :stroke-width="1.5" />
        <span class="sb__lb">Buscar</span>
        <kbd class="sb__kc" aria-hidden="true">⌘K</kbd>
      </button>
    </div>

    <!-- ══════ NAVEGACIÓN ══════ -->
    <nav ref="navRef" class="sb__nav" aria-label="Principal">
      <i ref="thRef" class="sb__th" aria-hidden="true" />

      <p v-if="!rail" class="sb__grp">Hoy</p>
      <RouterLink
        v-for="item in grupoHoy"
        :key="item.to"
        :to="item.to"
        class="sb__a"
        :title="item.label"
        :aria-label="item.label"
        @click="irA"
      >
        <component :is="item.icon" :size="18" :stroke-width="1.5" />
        <span class="sb__lb">{{ item.label }}</span>
        <span v-if="item.to === '/registrar' && contadorComidas > 0" class="sb__cnt">
          {{ contadorComidas }}<span class="sb__sr"> comidas hoy</span>
        </span>
      </RouterLink>

      <!-- Historial de conversaciones: el chevron es HERMANO del enlace -->
      <div class="sb__row">
        <RouterLink
          to="/chat"
          class="sb__a"
          title="Chat"
          aria-label="Chat"
          @click="irA"
        >
          <MessageCircle :size="18" :stroke-width="1.5" />
          <span class="sb__lb">Chat</span>
        </RouterLink>
        <button
          class="sb__ex"
          type="button"
          :aria-expanded="historialAbierto"
          aria-controls="sb-hist"
          :aria-label="historialAbierto ? 'Ocultar conversaciones' : 'Mostrar conversaciones recientes'"
          @click="alternarHistorial"
        />
      </div>

      <div id="sb-hist" class="sb__sub" :class="{ 'sb__sub--open': historialAbierto }">
        <div>
          <a
            v-for="c in conversaciones"
            :key="c.id"
            href="#"
            class="sb__cv"
            :class="{ 'sb__cv--on': c.id === conversacionActiva }"
            :aria-current="c.id === conversacionActiva ? 'true' : undefined"
            @click.prevent="abrirConversacion(c)"
          >
            {{ c.titulo }}
          </a>
        </div>
      </div>

      <p v-if="!rail" class="sb__grp">Seguimiento</p>
      <RouterLink
        v-for="item in grupoSeguimiento"
        :key="item.to"
        :to="item.to"
        class="sb__a"
        :title="item.pendiente ? `${item.label}, pendiente` : item.label"
        :aria-label="item.pendiente ? `${item.label}, pendiente` : item.label"
        @click="irA"
      >
        <component :is="item.icon" :size="18" :stroke-width="1.5" />
        <span class="sb__lb">{{ item.label }}</span>
        <!-- Rojo SOLO si el backend indica algo pendiente -->
        <i
          v-if="item.pendiente"
          class="sb__bd"
          role="img"
          aria-label="Pendiente"
        />
      </RouterLink>
    </nav>

    <!-- ══════ INSTRUMENTO HOY ══════ -->
    <div v-if="!rail && panel" class="sb__hoy" role="group" :aria-label="hoyAria">
      <p class="sb__lab">Hoy · kcal</p>
      <div class="sb__num">
        {{ panel.caloriasConsumidas.toLocaleString('es-MX') }}<small v-if="panel.metaCalorica"> / {{ panel.metaCalorica.toLocaleString('es-MX') }}</small>
      </div>
      <div v-if="panel.metaCalorica" class="sb__seg" role="img" :aria-label="hoyAria">
        <i
          v-for="i in 20"
          :key="i"
          :class="{ 'sb__seg--on': i <= segmentosEncendidos }"
          :style="{ transitionDelay: `${i * 45}ms` }"
        />
      </div>
      <p v-else class="sb__lab sb__lab--vacio">[SIN OBJETIVO]</p>

      <div v-if="macrosConDatos.length" class="sb__mac" role="group" aria-label="Macronutrientes">
        <div v-for="m in macrosConDatos" :key="m.codigo">
          <span :title="m.nombre">{{ m.codigo }}</span>
          <span class="sb__tr"><i :style="{ width: `${m.pct}%` }" /></span>
          <span>{{ m.gramos }}g</span>
        </div>
      </div>

      <div v-if="racha > 0" class="sb__rch" role="img" :aria-label="`Racha de ${racha} días`">
        <span class="sb__lab">Racha {{ racha }}</span>
        <i
          v-for="i in 7"
          :key="i"
          :class="{ 'sb__rch--on': i <= racha, 'sb__rch--td': i === racha + 1 }"
        />
      </div>
    </div>

    <!-- ══════ PIE 76px ══════ -->
    <footer class="sb__ft">
      <span class="sb__mono sb__mono--sm" aria-hidden="true">{{ iniciales }}</span>
      <div v-if="!rail" class="sb__who">
        <span class="sb__who-nombre">{{ userName }}</span>
        <span class="sb__lab">Local</span>
      </div>
      <button
        v-if="!rail"
        class="sb__ib"
        type="button"
        aria-label="Perfil"
        title="Perfil"
        @click="irA('/perfil')"
      >
        <User :size="18" :stroke-width="1.5" />
      </button>
      <button
        class="sb__ib"
        type="button"
        aria-label="Salir"
        title="Salir"
        @click="$emit('logout')"
      >
        <LogOut :size="18" :stroke-width="1.5" />
      </button>
    </footer>
  </aside>
</template>

<script setup lang="ts">
import {
  BarChart3,
  LogOut,
  MessageCircle,
  Plus,
  Ruler,
  Search,
  Target,
  User,
} from 'lucide-vue-next'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiFetch } from '../../shared/api/client'
import { userName } from '../../shared/api/auth'

const props = defineProps<{ open: boolean; expanded: boolean; rail: boolean; drawer: boolean }>()
const emit = defineEmits(['navigate', 'logout', 'nueva-conversacion', 'abrir-paleta'])

const router = useRouter()
const route = useRoute()

// ════════════════════════════════════════════════════════════
// NAVEGACIÓN
// ════════════════════════════════════════════════════════════

const grupoHoy = [
  { to: '/', label: 'Inicio', icon: BarChart3 },
  { to: '/chat', label: 'Chat', icon: MessageCircle },
  { to: '/registrar', label: 'Registrar', icon: Plus },
]

const grupoSeguimiento = computed(() => [
  { to: '/objetivo', label: 'Objetivo', icon: Target, pendiente: !panel.value?.tieneObjetivo },
  { to: '/mediciones', label: 'Mediciones', icon: Ruler, pendiente: false },
  { to: '/balance', label: 'Balance', icon: BarChart3, pendiente: false },
])

// ════════════════════════════════════════════════════════════
// PÍLDORA QUE DESLIZA
// ════════════════════════════════════════════════════════════

const navRef = ref<HTMLElement | null>(null)
const thRef = ref<HTMLElement | null>(null)

function colocarPildora(inmediato = false) {
  const nav = navRef.value
  const th = thRef.value
  if (!nav || !th) return

  const activo = nav.querySelector<HTMLElement>('.sb__a[aria-current="page"]')
  if (!activo) {
    th.style.opacity = '0'
    return
  }

  // getBoundingClientRect relativo al nav + scrollTop: si el nav hace scroll,
  // offsetTop solo no alcanza.
  const y = activo.getBoundingClientRect().top - nav.getBoundingClientRect().top + nav.scrollTop
  const h = activo.getBoundingClientRect().height || 44

  if (inmediato) {
    th.style.transition = 'none'
    th.style.opacity = '1'
    th.style.height = `${h}px`
    th.style.transform = `translateY(${y}px)`
    // Al siguiente frame vuelve la transición: si no, el primer pintado
    // animaría desde arriba.
    requestAnimationFrame(() => requestAnimationFrame(() => { th.style.transition = '' }))
    return
  }

  th.style.opacity = '1'
  th.style.height = `${h}px`
  th.style.transform = `translateY(${y}px)`
}

// ════════════════════════════════════════════════════════════
// HISTORIAL DE CONVERSACIONES
// ════════════════════════════════════════════════════════════

interface Conversacion {
  id: number
  titulo: string
  fechaModificacion: string
}

const conversaciones = ref<Conversacion[]>([])
const conversacionActiva = ref<number | null>(null)
const historialAbierto = ref(false)

async function cargarConversaciones() {
  try {
    conversaciones.value = await apiFetch<Conversacion[]>('/conversaciones')
  } catch {
    // Sin conversaciones o sin red: el historial queda vacío, no es un error.
    conversaciones.value = []
  }
}

function alternarHistorial() {
  historialAbierto.value = !historialAbierto.value
  // Al terminar la animación los ítems de abajo se desplazan: la píldora tiene
  // que seguir sobre Chat.
  if (historialAbierto.value) {
    document.getElementById('sb-hist')?.addEventListener('transitionend', () => colocarPildora(), { once: true })
  }
}

function abrirConversacion(c: Conversacion) {
  conversacionActiva.value = c.id
  historialAbierto.value = true
  void router.push('/chat')
  emit('navigate')
}

function irA(destino?: string) {
  // Los RouterLink llaman @click="irA" pasando el MouseEvent: hay que
  // distinguirlo de un destino real, o router.push(evento) falla en silencio
  // y la navegación del enlace se pierde.
  if (destino && typeof destino === 'string') void router.push(destino)
  conversacionActiva.value = null
  emit('navigate')
}

// ════════════════════════════════════════════════════════════
// INSTRUMENTO HOY: datos reales del backend
// ════════════════════════════════════════════════════════════

interface Panel {
  caloriasConsumidas: number
  metaCalorica: number | null
  tieneObjetivo: boolean
  macros: Record<string, number>
  serie: { dia: string; kcal: number }[]
}

const panel = ref<Panel | null>(null)
const contadorComidas = ref(0)

const MACROS: { codigo: string; nombre: string }[] = [
  { codigo: 'PROTEINA', nombre: 'Proteína' },
  { codigo: 'CARBOHIDRATO', nombre: 'Carbohidratos' },
  { codigo: 'GRASA', nombre: 'Grasa' },
]

const macrosConDatos = computed(() => {
  if (!panel.value) return []
  return MACROS
    .map(m => {
      const gramos = panel.value!.macros[m.codigo] ?? 0
      // La pista es proporcional al mayor de los tres: sin un denominador fijo
      // no hay forma de escalar, y el brief pide no inventar valores.
      return { ...m, gramos: Math.round(gramos), pct: 0 }
    })
    .filter(m => m.gramos > 0)
    .map(m => {
      const max = Math.max(...MACROS.map(x => Math.round(panel.value!.macros[x.codigo] ?? 0)), 1)
      return { ...m, pct: Math.min(100, Math.round((m.gramos / max) * 100)) }
    })
})

const segmentosEncendidos = computed(() => {
  const p = panel.value
  if (!p?.metaCalorica) return 0
  return Math.min(20, Math.round((p.caloriasConsumidas / p.metaCalorica) * 20))
})

const hoyAria = computed(() => {
  const p = panel.value
  if (!p) return 'Sin datos de hoy'
  if (!p.metaCalorica) return `${p.caloriasConsumidas} kilocalorías, sin meta definida`
  return `${p.caloriasConsumidas} de ${p.metaCalorica} kilocalorías`
})

// Racha: días consecutivos con al menos un registro (kcal > 0), hasta 7.
const racha = computed(() => {
  const p = panel.value
  if (!p) return 0
  const dias = new Set(p.serie.filter(s => s.kcal > 0).map(s => s.dia.slice(0, 10)))
  if (!dias.size) return 0
  let n = 0
  for (let i = 0; i < 7; i++) {
    const d = new Date()
    d.setDate(d.getDate() - i)
    const clave = d.toISOString().slice(0, 10)
    if (dias.has(clave)) n++
    else if (i > 0) break
  }
  return n
})

async function cargarPanel() {
  try {
    panel.value = await apiFetch<Panel>('/panel')
  } catch {
    panel.value = null
  }
}

async function cargarContador() {
  try {
    const items = await apiFetch<unknown[]>('/comidas')
    contadorComidas.value = Array.isArray(items) ? items.length : 0
  } catch {
    contadorComidas.value = 0
  }
}

// ════════════════════════════════════════════════════════════
// CICLO DE VIDA
// ════════════════════════════════════════════════════════════

const iniciales = computed(() => {
  const nombre = (userName.value ?? '').trim()
  if (!nombre) return '??'
  return nombre
    .split(/\s+/)
    .slice(0, 2)
    .map(p => p[0]!.toUpperCase())
    .join('')
})

async function cargarNombre() {
  if (userName.value) return
  try {
    const r = await apiFetch<{ nombre: string }>('/usuario')
    if (r.nombre) userName.value = r.nombre
  } catch {
    // Sin nombre: el monograma muestra "??" y el pie queda sin nombre.
  }
}

onMounted(async () => {
  colocarPildora(true)
  await Promise.all([cargarPanel(), cargarContador(), cargarConversaciones(), cargarNombre()])
  await nextTick()
  colocarPildora(true)
})

// Al cambiar de ruta, la píldora sigue al nuevo activo.
watch(() => route.fullPath, () => nextTick(() => colocarPildora()))

// Al abrir el cajón móvil, la píldora tiene que recolocarse.
watch(() => props.open, async (abierto: boolean) => {
  if (!abierto) return
  await nextTick()
  colocarPildora(true)
})

// Recalcular si cambia el tamaño de la ventana (riel ↔ completo).
let resizeTimer: ReturnType<typeof setTimeout> | undefined
window.addEventListener('resize', () => {
  clearTimeout(resizeTimer)
  resizeTimer = setTimeout(() => colocarPildora(), 120)
})

// Exponer recarga para que App.vue la llame al registrar una comida.
defineExpose({ recargar: () => { void cargarPanel(); void cargarContador() } })
</script>
