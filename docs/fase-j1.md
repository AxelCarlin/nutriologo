# Fase J1 — E2E reales

## Alcance verificado (tests/Nutriologo.Integration.Tests, WebApplicationFactory + PostgreSQL real + Ollama real)

| Caso | Resultado |
|---|---|
| Objetivo feliz (BMR/TDEE/meta + evaluación de riesgo) | ✅ 7/7 tests pasan |
| Objetivo con menor de edad → 400 | ✅ |
| Registro por formulario + balance + aislamiento por usuario | ✅ |
| Comida con alimento inexistente → 400 sin persistir | ✅ |
| Chat "dos huevos y dos tortillas" → registrado + balance | ✅ |
| Chat "Comí pizza" → aclaración + cero persistencia | ✅ |

- Build backend 0 errores. Todos pasan en ~6 s.
- Visión: el test correcto sigue siendo fail-closed (`AiGuardException` → no persistencia), ya cubierto a nivel de adapter/validator; no se marca como E2E exitoso porque G8.10 demostró que los modelos actuales no cumplen el contrato de forma consistente.
- Datos de smoke eliminados de la DB de desarrollo.

## Limitación conocida (regresión explícita, no escondida)

`E2E: 2 huevos + 2 tortillas → balance` produce un valor conocido bajo la conversión actual de unidades (Cantidad en unidad base G); el puente PIEZA→gramos en `PorcionAlimento` sigue pendiente como mejora independiente.
