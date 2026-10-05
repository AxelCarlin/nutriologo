import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

/**
 * El DOM de happy-dom no carga las hojas de estilo, así que `getComputedStyle`
 * devuelve vacío en los tests de componente. Estas aserciones leen el CSS real
 * para fijar las reglas que el usuario ve.
 *
 * No es una forma elegante de testear: preferible sería medir en un navegador
 * (ver `verificar-layout.mjs`). Se deja como red de seguridad: si alguien borra
 * una regla o reintroduce la colisión con otra hoja, esto salta.
 */

const raiz = resolve(__dirname, '..')
const leer = (rel: string) => readFileSync(resolve(raiz, rel), 'utf8')

const chat = leer('src/styles/components/chat.css')
const composer = leer('src/styles/components/composer.css')
const indice = leer('src/styles/index.css')
const botones = leer('src/styles/components/buttons.css')
const forms = leer('src/styles/components/forms.css')

describe('chat.css — el asistente sin caja', () => {
  it('.msg--agent no declara borde, fondo ni min-height', () => {
    const bloque = /\.msg--agent\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(bloque).not.toMatch(/border\s*:/)
    expect(bloque).not.toMatch(/background\s*:/)
    expect(bloque).not.toMatch(/min-height\s*:/)
  })

  it('.msg__texto no tiene min-height (nada de hueco vacío)', () => {
    const bloque = /\.msg__texto\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(bloque).toMatch(/min-height:\s*0/)
    expect(bloque).not.toMatch(/min-height:\s*[1-9]/)
  })

  it('.msg__texto ya no usa pre-wrap (el markdown se encarga)', () => {
    const bloque = /\.msg__texto\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(bloque).not.toMatch(/pre-wrap/)
  })

  it('la burbuja del usuario es la única caja: asimétrica y a la derecha', () => {
    const burbuja = /\.msg__burbuja\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(burbuja).toMatch(/border:\s*1px solid var\(--bd2\)/)
    expect(burbuja).toMatch(/border-radius:\s*16px 16px 4px 16px/)
    expect(burbuja).toMatch(/max-width:\s*min\(86%, 560px\)/)
    expect(burbuja).toMatch(/padding:\s*10px 16px/)

    const li = /\.msg--user\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(li).toMatch(/justify-content:\s*flex-end/)
    // La caja del <li> viene de otra hoja vieja: aquí no se declara.
    expect(li).not.toMatch(/border\s*:/)
  })
})

describe('chat.css — una sola columna', () => {
  it('.col comparte ancho y padding en las tres zonas', () => {
    expect(chat).toMatch(/\.col\s*\{[^}]*max-width:\s*var\(--nd-chat-w\)/)
    expect(chat).toMatch(/\.col\s*\{[^}]*padding-inline:\s*24px/)
    expect(chat).toMatch(/\.col\s*\{[^}]*margin-inline:\s*auto/)
  })

  it('el hilo no reserva carril de scroll (desalineaba el texto)', () => {
    const bloque = /\.chat__hilo\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(bloque).toMatch(/scrollbar-width:\s*thin/)
    expect(bloque).toMatch(/scrollbar-color:\s*var\(--bd2\) transparent/)
    // `stable` adds ~15px and pushed the text out of line with the header.
    expect(bloque).not.toMatch(/scrollbar-gutter:\s*stable/)
  })

  it('[BAJAR] es fijo y fuera del hilo, no sticky dentro', () => {
    const bloque = /\.bajar\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    expect(bloque).toMatch(/position:\s*fixed/)
    expect(bloque).not.toMatch(/position:\s*sticky/)
  })
})

describe('composer.css — altura razonable y contraste', () => {
  it('el placeholder usa --t2, no --t3 (era casi invisible)', () => {
    expect(composer).toMatch(/\.composer__area::placeholder\s*\{[^}]*color:\s*var\(--t2\)/)
    expect(composer).not.toMatch(/\.composer__area::placeholder\s*\{[^}]*var\(--t3\)/)
  })

  it('el bloque bloqueado no atenúa el botón de detener', () => {
    // Atenuar todo el bloque apagaba justamente el control activo.
    expect(composer).not.toMatch(/\.composer__envoltura\[data-bloqueado='true'\]\s*\{[^}]*opacity/)
    expect(composer).toMatch(/\[data-bloqueado='true'\] \.composer__area/)
  })

  it('los chips atenuados siguen siendo visibles', () => {
    const chip = /\.chip:disabled\s*\{([^}]*)\}/.exec(chat)?.[1] ?? ''
    const opacidad = Number(/opacity:\s*([\d.]+)/.exec(chip)?.[1] ?? 0)
    // 0.4 los volvía invisibles.
    expect(opacidad).toBeGreaterThanOrEqual(0.5)
  })
})

describe('colisión de hojas — la causa de las cajas', () => {
  it('events.css ya no existe', () => {
    expect(() => leer('src/styles/components/events.css')).toThrow()
  })

  it('index.css no la importa', () => {
    expect(indice).not.toMatch(/events\.css/)
  })

  it('ninguna otra hoja declara un .msg genérico', () => {
    // Un `.msg` suelto en otra hoja reintroduce el borde, el fondo y el
    // max-width:80% que hacían tarjetas cuadradas.
    const selectorMsg = /\.msg\s*\{/
    for (const [nombre, css] of [
      ['buttons.css', botones],
      ['forms.css', forms],
    ] as const) {
      expect(selectorMsg.test(css), `${nombre} declara un .msg`).toBe(false)
    }
  })
})