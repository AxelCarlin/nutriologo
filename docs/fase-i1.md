# Fase I1 — Fundación del frontend

- `src/Nutriologo.Web`: Vue 3 + TypeScript + Vite + Pinia + Vue Router + PWA (vite-plugin-pwa, workbox, manifest).
- Estructura por features (`auth`, `profile`, `nutrition`, `foods`, `plans`, `meals`, `conversation`, `images`, `progress`) + `app/` (router, stores, providers) + `shared/` (components, composables, types, utils, api).
- Límite arquitectónico: `shared/api/client.ts` centraliza HTTP; no hay fórmulas ni lógica de dominio en Vue. El frontend **representa** resultados del backend.
- PWA: service worker generado en build (6 entradas precache).
- Scripts: `dev`, `build`, `preview`, `test` (vitest), `typecheck` (vue-tsc).
- Verificado: `npm run build` OK (dist + sw.js), `npx vitest run` 1/1, `npm run typecheck` OK.
- ESLint instalado pero sin configurar todavía (pendiente).
- Chat/orb/formulario/imagen comparten el mismo boundary: API client → ASP.NET Core → Application → Domain.
