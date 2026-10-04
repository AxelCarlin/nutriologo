# G8.10–G8.11 — Benchmark y adapter de visión

## Condiciones

- Modelos: `qwen2.5vl:7b-q4_K_M` (6.0 GB), `llava:7b` (4.7 GB).
- Hardware: GTX 1070 8 GB, driver 550.163.01, Ollama 0.35.1.
- Dataset: 3 imágenes sintéticas controladas (`/tmp/opencode/img_*.png`), mismas condiciones de prompt/format que texto. `scripts/benchmark_g8_vision.py`, resultados en `docs/benchmark-g8-vision.json`.

## Resultados (smoke, n=3 por modelo)

| Modelo | JSON parseable | Conforme al esquema | Errores | Latencia | tokens/s | VRAM |
|---|---|---|---|---|---|---|
| qwen2.5vl:7b Q4_K_M | 3/3 | ❌ devuelve `{}` o descripciones sin schema | 0 | 23.4 s | ~136 | ~6.5 GB |
| llava:7b | 2/3 | ❌ devuelve `{"image":..., "description":...}` (metadata inventada) | 1 HTTP 500 | 9.3 s | ~73 | ~5.2 GB |

## Conclusión

> **Ninguno de los dos modelos de visión probados produce consistentemente el `VisionAnalysis` estructurado esperado en este smoke test zero-shot.** Ambos caben en VRAM (5–6.5 GB), pero la conformidad de esquema falla. El guard `AiGuardException` del adapter lo convierte en error honesto en lugar de persistir basura.

Implicaciones:
- El pipeline `Imagen → IVisionAnalyzer → resolver → aclaración` ya está implementado (`OllamaVisionAnalyzer` con `StructuredOutputValidator`), pero **la calidad de extracción de visión no está demostrada**; se requiere un dataset de imágenes real etiquetado (Fase J3) y prompt engineering antes de usarlo en producción.
- Texto queda cerrado (G8.1–G8.9). Visión queda implementada-como-contrato, no validada funcionalmente.

## Arquitectura

`IVisionAnalyzer → OllamaVisionAnalyzer` en Infrastructure; Domain/Application intactos. DI `Ollama:VisionModel` configurado (`qwen2.5vl:7b-q4_K_M`).
