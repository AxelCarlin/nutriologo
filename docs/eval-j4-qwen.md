# J3 — Evaluación end-to-end (Api + PostgreSQL + Ollama)

- Casos: 12
- Clasificación: {
  "correcto": 1,
  "aclaracion_innecesaria": 3,
  "aclaracion_correcta": 7,
  "error": 1
}
- Latencia media: 2.74s (max 9.31s)

| Entrada | Esperado | Obtenido | Clasificación | Latencia |
|---|---|---|---|---|
| `Comí dos huevos y dos tortillas.` | registrado | registrado | correcto | 9.31s |
| `Comí 150 gramos de arroz con pollo hervido.` | registrado | aclaracion | aclaracion_innecesaria | 3.07s |
| `Tomé un vaso de agua.` | registrado | aclaracion | aclaracion_innecesaria | 1.89s |
| `Comí pizza.` | aclaracion | aclaracion | aclaracion_correcta | 2.06s |
| `Comí unos tacos.` | aclaracion | aclaracion | aclaracion_correcta | 2.16s |
| `Comí huevos asados con salsa.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 3.25s |
| `Comí frijoles.` | aclaracion | aclaracion | aclaracion_correcta | 1.99s |
| `Comí dos refrescos.` | registrado | aclaracion | aclaracion_innecesaria | 1.84s |
| `Comí pollo teriyaki.` | aclaracion | aclaracion | aclaracion_correcta | 2.04s |
| `Agua.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 1.74s |
| `Comí aguacate con limón.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 2.08s |
| `Hoy tengo mucha hambre.` | aclaracion | error | error | 1.39s |

Clasificación: correcto / aclaracion_correcta / falso_positivo / aclaracion_innecesaria / rechazo_correcto / error.
El loose end PIEZA→g se mantiene como variable conocida, no se contaminó el dataset.
