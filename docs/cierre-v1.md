# Nutriólogo MVP v1.0 — Cierre

Fecha: 2026-10-04. Tag: `v1.0.0-mvp`. Secuencia: I1–I5 → J1 → J3 → J4 → J5 → K.

## Inventario

- API .NET 10 (`src/Nutriologo.Api`), Domain / Application / Infrastructure, Web Vue 3 + Vite + PWA.
- Endpoints: perfil, mediciones, objetivo (BMR/TDEE/meta + riesgo), alimentos/recetas/planes, `POST /api/comidas`, `GET /api/balance`, `POST /api/conversacion/mensaje` (texto; visión experimental), `/health`, rate limiting `business` (100 req/min, 429 verificado).
- Despliegue: `docker-compose.yml` (postgres+pgvector, ollama, api, web nginx). Sin reverse proxy en el stack; exposición pública pendiente vía dominio + cloudflared.

## Evidencia de tests (cierre)

- `dotnet test`: Domain 73/73, Application 43/43, Infrastructure 3/3, Integration 7/7 → 126/126.
- J3 dataset 12 casos congelado; J4 benchmark Qwen/Llama/Gemma → Qwen seleccionado, 0 falsos positivos.
- K6 repetido en cierre contra stack composeado (api:8080, web:3000): catálogo, objetivo, comidas, balance (798.02), chat fail-closed (plátano → aclaración, sin persistir), restart con persistencia, backup + restore (Alimento=10).

## Arquitectura final

Web → Api :8080 → PostgreSQL+pgvector :5432 / Ollama :11434. El LLM propone y extrae; `RegisterMealValidator` + `MealRegistrar` validan, calculan y deciden. Nada del LLM persiste sin validación determinista.

## Secretos

Ningún secreto versionado: `.env` y `appsettings.Development.json` en `.gitignore`; tests leen `ConnectionStrings__DefaultConnection` (placeholder por defecto). Verificado con grep en `src/`, `tests/`, `docs/`, `*.yml`, `*.json`, `*.md`.

## Deuda técnica explícita (v1.1+, no bloquea MVP)

1. Autenticación real (hoy `idUsuario` en body).
2. Tests BOLA/IDOR con dos usuarios autenticados.
3. Validación estricta de uploads (sin endpoint aún).
4. Rate limiting particionado por IP/usuario (hoy global fijo).
5. `PIEZA → gramos` usa supuesto base-G (documentado).
6. Visión experimental (ADR 005).
7. Ollama en contenedor corre en CPU; GPU requiere nvidia-container-toolkit.

## Estado de producción

MVP implementado, evaluado, desplegable y reproducible. Operación pendiente: dominio + cloudflared, `POSTGRES_PASSWORD`/`Ollama__*` por entorno, backups programados (`scripts/backup.sh`).
