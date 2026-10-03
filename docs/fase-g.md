# Fase G1 — Contratos de IA

## Contratos (Application, sin Ollama)

- `IChatInterpreter` → `AiResult<ChatInterpretation>` (intenciones: registrar comida, medición, objetivo, balance, desconocida).
- `IVisionAnalyzer` → `AiResult<VisionAnalysis>` (confianza global + candidatos + raw JSON).
- `IFoodCandidateResolver` → `FoodResolution`.
- `IClarificationGenerator` → `ClarificationDecision`.

## Salida estructurada

`FoodCandidate(nombre, cantidad?, unidad?, preparación?, confianza, incertidumbres)`.
`AiResult<T>` persiste modelo, versión, duración y raw JSON (alineado con `AnalisisImagen`/`PrediccionAlimento`).

## Confianza (determinista)

`ConfidenceClassifier`: Alta ≥ 0.8, Media ≥ 0.5, Baja < 0.5; cualquier incertidumbre declarada → `RequiereAclaracion`. Sin colores.

## Política de aclaración

`ClarificationPolicy`: pregunta solo cuando la incertidumbre cambia materialmente calorías/alimento/cantidad/preparación (intencion desconocida, sin candidatos, confianza < 0.5 o incertidumbre declarada).

## Reglas

- El LLM propone; Domain/Application valida y calcula. `Nutriologo.Domain` sigue sin dependencias de IA/Infra.
- Tests: 55 Domain, 15 Application (incluye classifier, policy, rangos inválidos), 2 Infrastructure; build 0 errores.

## G3 — Resolución determinista de alimentos

`DeterministicFoodResolver` (Domain, puro, sin embeddings):

1. Normalización: minúsculas, sin acentos, espacios colapsados, singularización simple.
2. Puntuación: exacta 1.0 / normalizada-singular 0.95 / sinónimo 0.9 / parcial 0.65.
3. Contexto: preparación/cocina coincidente suma +0.15; contradictoria resta −0.25.
4. Decisión: candidato único con score ≥ 0.85 → resuelto; si no, múltiples candidatos + `RequiereAclaracion`.
5. Nunca crea entidades: inexistente/ambiguo → aclaración; catálogo vacío → aclaración.
6. Sin fuzzy matching con dependencias externas ni pgvector hasta medir G7.

Tests: 67/67 Domain (incluye plantillas de todos los casos solicitados). Commit `7940c81`.

## G4/G5 — Aclaraciones

`ClarificationGenerator` (Application) implementa `IClarificationGenerator` como adaptador: prioriza (preparación > cantidad > incertidumbre declarada) y redacta la pregunta; **no decide** si preguntar (`ClarificationPolicy` ya lo hace). Tests de caso de uso: alimento/cantidad/preparación/confianza baja/información suficiente/múltiples incertidumbres.

## G6 — Visión (esqueleto sin Ollama)

`AnalyzeMealImageUseCase`: `IVisionAnalyzer` → `VisionAnalysis` → `DeterministicFoodResolver` (mismo resolver de G3). Trabaja con adapter falso en tests; no produce calorías ni "verdad nutricional" desde la visión (test `Pipeline_Vision_Resolver_SinPersistirVerdad`). Persistencia real vía entidades existentes (`Imagen`, `AnalisisImagen`, `PrediccionAlimento`, `EstadoProcesamiento`, `AlimentoEmbedding`) en fases posteriores.

## G7 — Evaluación comparativa

Baseline determinista sobre el catálogo seed: 18/18 correctos, 0 falsos positivos, ~2 ms (`docs/eval-g7.md`). Conclusión: matching determinista suficiente para el catálogo actual; la comparación con pgvector/híbrido se difiere a post-G8 con embeddings disponibles y catálogo real.
