# J3 — Evaluación end-to-end (Api + PostgreSQL + Ollama)

- Casos: 12
- Clasificación: {
  "correcto": 1,
  "aclaracion_innecesaria": 3,
  "aclaracion_correcta": 7,
  "error": 1
}
- Latencia media: 2.26s (max 3.81s)

| Entrada | Esperado | Obtenido | Clasificación | Latencia |
|---|---|---|---|---|
| `Comí dos huevos y dos tortillas.` | registrado | registrado | correcto | 3.81s |
| `Comí 150 gramos de arroz con pollo hervido.` | registrado | aclaracion | aclaracion_innecesaria | 3.05s |
| `Tomé un vaso de agua.` | registrado | aclaracion | aclaracion_innecesaria | 1.9s |
| `Comí pizza.` | aclaracion | aclaracion | aclaracion_correcta | 2.06s |
| `Comí unos tacos.` | aclaracion | aclaracion | aclaracion_correcta | 2.16s |
| `Comí huevos asados con salsa.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 3.22s |
| `Comí frijoles.` | aclaracion | aclaracion | aclaracion_correcta | 1.96s |
| `Comí dos refrescos.` | registrado | aclaracion | aclaracion_innecesaria | 1.82s |
| `Comí pollo teriyaki.` | aclaracion | aclaracion | aclaracion_correcta | 2.03s |
| `Agua.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 1.71s |
| `Comí aguacate con limón.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 2.03s |
| `Hoy tengo mucha hambre.` | aclaracion | error | error | 1.36s |

Clasificación: correcto / aclaracion_correcta / falso_positivo / aclaracion_innecesaria / rechazo_correcto / error.
El loose end PIEZA→g se mantiene como variable conocida, no se contaminó el dataset.
