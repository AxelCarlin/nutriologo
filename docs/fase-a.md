# Fase A — Preparación

## A1 — Entorno (2026-10-03)

| Herramienta | Estado |
|---|---|
| SO | Debian 13 (trixie) |
| .NET SDK | 10.0.401 (`~/.dotnet`) |
| Node.js / npm / pnpm | No instalado (Fase I) |
| PostgreSQL cliente (psql) | No en host; vía Docker |
| Docker | 29.8.2, servicio activo |
| Docker Compose | v5.6.0 |
| Git | 2.47.3 |
| Ollama | No instalado (Fase G) |
| GPU / nvidia-smi | No visible en host (evaluar Fase G) |
| RAM | ~14 GiB, 20 vCPUs |

## A2 — Inventario de NutriologoDB.sql

- 78 tablas (`CREATE TABLE`).
- 273 constraints FK vía `ALTER TABLE ... ADD FOREIGN KEY ... DEFERRABLE INITIALLY IMMEDIATE`.
- Auditoría uniforme: `FechaCreacion`, `FechaModificacion`, `Estatus`, `IdUsuarioCrea`, `IdUsuarioModifica` en todas las tablas.
- Tipos: `int`/`smallint`/`decimal`/`varchar`/`text`/`datetime`, `jsonb` (análisis de IA, bounding boxes, interpretación), `vector` (embeddings de predicciones).
- 0 `INSERT`, 0 índices secundarios explícitos, 0 `CREATE EXTENSION`.

### Contradicciones / hallazgos (resueltos)

1. ~~`vector` sin `CREATE EXTENSION`~~ → resuelto: imagen `pgvector/pgvector:pg17` + `CREATE EXTENSION IF NOT EXISTS vector;` al inicio del script.
2. ~~`datetime` no existe en PostgreSQL~~ → resuelto: reemplazo global `datetime` → `timestamp` (188 ocurrencias), sin cambiar nombres ni relaciones. Verificado: carga completa desde base vacía (78 tablas, 273 FKs, columna `AlimentoEmbedding.Vector` tipo `vector` presente).
3. Warning de collation (2.41 vs 2.36): documentado, no es error del esquema; resuelto con `ALTER DATABASE ... REFRESH COLLATION VERSION`.
4. Sin índices secundarios: revisar FKs de alto uso en Fase B/C.

## A3 — Solución creada

- `Nutriologo.sln` con 4 src + 3 tests, referencias: Application→Domain, Infrastructure→Domain+Application, Api→Application+Infrastructure. Build: 0 errores.

## Decisiones

- .NET 10 (LTS), Node/Ollama diferidos a sus fases.
- Trabajo en `/home/axel/projects/nutriologo`.
- `NutriologoDB.sql` copiado sin modificaciones; las correcciones (pgvector) se propondrán como cambio explícito.
