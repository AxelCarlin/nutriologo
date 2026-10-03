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
