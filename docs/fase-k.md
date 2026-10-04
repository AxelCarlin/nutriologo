# Fase K — Despliegue/Producto operativo

## K1–K3 Docker Compose + persistencia + configuración

- `docker-compose.yml`: `postgres` (pgvector/pgvector:pg17, volumen `pgdata`, healthcheck), `ollama` (volumen `/usr/share/ollama/.ollama`), `api` (build desde `src/Nutriologo.Api/Dockerfile`, env-based config, healthcheck), `web` (nginx sirviendo build estático), `caddy` (reverse proxy y estático).
- Secretos por variables de entorno (`.env` gitignored): ningún secreto en git tras J5.
- Verificado: `docker compose up -d --build` levanta todo, `http://localhost/health` responde, `/api/alimentos` devuelve catálogo.

## K2 persistencia

- `docker compose restart` mantiene tablas (78), alimentos (10) y registros de comida (balance cambia de 527.46 → 530.56 tras un registro por chat y reinicio).
- Volumen `pgdata` y volumen de modelos de Ollama montado desde `/usr/share/ollama/.ollama`.

## K4 Backups/restore

- `scripts/backup.sh` genera dump custom de `nutriologo`.
- `scripts/restore.sh` probado: crea `nutriologo_restore` con 10 alimentos restaurados.
- Nota: tras el cambio de imagen de PostgreSQL se refrescó la versión de colación (`ALTER DATABASE ... REFRESH COLLATION VERSION`) — queda documentado.

## K5 Observabilidad mínima

- Healthchecks en Compose para postgres/ollama/api; Caddy expone `/health`.
- Logs de estructura estándar del Api; middleware de errores no expone internals.

## K6 Smoke test del despliegue (todo a través de Caddy)

- Frontend `curl /` → HTML estático.
- `/health` → `{"status":"ok"}`.
- `/api/objetivo/calcular` → BMR/TDEE/meta y evaluación de riesgo.
- `/api/comidas` + `/api/balance` → persistencia y balance.
- `/api/conversacion/mensaje` → `tipo: registrado` (Ollama en contenedor, CPU — sin GPU toolkit instalada).
- Persistencia tras `docker compose restart`.
- Backup y restore verificados.

## Limitaciones conocidas (no scope de K)

- Ollama dentro de Docker no usa GPU hasta instalar `nvidia-container-toolkit` (línea comentada en compose).
- Autenticación real, uploads, BOLA/IDOR y rate limit por usuario quedan como deuda técnica documentada en J5.
- PIEZA→g en calorías derivadas sigue como mejora independiente.
