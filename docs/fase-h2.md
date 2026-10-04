# Fase H2 — Importación de planes

## Regla estricta

La IA propone un candidato estructurado (`MealPlanCandidate`); **no persiste**. El plan solo se materializa como `PlanAlimenticio → DiaPlanAlimenticio → ComidaPlan → AlimentoComidaPlan` (Fase F) tras validación determinista.

## Pipeline

```
Entrada (Texto/Imagen/Formulario)
  → IA (StructuredOutputValidator)
  → MealPlanCandidate
  → ValidateMealPlanCandidateUseCase
      ├── nombre no vacío
      ├── ≥1 item
      ├── cantidades positivas
      └── cada alimento resuelto únicamente si DeterministicFoodResolver lo resuelve con alta confianza
  → válido → PlanAlimenticio ; inválido/ambiguo/desconocido → aclaración/rechazo, NUNCA persistencia parcial
```

## Reutilización

`StructuredOutputValidator`, `DeterministicFoodResolver`, `ClarificationPolicy` — sin duplicar lógica nutricional. `ImportacionPlan` representa el proceso (entrada original, `InterpretacionJson`, resultado, confianza), no es un segundo plan.

## Tests

37/37 Application (6 casos: éxito, sin nombre, sin items, cantidad inválida, alimento desconocido → aclaración sin persistir, ambiguo). Build 0 errores.
