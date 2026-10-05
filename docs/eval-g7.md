# G7 — Evaluación baseline determinista

- Casos: 18
- Correctos: 18
- Requiere aclaración (inesperada): 0
- Incorrectos: 0
- Precisión: 100 %
- Latencia total: 3 ms (18 consultas)

| Query | Esperado | Resultado | Motivo |
|---|---|---|---|
| `huevo` | esperado=1 | OK | resuelto con alta confianza |
| `huevos` | esperado=1 | OK | resuelto con alta confianza |
| `HUEVO` | esperado=1 | OK | resuelto con alta confianza |
| `huevo ` | esperado=1 | OK | resuelto con alta confianza |
| `arroz` | esperado=2 | OK | resuelto con alta confianza |
| `ARROZ` | esperado=2 | OK | resuelto con alta confianza |
| `pollo` | esperado=3 | OK | resuelto con alta confianza |
| `pollo asado` | esperado=aclaración | ACLARACION | confianza insuficiente |
| `tortilla` | esperado=4 | OK | resuelto con alta confianza |
| `frijoles` | esperado=5 | OK | resuelto con alta confianza |
| `frijol` | esperado=aclaración | ACLARACION | confianza insuficiente |
| `aguacate` | esperado=6 | OK | resuelto con alta confianza |
| `aceite` | esperado=7 | OK | resuelto con alta confianza |
| `salsa` | esperado=8 | OK | resuelto con alta confianza |
| `agua` | esperado=9 | OK | resuelto con alta confianza |
| `refresco` | esperado=10 | OK | resuelto con alta confianza |
| `pizza` | esperado=aclaración | ACLARACION | alimento inexistente en catálogo |
| `pizza de pollo` | esperado=aclaración | ACLARACION | confianza insuficiente |


## Decisión pgvector

Baseline determinista suficiente para el catálogo actual (10+ alimentos). Comparación con pgvector/híbrido se realizará cuando existan embeddings disponibles (post-G8), con dataset ampliado y catálogo real.
