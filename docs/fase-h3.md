# Fase H3 — Comparación plan vs consumo

- `PlanConsumptionComparisonService` (Application, puro): empareja por fecha + tipo de comida + IdAlimento resuelto; produce `ComparacionPlanConsumo` con estado (`Coincidencia`, `CantidadDiferente`, `PlanificadoSinConsumo`, `ConsumidoSinPlan`) y diferencias de cantidad/calorías cuando ambas existen.
- No fusiona ni sobrescribe: plan y consumo permanecen separados; el resultado es un DTO calculado, no persistido.
- Alimentos equivalentes pero no idénticos se resuelven por IdAlimento (resolver de Fase G3), no por texto.
- Tests: 43/43 Application (coincidencia, cantidad diferente, plan sin consumo, consumo sin plan, múltiples alimentos/días, no-mutación de fuentes). Commit `c425ca9`.

## Fase H cerrada

- H1 Feedback IA (dataset de evaluación) ✅
- H2 Importación de planes (IA propone, dominio valida) ✅
- H3 Plan vs consumo ✅
