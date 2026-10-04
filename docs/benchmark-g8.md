# G8.4–G8.6 — Benchmark de modelos

## Condiciones comparables

- Hardware: GTX 1070 (8 GB VRAM), driver 550.163.01, CUDA 12.4, ~15 GB RAM, Ollama 0.35.1.
- Mismo dataset (6 casos en español: comidas simples, ambiguas, preparación, ambigüedad, inexistente).
- Mismo prompt de sistema, `format:"json"`, `temperature:0`, `num_ctx:4096`, streaming off.
- `scripts/benchmark_g8.py` reproducible; resultados en `docs/benchmark-g8.json`.

## Candidatos y variante usada

| Modelo | Tag usado | Tamaño | Cuantización | VRAM usada | Latencia media |
|---|---|---|---|---|---|
| Qwen 2.5 7B Instruct | `qwen2.5:7b-instruct-q4_K_M` | 4.7 GB | Q4_K_M | ~4.8 GB | ~2.0 s/caso |
| Llama 3.1 8B Instruct | `llama3.1:8b-instruct-q4_K_M` | 4.9 GB | Q4_K_M | ~5.3 GB | ~2.3 s/caso |
| Gemma 2 9B Instruct | `gemma2:9b-instruct-q4_K_M` | 5.8 GB | Q4_K_M | ~7.2 GB | ~3.5 s/caso |

Visión (separados, no benchmarked todavía): `llava:7b`, `qwen2.5vl:7b`.

## Resultados (n=6 por modelo)

| Modelo | JSON válido | Alimento | Cantidad | Unidad | Preparación | Aclaración | Latencia total | tokens/s | VRAM |
|---|---|---|---|---|---|---|---|---|---|
| Qwen2.5 7B Q4_K_M | 6/6 | 4/6 | 6/6 | 6/6 | 6/6 | 5/6 | 12.3 s | ~207 | ~4.8 GB |
| Llama 3.1 8B Q4_K_M | 5/6 | 4/6 | 5/6 | 5/6 | 5/6 | 4/6 | 13.9 s + 1 timeout | ~163 | ~5.3 GB |
| Gemma 2 9B Q4_K_M | 6/6 | 5/6 | 6/6 | 6/6 | 6/6 | 4/6 | 21.1 s | ~144 | ~7.2 GB |

Notas:
- Qwen falla identificación en "huevos con aceite" (no lista aceite) y en pizza (lista "pizza" pese a catálogo cerrado); Gemma resuelve aceite correctamente y también falla en pizza como "acierto" esperado de aclaración (pidió aclaración, food_ok=false estricto).
- Llama tuvo 1 timeout y un error de JSON (5/6).

## Recomendación reproducible

> **`qwen2.5:7b-instruct-q4_K_M` es el candidato recomendado para el hardware actual**: 6/6 JSON válido, ~207 tokens/s, ~4.8 GB VRAM, sin timeouts. **Gemma2 9B Q4** queda como alternativa de mayor calidad de extracción (5/6 alimentos) a cambio de ~50% más latencia y ~7.2 GB VRAM. El error sistemático en "alimento fuera de catálogo debe pedir aclaración" es una mejora pendiente del prompt/guard, no del modelo.

## GPU

Verificado: durante la inferencia `nvidia-smi` muestra VRAM 4.8–7.2 GB según modelo y GPU-Util > 0 — la aceleración GPU está activa.

## Arquitectura

Ollama permanece detrás de `IChatInterpreter`/`IVisionAnalyzer`/`IFoodCandidateResolver`/`IClarificationGenerator`. Domain y Application no modificados. No se persisten resultados experimentales como producción.
