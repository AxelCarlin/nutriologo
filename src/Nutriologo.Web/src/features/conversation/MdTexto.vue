<script setup lang="ts">
import { computed } from 'vue'
import { parsearMarkdown } from './markdown'

/**
 * Render del texto del asistente.
 *
 * NO usa `v-html` a propósito: el texto viene del modelo y se persiste en la
 * base, así que tratarlo como HTML permitiría inyectar etiquetas. Acá se pinta
 * con nodos de Vue, que escapan todo lo que no sea texto.
 */
const props = defineProps<{ texto: string }>()

const nodos = computed(() => parsearMarkdown(props.texto))
</script>

<template>
  <div class="md">
    <template v-for="(n, i) in nodos" :key="i">
      <ul v-if="n.tipo === 'lista' && !n.ordenada" class="md__lista">
        <li v-for="(item, j) in n.items" :key="j" class="md__item">
          <template v-for="(p, k) in item" :key="k">
            <strong v-if="p.tipo === 'fuerte'">{{ p.valor }}</strong>
            <em v-else-if="p.tipo === 'em'">{{ p.valor }}</em>
            <code v-else-if="p.tipo === 'codigo'">{{ p.valor }}</code>
            <template v-else>{{ p.valor }}</template>
          </template>
        </li>
      </ul>

      <ol v-else-if="n.tipo === 'lista'" class="md__lista md__lista--num">
        <li v-for="(item, j) in n.items" :key="j" class="md__item">
          <template v-for="(p, k) in item" :key="k">
            <strong v-if="p.tipo === 'fuerte'">{{ p.valor }}</strong>
            <em v-else-if="p.tipo === 'em'">{{ p.valor }}</em>
            <code v-else-if="p.tipo === 'codigo'">{{ p.valor }}</code>
            <template v-else>{{ p.valor }}</template>
          </template>
        </li>
      </ol>

      <p v-else class="md__parrafo">
        <template v-for="(p, k) in n.inline" :key="k">
          <strong v-if="p.tipo === 'fuerte'">{{ p.valor }}</strong>
          <em v-else-if="p.tipo === 'em'">{{ p.valor }}</em>
          <code v-else-if="p.tipo === 'codigo'">{{ p.valor }}</code>
          <template v-else>{{ p.valor }}</template>
        </template>
      </p>
    </template>
  </div>
</template>

<style>
/* El markdown hereda la tipografía del mensaje: misma fuente, mismo peso,
   mismo interlineado. El marcador es propio (`—`) en vez del punto del
   navegador, que desentona con el resto de la retícula. */
.md__parrafo {
  margin: 0;
  padding: 2px 0;
}

.md__parrafo + .md__parrafo,
.md__parrafo + .md__lista,
.md__lista + .md__parrafo,
.md__lista + .md__lista {
  margin-top: 8px;
}

.md__lista {
  margin: 0;
  padding: 0 0 0 18px;
  list-style: none;
}

.md__item {
  position: relative;
  padding: 0 0 0 2px;
  font-size: 15px;
  font-weight: 300;
  line-height: 1.55;
  color: var(--t1);
}

/* Viñeta propia: em dash, la misma retícula que el resto del sistema. */
.md__lista:not(.md__lista--num) > .md__item::before {
  content: '—';
  position: absolute;
  left: -18px;
  color: var(--t3);
}

/* La numerada usa las cifras del navegador: se alinean solas con `list-style`
   y así los números quedan en columna, que es lo que se busca al listar. */
.md__lista--num {
  list-style: decimal;
}

.md__lista--num > .md__item::marker {
  color: var(--t3);
  font-family: var(--nd-font-mono);
  font-size: 12px;
}

.md strong { font-weight: 500; color: var(--t1); }
.md em { font-style: italic; }

.md code {
  font-family: var(--nd-font-mono);
  font-size: 13px;
  color: var(--t2);
  background: var(--sf);
  border: 1px solid var(--bd);
  border-radius: 4px;
  padding: 1px 5px;
}
</style>