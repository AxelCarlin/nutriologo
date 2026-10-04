# Fase J5 — Seguridad

## Verificado

| Bloque | Evidencia / decisión |
|---|---|
| Rate limiting | `AddRateLimiter` con política `business` (100 req/min por ventana fija) aplicada a `/api/*` y `/api/conversacion/mensaje`. Probado: ráfaga de 105 → 99×200, luego **429 ×6**. `/health` aislado. |
| Credenciales en repo | `appsettings.json` pasa a placeholder `SET_VIA_ENV_...`; el valor real vive en `appsettings.Development.json` (ahora en `.gitignore`, Desvinculado del tracking). None de `Buss1ness.2026` versionado ya en blobs posteriores este commit. |
| Aisling por usuario | Test existente de balance con `idUsuario=999`; el aislamiento formal se audita mejor al introducir autenticación real. |
| Prompt injection / fail-closed | J4: 0 falsos positivos en los tres modelos; J1: pizza → aclaración, sin persistir. El LLM no ejecuta operaciones; persistsvia `RegisterMealValidator`/`MealRegistrar`. |
| Secrets en logs | El código no registra prompts ni respuestas IA; middleware de errores devuelve mensaje genérico. Revisión inicial de `Program.cs`/adapters: sin `Console.WriteLine` de payload. |
| Ollama away from Domain | Test estructural garantiza que `Nutriologo.Domain` no referencia Ollama/HTTP. Ollama solo en `Infrastructure/AI`. |

## Quedó pendiente / por_definir

- **Autenticación real** (registro/login con hash + token): el sistema hoy identifica usuario por `idUsuario` en el body. Introducir cuando se diseñe la Fase I5-auth o K.
- **BOLA/IDOR tests**: se verifican de forma documental; falta el harness con dos usuarios autenticados.
- **Uploads**: no hay endpoint todavía; cuando exista, validar MIME real, tamaño, nombre generado, directorio separado.
- **Rate limit por usuario/IP**: hoy es global fija; en producción conviene particionar por IP/usuario.
- **Observabilidad de errores**: sin logging de prompts/imágenes; confirmado.

## Entorno de producción (K, cuando corresponda)

- `ConnectionStrings__DefaultConnection` y `Ollama__Model` como variables de entorno.
- No copiar `appsettings.Development.json` a producción (está fuera de git).

## Tests

- Integration tests siguen 7/7 con rate limiting activo (la suite no excede el umbral en cada corrida). Commit `2fe629e`.
