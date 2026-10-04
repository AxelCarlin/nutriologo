# J3 — Evaluación end-to-end (Api + PostgreSQL + Ollama)

- Casos: 12
- Clasificación: {
  "correcto": 1,
  "aclaracion_innecesaria": 3,
  "aclaracion_correcta": 8
}
- Latencia media: 2.79s (max 9.69s)

| Entrada | Esperado | Obtenido | Clasificación | Latencia |
|---|---|---|---|---|
| `Comí dos huevos y dos tortillas.` | registrado | registrado | correcto | 9.69s |
| `Comí 150 gramos de arroz con pollo hervido.` | registrado | aclaracion | aclaracion_innecesaria | 3.24s |
| `Tomé un vaso de agua.` | registrado | aclaracion | aclaracion_innecesaria | 1.94s |
| `Comí pizza.` | aclaracion | aclaracion | aclaracion_correcta | 2.23s |
| `Comí unos tacos.` | aclaracion | aclaracion | aclaracion_correcta | 2.26s |
| `Comí huevos asados con salsa.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 3.18s |
| `Comí frijoles.` | aclaracion | aclaracion | aclaracion_correcta | 1.97s |
| `Comí dos refrescos.` | registrado | aclaracion | aclaracion_innecesaria | 2.23s |
| `Comí pollo teriyaki.` | aclaracion | aclaracion | aclaracion_correcta | 2.09s |
| `Agua.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 1.73s |
| `Comí aguacate con limón.` | registrado_o_aclaracion | aclaracion | aclaracion_correcta | 2.08s |
| `Hoy tengo mucha hambre.` | aclaracion | aclaracion | aclaracion_correcta | 0.88s |

Clasificación: correcto / aclaracion_correcta / falso_positivo / aclaracion_innecesaria / rechazo_correcto / error.
El loose end PIEZA→g se mantiene como variable conocida, no se contaminó el dataset.
