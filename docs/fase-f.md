# Fase F — Planes y consumo real

## Separación

PLANIFICADO: `PlanAlimenticio` → `DiaPlanAlimenticio` → `ComidaPlan` → `AlimentoComidaPlan`.
CONSUMIDO: `RegistroComida` → `AlimentoRegistroComida`.
`BalanceCalorico` deriva de ambos conceptos sin acoplarlos (objetivo nullable).

## Reglas de dominio

1. Plan = intención; Registro = evento real. No FK de consumo → plan (regla 3).
2. Historial inmutable en contenido: `RegistroComida`/`AlimentoRegistroComida` no exponen setters públicos (test `HistorialEsInmutable_PorDisenio`). Corrección = nuevo registro + Estatus/auditoría.
3. `BalanceService.Calcular` es determinista: consumidas − objetivo; sin LLM.
4. Cantidades vía `ConversionService`/`NutricionScaling` existentes; sin duplicar fórmulas.

## Validación

- Build: 0 errores. Tests: Domain 55/55, Application 4/4, Infrastructure 2/2 (incluye insert/rollback real contra PostgreSQL).
- `NutriologoDB.sql` sin modificar (git diff vacío). Solo `scripts/seed-fase-d.sql` ampliado con `TipoPlanAlimenticio`, `OrigenPlanAlimenticio`, `TipoComida`.
- Converter global DateTime UTC → Unspecified para columnas `timestamp` (decisión documentada en Fase A).

## Riesgos pendientes

- Seed de catálogos de condiciones fisiológicas, medición corporal, etc.: pendiente hasta fases que los usen.
