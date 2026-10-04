# Exposición pública — Cloudflare Tunnel (nexsmart.com.mx)

## Decisión

Sin reverse proxy en el stack. Un único túnel `ab707fc3-…` expone el dominioapex con enrutado por path (mismo origen → sin CORS):

| Ruta | Destino local |
|---|---|
| `nexsmart.com.mx/api/*` | `http://localhost:8080` (Api) |
| `nexsmart.com.mx/health` | `http://localhost:8080` |
| `nexsmart.com.mx/openapi.*` | `http://localhost:8080` |
| `nexsmart.com.mx/*` | `http://localhost:3000` (frontend) |

El frontend usa `VITE_API_URL ?? '/api'` (mismo origen), por lo que el buildactual funciona sin reconstruir.

## Archivos

- `~/.cloudflared/config.yml` (fuera del repo: contiene ID del túnel;permisos 0600). Servicio systemd `cloudflared.service` activo yhabilitado.
- DNS: `nexsmart.com.mx` resuelve a Cloudflare (proxied); el túnel daun `502` previo quedó corregido al apuntar a :3000/:8080.

## Verificación pública (2026-10-04)

- `GET https://nexsmart.com.mx/` → 200 (frontend).
- `GET https://nexsmart.com.mx/health` → `{"status":"ok"}`.
- `GET https://nexsmart.com.mx/api/alimentos` → catálogo.
- `POST https://nexsmart.com.mx/api/objetivo/calcular` → BMR/TDEE/meta +evaluación (1843.75/2857.81/2357.81).

## Notas operativas

- El túnel corre en el host de despliegue; si el host cambia, reinstalar`cloudflared` y reutilizar `cert.pem` + credenciales del túnel.
- Rate limiting `business` (100 req/min) aplica también al tráfico público.
- Backups y observabilidad según `docs/fase-k.md`.
- **502 con página de Cloudflare** = origen inalcanzable (contenedor reiniciándoseo túnel caído), no error del modelo. Ver `journalctl -u cloudflared` y`docker ps`. Ocurrido el 2026-10-04 durante un rebuild de `web`.
- **502 JSON del Api** (`IA no disponible o salida inválida`) = el modelo devolvióforma inesperada; el validador la normaliza cuando es seguro (`alimentos:null`→ `[]`) y lo demás sigue fail-closed.
- Healthchecks de `api`/`ollama` son chequeos TCP con `bash` (las imágenes notraen `curl`).
