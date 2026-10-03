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

### Contradicciones / hallazgos

1. Se usa el tipo `vector` (línea ~880) sin `CREATE EXTENSION vector;` → el script fallará en PostgreSQL estándar. Requiere `pgvector`.
2. Sin índices secundarios: revisar FKs de alto uso en Fase B/C.
3. Dialecto PostgreSQL con `datetime` (alias de `timestamp`).

## A3 — Solución creada

- `Nutriologo.sln` con 4 src + 3 tests, referencias: Application→Domain, Infrastructure→Domain+Application, Api→Application+Infrastructure. Build: 0 errores.

## Decisiones

- .NET 10 (LTS), Node/Ollama diferidos a sus fases.
- Trabajo en `/home/axel/projects/nutriologo`.
- `NutriologoDB.sql` copiado sin modificaciones; las correcciones (pgvector) se propondrán como cambio explícito.
