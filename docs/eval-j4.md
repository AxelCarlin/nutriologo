# Fase J4 — Benchmark de modelos E2E (mismo dataset)

Dataset congelado de J3 (`scripts/eval_j3_dataset.json`, n=12), mismo harness `scripts/eval_j3.py`, mismos prompts/configuración; solo cambia el modelo detrás de `IChatInterpreter` vía `OLLAMA__MODEL`.

## Resultados

| Modelo | Correcto | Aclaración correcta | Aclaración innecesaria | Falso positivo | Error | Latencia media |
|---|---|---|---|---|---|---|
| Qwen 2.5 7B Q4_K_M (baseline) | 1 | 7 | 3 | 0 | 1 | ~2.3 s |
| Llama 3.1 8B Q4_K_M | 1 | 8 | 3 | 0 | 0 | ~1.7 s |
| Gemma 2 9B Q4_K_M | 2 | 5 | 2 | 0 | 3 | ~2.6 s |

Evidencia por modelo: `docs/eval-j4-qwen.md`, `docs/eval-j4-llama31.md`, `docs/eval-j4-gemma2.md`.

## Interpretación

- **0 falsos positivos en los tres modelos**: los guards (validator + resolver + policy) convierten cualquier respuesta dudosa en aclaración/error, nunca en registro indebido. El criterio fail-closed se mantiene.
- **Qwen sigue siendo una elección válida y balanceada**: mismo número de aclaraciones innecesarias que Llama (3), 1 error, buen throughput.
- **Llama 3.1 8B** tiene la mejor tasa de aclaraciones correctas (8/12) y 0 errores — alternativa sólida si se prioriza estabilidad, pero comparte la debilidad de Qwen (3 aclaraciones innecesarias en cantidades formuladas distinto).
- **Gemma 2 9B** resuelve ligeramente mejor la extracción (2 correctos, 2 innecesarias) pero acumula 3 errores; no se recomienda como cambio.

## Decisión explícita

> **Qwen 2.5 7B Q4_K_M continúa seleccionado.** J4 demuestra que la decisión de G8 se mantiene bajo evaluación E2E rigurosa: ningún modelo supera significativamente a Qwen, los tres preservan el fail-closed, y Qwen equilibra calidad, latencia y estabilidad. Gemma no justifica el coste; Llama es candidato de respaldo.

La debilidad común (3 aclaraciones innecesarias ante cantidades no evidentes) se documenta como mejora de prompt/contrato, no como motivo de cambio de modelo.
