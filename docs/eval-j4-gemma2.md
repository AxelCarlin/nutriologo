# J3 — Evaluación end-to-end (Api + PostgreSQL + Ollama)

- Casos: 12
- Clasificación: {
  "correcto": 2,
  "aclaracion_innecesaria": 2,
  "error": 3,
  "aclaracion_correcta": 5
}
- Latencia media: 5.92s (max 13.54s)

| Entrada | Esperado | Obtenido | Clasificación | Latencia |
|---|---|---|---|---|
| `Comí dos huevos y dos tortillas.` | registrado | registrado | correcto | 13.54s |
| `Comí 150 gramos de arroz con pollo hervido.` | registrado | aclaracion | aclaracion_innecesaria | 7.08s |
| `Tomé un vaso de agua.` | registrado | aclaracion | aclaracion_innecesaria | 5.01s |
| `Comí pizza.` | aclaracion | error | error | 3.88s |
| `Comí unos tacos.` | aclaracion | error | error | 3.72s |
| `Comí huevos asados con salsa.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 6.98s |
| `Comí frijoles.` | aclaracion | aclaracion | aclaracion_correcta | 5.0s |
| `Comí dos refrescos.` | registrado | registrado | correcto | 5.07s |
| `Comí pollo teriyaki.` | aclaracion | aclaracion | aclaracion_correcta | 5.24s |
| `Agua.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 5.03s |
| `Comí aguacate con limón.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 6.87s |
| `Hoy tengo mucha hambre.` | aclaracion | error | error | 3.64s |

Clasificación: correcto / aclaracion_correcta / falso_positivo / aclaracion_innecesaria / rechazo_correcto / error.
El loose end PIEZA→g se mantiene como variable conocida, no se contaminó el dataset.
