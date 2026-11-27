# Nutriólogo

Asistente conversacional de nutrición para adultos: combina cálculo nutricional determinístico con un modelo de lenguaje local (Ollama) para interpretar comidas en lenguaje natural, responder dudas y guiar al usuario por la app, sin depender de servicios de IA de terceros.

> **Stack:** .NET 10 · Vue 3 + Vite · PostgreSQL 17 + pgvector · Ollama · Docker Compose · Cloudflare Tunnel
> **Estado:** MVP v1.0 operativo. Desplegado en `https://nexsmart.com.mx` (mismo origen para web y API).

---

## 1. Resumen

El proyecto resuelve un problema concreto: registrar comidas y entender el balance nutricional no debería obligar a cargar formularios con gramos y códigos. El usuario escribe o dice «comí un plato de arroz con pollo y una manzana» y el sistema:

1. interpreta la intención y extrae alimentos/cantidades con un LLM local;
2. normaliza los candidatos contra un catálogo determinístico (tablas de composición de alimentos);
3. guarda el registro en PostgreSQL y calcula calorías, macros y balance del día;
4. responde por **SSE** (streaming) y ofrece botones de redirección a las vistas correspondientes cuando hace falta confirmación humana.

La regla rectora es **fail-closed**: si el modelo no devuelve una forma válida, el validador normaliza lo que es seguro (`alimentos: null → []`) y el resto se rechaza en lugar de inventar datos nutricionales. El cálculo numérico nunca depende del LLM.

### Características principales

- Registro de comidas por chat, con aclaraciones cuando faltan cantidades.
- Catálogo de alimentos, unidades de medida y recetas/alimentos compuestos.
- Perfil, mediciones corporales y cálculo de objetivo calórico (BMR/TDEE con márgenes de seguridad).
- Panel de balance diario, consumo real vs. plan y comparación plan/consumo.
- Chat con streaming, historial persistido y redirecciones a vistas de la SPA.
- Autenticación JWT HMAC-SHA256, rate limiting y separación estricta de datos entre usuarios.
- Interfaz PWA con sistema de diseño propio («Nothing design»): tipografía editorial, rejilla, píldora deslizante y tema claro/oscuro.
- Backups/restore reproducibles y exposición pública mediante un único Cloudflare Tunnel.

---

## 2. Contexto y decisiones de diseño

| Decisión | Motivo |
| --- | --- |
| **Arquitectura limpia** (Domain → Application → Infrastructure → Api) | Las reglas nutricionales son verificables con tests unitarios y no deben conocer a EF Core, HTTP ni Ollama. |
| **LLM local con Ollama** | Privacidad de los datos del usuario y coste predecible; el modelo se usa solo para interpretación, nunca para cálculo. |
| **Mismo origen (`/api`)** | El frontend llama a `/api/...` y el túnel enruta por path: no hay CORS, ni dominios separados, ni tokens viajando a otro origin. |
| **`timestamp` sin zona horaria** | Decisión original del diseño: las fechas son «día local del usuario», no instantes UTC. |
| **pgvector** | Los embeddings de alimentos se guardan en la misma BD; no hace falta un vector store externo. |
| **Fail-closed** | Ante una salida inválida del modelo se prefiere no registrar nada antes que guardar un dato nutricional erróneo. |
| **Sin reverse proxy en el stack** | Cloudflare Tunnel termina TLS y enruta directamente a los puertos locales; Nginx solo sirve la SPA y hace de proxy `/api` interna. |

El repositorio conserva el registro de las fases A→K en `docs/`: bases deterministas, contratos de IA, evaluación de modelos, frontend, seguridad y despliegue.

---

## 3. Stack tecnológico

**Backend**
- .NET 10 (ASP.NET Core minimal APIs), Entity Framework Core + Npgsql.
- PostgreSQL 17 con extensión `vector` (imagen `pgvector/pgvector:pg17`).
- Ollama local (`qwen2.5:7b-instruct-q4_K_M` y `qwen2.5vl:7b-q4_K_M` por defecto).
- JWT propio (`Auth:Key`), rate limiter fijo de 100 req/min, OpenAPI en desarrollo.

**Frontend**
- Vue 3, TypeScript, Vite 8, Pinia, Vue Router 5, `lucide-vue-next`.
- Vitest + happy-dom/jsdom para tests de componentes y cliente HTTP.
- `vite-plugin-pwa` (service worker y manifest con cache-control explícito).
- Nginx en producción (SPA fallback + proxy `/api` + SSE sin buffering).

**Infraestructura**
- Docker Compose: `postgres`, `ollama`, `api`, `web`.
- Cloudflare Tunnel (`cloudflared`) como único punto de entrada público.
- Scripts bash para backup/restore y suite E2E de chat.

---

## 4. Árbol de directorios

```text
nutriologo/
├── docker-compose.yml          # Orquestación de postgres, ollama, api y web
├── Nutriologo.slnx             # Solución .NET (4 proyectos + 4 de tests)
├── NutriologoDB.sql            # Fuente de verdad del esquema y datos semilla
├── .env                        # Secretos locales (NO versionado, ver .gitignore)
├── .gitignore
├── test-chat-suite.sh          # Suite E2E de conversación con dos usuarios
├── seed-user-b.sh              # Siembra datos privados para pruebas de aislamiento
│
├── src/
│   ├── Nutriologo.Domain/      # CAPA 1 — reglas de negocio, sin dependencias externas
│   │   ├── Entities/           # Usuario, Alimento, Comida, Plan, Conversación…
│   │   ├── Catalog/            # Resolución de alimentos y unidades
│   │   ├── Nutrition/          # BMR/TDEE, macros, balances, conversiones
│   │   ├── Consumption/        # Seguimiento de consumo real vs. plan
│   │   ├── Conversations/      # Estados del chat (draft, turnos, intenciones)
│   │   ├── Plans/              # Planes de comida y Objective/objetivo
│   │   ├── Recipes/            # Recetas y alimentos compuestos
│   │   ├── Import/             # Importación de planes externos
│   │   ├── Feedback/           # Feedback del modelo como dataset de eval
│   │   └── Exceptions/, Common/
│   │
│   ├── Nutriologo.Application/ # CAPA 2 — casos de uso y contratos
│   │   ├── Abstractions/       # Interfaces (IChatInterpreter, repos, fuentes)
│   │   ├── AI/                 # Contratos de salida estructurada del LLM
│   │   ├── Common/             # Validadores y utilidades compartidas
│   │   ├── Contracts/          # DTOs de entrada/salida
│   │   ├── Meals/              # Casos de uso de registro de comidas
│   │   └── Plans/              # Casos de uso de planes y objetivo
│   │
│   ├── Nutriologo.Infrastructure/ # CAPA 3 — implementaciones técnicas
│   │   ├── Persistence/        # AppDbContext, mapeos EF Core, seed
│   │   ├── AI/                 # Adapter Ollama, intérprete, respondedor, visión
│   │   ├── Catalog/, Conversations/
│   │   └── DependencyInjection.cs
│   │
│   ├── Nutriologo.Api/         # CAPA 4 — HTTP
│   │   ├── Program.cs          # DI, JWT, rate limiting, OpenAPI, middleware
│   │   ├── Auth/               # /api/auth (registro, login, JwtIssuer, hasher)
│   │   ├── ChatEndpoints.cs    # /api/conversacion/* (SSE y redirecciones)
│   │   ├── NutricionEndpoints.cs # /api/* (perfil, comidas, balance, objetivo…)
│   │   ├── MealRegistrar.cs    # Orquestación de registro asistido
│   │   ├── Middleware/         # Manejo global de excepciones
│   │   ├── appsettings*.json
│   │   └── Dockerfile          # build multietapa (sdk → aspnet)
│   │
│   └── Nutriologo.Web/         # SPA Vue 3
│       ├── src/app/router/     # Rutas y guard de autenticación
│       ├── src/features/
│       │   ├── auth/           # LoginView
│       │   ├── conversation/   # Chat, SSE, markdown, redirecciones, tests
│       │   ├── meals/          # Registro de comida y balance
│       │   ├── nutrition/      # Inicio, objetivo, mediciones
│       │   └── profile/        # Perfil del usuario
│       ├── src/shared/
│       │   ├── api/            # Cliente fetch, token JWT, helpers
│       │   ├── components/     # Sidebar, campos, estados vacíos
│       │   ├── stores/         # Pinia (perfil)
│       │   └── skills/nothing-design/ # Tokens y reglas del sistema visual
│       ├── src/styles/         # tokens.css, base.css y componentes
│       ├── public/             # favicon, manifest, service worker
│       ├── tests/, *.test.ts   # Vitest
│       ├── vite.config.ts, vitest.config.ts, package.json
│       └── README.md           # Notas específicas del frontend
│
├── tests/                      # Proyectos de test paralelos a las capas
│   ├── Nutriologo.Domain.Tests/        # Reglas puras: Conversion, Nutrition, Feedback…
│   ├── Nutriologo.Application.Tests/   # Contratos de IA, arquitectura, planes
│   ├── Nutriologo.Infrastructure.Tests/# Persistencia, adapter Ollama, fallback
│   └── Nutriologo.Integration.Tests/   # API + BD real (chat, comidas, privacidad)
│
├── docs/                       # Decisiones, evaluaciones y operación
│   ├── cloudflared.md          # Exposición pública y enrutado del túnel
│   ├── cierre-v1.md            # Cierre del MVP y estado operativo
│   ├── fase-*.md               # Registro de las fases A–K
│   ├── eval-*.md               # Evaluaciones deterministas y E2E de modelos
│   ├── benchmark-*.md          # Benchmarks de modelos (texto y visión)
│   └── PowerAppsIntegration.md # Integración con PowerApps
│
├── deploy/
│   ├── web.Dockerfile          # Node → build → Nginx
│   └── nginx.conf              # SPA fallback, proxy /api, SSE, caché PWA
│
└── scripts/
    ├── backup.sh               # pg_dump → ./backups
    ├── restore.sh              # pg_restore en base temporal
    ├── eval_*.py               # Datasets y evaluación de modelos
    └── benchmark_*.py          # Benchmarks de inferencia
```

> `bin/`, `obj/`, `node_modules/`, `dist/`, `.env` y `appsettings.Development.json` están excluidos por `.gitignore`.

---

## 5. Arquitectura y flujo de una petición

```text
Navegador (SPA)
   │  GET /              → web (:3000)
   │  POST /api/…        → api (:8080) vía Nginx / Cloudflare Tunnel
   ▼
Cloudflare Tunnel  ── path /api/*, /health, /openapi* → :8080
                   └─ resto (*)                       → :3000
   ▼
ASP.NET Core (Api)
   ├─ JWT + rate limit + GlobalExceptionMiddleware
   ├─ Application (casos de uso / contratos)
   ├─ Domain (cálculo y reglas puras)
   └─ Infrastructure
        ├─ EF Core → PostgreSQL + pgvector (:5432)
        └─ Ollama HTTP → modelo local (:11434)
```

**Flujo del chat:** `POST /api/conversacion/mensaje|stream` → el intérprete clasifica intención y extrae candidatos → el validador normaliza la salida → `MealRegistrar` resuelve alimentos contra el catálogo → se persisten turnos y comida → la respuesta se emite por SSE con un `RedireccionDestino` opcional (`registrar`, `mediciones`, `objetivo`, `perfil`).

---

## 6. Requisitos

- **.NET SDK 10.0.x**
- **Docker** y **Docker Compose**
- **Node.js 20 LTS** (solo para desarrollo/build del frontend; en producción lo hace el Dockerfile)
- Opcional: **NVIDIA Container Toolkit** para acelerar Ollama con GPU (sin él corre en CPU, más lento)
- ~25 GB libres para los modelos de Ollama si se montan localmente

---

## 7. Puesta en marcha

```bash
git clone https://github.com/AxelCarlin/nutriologo.git
cd nutriologo

# 1. Crear secretos locales (NO los subas al repo)
cat > .env <<'EOF'
POSTGRES_PASSWORD=cambia-esta-clave
AUTH_KEY=cambia-esta-clave-larga-y-aleatoria
# Opcionales:
# POSTGRES_USER=postgres
# POSTGRES_DB=nutriologo
# Ollama_Model=qwen2.5:7b-instruct-q4_K_M
# Ollama_VisionModel=qwen2.5vl:7b-q4_K_M
EOF

# 2. Levantar el stack
docker compose up -d --build

# 3. Verificar
curl http://localhost:8080/health     # {"status":"ok"}
curl http://localhost:3000            # SPA
docker compose ps
```

Servicios resultantes:

| Servicio | Contenedor | Puerto host | Descripción |
| --- | --- | --- | --- |
| `postgres` | `nutriologo-db` | 5432 | PostgreSQL 17 + pgvector |
| `ollama` | `nutriologo-ollama` | 127.0.0.1:11434 | Modelos locales (solo loopback) |
| `api` | `nutriologo-api` | 8080 | API minimal |
| `web` | `nutriologo-web` | 3000 | SPA servida por Nginx |

### Variables de entorno

| Variable | Obligatoria | Uso |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | sí | Contraseña de la BD; se inyecta también en `ConnectionStrings__DefaultConnection`. |
| `AUTH_KEY` | sí | Clave HMAC-SHA256 para firmar los JWT. Cambiarla invalida los tokens emitidos. |
| `POSTGRES_USER` / `POSTGRES_DB` | no | Defaults: `postgres` / `nutriologo`. |
| `Ollama_Model` / `Ollama_VisionModel` | no | Modelos de texto y visión; ver `docs/benchmark-*.md`. |

> La API **falla en arranque** si no encuentra `Auth:Key`. `.env` y `appsettings.Development.json` nunca se versionan.

---

## 8. Base de datos

`NutriologoDB.sql` es la fuente de verdad: crea el esquema, la extensión `vector` y los datos semilla (catálogo de alimentos, unidades, etc.). El contenedor ejecuta la inicialización solo la primera vez; el volumen `nutriologo_pgdata` conserva los datos posteriores.

```bash
# Copiar el esquema a una BD ya levantada (si hace falta)
docker exec -i nutriologo-db psql -U postgres -d nutriologo < NutriologoDB.sql

# Backup y restauración
scripts/backup.sh ./backups
scripts/restore.sh ./backups/nutriologo_YYYYMMDD_HHMMSS.dump
```

La restauración crea una base **nueva** (`nutriologo_restore`) en lugar de sobrescribir la activa.

**Incidente conocido:** si el host se apaga de forma brusca puede quedar un `postmaster.pid` vacío y la BD entra en reinicio continuo; al estar caída, el alias DNS `postgres` desaparece y la API devuelve 500 en `/api/auth/*`. La solución es detener el contenedor, mover ese archivo del volumen y reiniciar.

---

## 9. API

Base: `http://localhost:8080` · en producción `https://nexsmart.com.mx` (mismo origen que la SPA).
Documentación OpenAPI disponible en desarrollo (`/openapi/v1.json`).

### Públicos

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/health` | Healthcheck (`{"status":"ok"}`). |
| `POST` | `/api/auth/registro` | `{nombre, correo, password}` → `{id, token}`. Mínimo 8 caracteres. |
| `POST` | `/api/auth/login` | `{correo, password}` → `{id, nombre, token}`. Credenciales inválidas → 401. |

### Autenticados (Bearer JWT)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/usuario`, `/api/perfil` | Datos del usuario autenticado. |
| `POST` | `/api/perfil`, `/api/mediciones` | Actualización de perfil y mediciones. |
| `GET` | `/api/mediciones` | Historial (filtros `tipo`, `desde`). |
| `GET` | `/api/alimentos`, `/api/unidades`, `/api/catalogos` | Catálogos de apoyo al registro. |
| `POST` / `GET` / `DELETE` | `/api/comidas`, `/api/comidas/{id}` | Registro, consulta por fecha y borrado. |
| `GET` | `/api/balance`, `/api/panel` | Balance día y panel agregado. |
| `POST` | `/api/objetivo/calcular` | BMR/TDEE/meta + evaluación de riesgo. |
| `POST` | `/api/conversacion/mensaje` | Turno de chat (JSON). |
| `POST` | `/api/conversacion/stream` | Turno de chat por SSE (streaming). |
| `GET` | `/api/conversaciones` | Historial de conversaciones. |

Todas las rutas de negocio/IA aplican la política `business` (100 peticiones/minuto; 429 al exceder) y los datos se filtran siempre por el `sub` del token.

---

## 10. Frontend

Vistas principales bajo `src/Nutriologo.Web/src/features/`:

- `auth/LoginView.vue` — acceso y registro.
- `nutrition/Inicio.vue`, `ObjetivoView.vue`, `MedicionesView.vue` — resumen, objetivo y mediciones.
- `meals/RegistroComidaView.vue`, `BalanceView.vue` — registro asistido y balance.
- `conversation/ConversacionView.vue` — chat con Markdown, SSE y botones de redirección.
- `profile/PerfilView.vue` — perfil del usuario.

El cliente HTTP (`shared/api/client.ts`) añade el `Authorization: Bearer <token>`, redirige a `/login` ante un 401 y usa `/api` como base (mismo origen). El sistema visual se documenta en `shared/skills/nothing-design/`.

```bash
cd src/Nutriologo.Web
npm install
npm run dev        # servidor de desarrollo (Vite)
npm run build      # vue-tsc + vite build → dist/
npm test           # Vitest
npm run typecheck
```

---

## 11. IA y validación

- `Infrastructure/AI/` contiene el adapter HTTP de Ollama, el intérprete de intenciones, el respondedor conversacional y el soporte de visión.
- Los modelos se eligen por configuración (`Ollama__BaseUrl`, `Ollama__Model`, `Ollama__VisionModel`).
- La salida del modelo se valida contra contratos fuertemente tipados antes de tocar la BD.
- `docs/eval-*.md` y `docs/benchmark-*.md` registran las evaluaciones (dataset determinista, E2E con BD real y comparación de modelos).

---

## 12. Tests

```bash
# Backend (solución completa)
dotnet test

# Proyectos individuales
dotnet test tests/Nutriologo.Domain.Tests
dotnet test tests/Nutriologo.Application.Tests
dotnet test tests/Nutriologo.Infrastructure.Tests
dotnet test tests/Nutriologo.Integration.Tests

# Frontend
cd src/Nutriologo.Web && npm test

# Suite E2E de chat (requiere dos tokens y el stack levantado)
./test-chat-suite.sh <TOKEN_USUARIO_A> <TOKEN_USUARIO_B> [BASE_URL]
```

Los tests de integración usan PostgreSQL real; los de infraestructura que involucran LLM están aislados y tienen fallback determinista para no depender de Ollama en CI.

---

## 13. Despliegue

`cloudflared` corre como servicio systemd en el host y usa un único túnel para varios proyectos. Enrutado correspondiente a este repositorio (ver `docs/cloudflared.md`):

| Ruta pública | Destino |
| --- | --- |
| `nexsmart.com.mx/api/*` | `http://localhost:8080` |
| `nexsmart.com.mx/health` | `http://localhost:8080` |
| `nexsmart.com.mx/openapi.*` | `http://localhost:8080` |
| `nexsmart.com.mx/*` | `http://localhost:3000` |

Sin CORS: frontend y API comparten origen. Si el host cambia, se reinstala `cloudflared` reutilizando el certificado y las credenciales del túnel.

**Diagnóstico rápido:** una página 502 de Cloudflare significa origen inalcanzable (contenedor caído o túnel cortado); revisar `journalctl -u cloudflared` y `docker ps`. Un 500 con `Name or service not known` en los logs de la API significa que la BD no está levantada.

---

## 14. Seguridad

- Contraseñas hasheadas (`Auth/PasswordHasher.cs`), nunca en claro.
- JWT HMAC-SHA256 con emisor/audiencia validados y `ClockSkew` de 2 minutos.
- Rate limiting por IP (100 req/min) en rutas públicas y privadas.
- Secretos solo por variables de entorno; `.env` y `appsettings.Development.json` en `.gitignore`.
- Ollama publicado únicamente en `127.0.0.1`; el contenedor Db no necesita exponerse a la LAN en producción.
- Aislamiento de datos: cada consulta filtra por el usuario del token (ver tests de privacidad).
- Respuestas del modelo tratadas como datos no confiables (validación fail-closed).

---

## 15. Documentación del proyecto

| Documento | Contenido |
| --- | --- |
| `docs/fase-a.md` … `docs/fase-k.md` | Registro cronológico de decisiones e implementación por fases. |
| `docs/cierre-v1.md` | Cierre del MVP, verificación y estado operativo. |
| `docs/cloudflared.md` | Exposición pública, rutas y verificaciones. |
| `docs/eval-g7.md`, `docs/eval-j3.md`, `docs/eval-j4*.md` | Evaluaciones de modelos y datasets. |
| `docs/benchmark-g8*.md` | Benchmarks de inferencia y visión. |
| `docs/PowerAppsIntegration.md` | Uso de la API desde PowerApps. |

---

## 16. Estado y próximos pasos

**Hecho:** MVP funcional, tests por capa, E2E reales, despliegue operativo, evaluación de modelos y operación documentada.

**Pendiente / mejoras naturales:**
- Programar backups (hoy los scripts son manuales).
- Añadir observabilidad centralizada y alertas de reinicio de contenedores.
- Ampliar la suite de regresión del chat y el dataset de evaluación.
- Opcional: aceleración GPU de Ollama y ajuste de modelos según los benchmarks.

---

## Autor

Alex L. ([@AxelCarlin](https://github.com/AxelCarlin)).
