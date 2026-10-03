# Fase C — Dominio nutricional determinístico

## Fórmulas documentadas

- **BMR (Mifflin–St Jeor):** `10·peso(kg) + 6.25·altura(cm) − 5·edad + s`, con `s = +5` masculino, `s = −161` femenino.
- **TDEE:** `BMR · factorActividad` (factor desde `NivelActividadFisica.FactorActividad`).
- **Cambio total:** `|pesoInicial − pesoObjetivo|`.
- **Ritmo semanal:** `cambioTotal / semanasObjetivo` (kg/semana).
- **Porcentaje de cambio:** `cambioTotal / pesoInicial · 100`.
- **Déficit diario requerido:** `cambioTotal · 7700 / (semanas · 7)` (7700 kcal ≈ 1 kg). Para ganancia se reporta como superávit (negativo en el signo de déficit).
- **Meta calórica estimada:** `TDEE − déficitDiario`.

## Evaluadores de riesgo (configurables, no hardcodeados)

- `RiesgoEvaluator.EvaluarRitmo`: bandas de `ReglaRiesgoObjetivo` (`RitmoMinimo`/`RitmoMaximo`), contención, primera por `Orden`.
- `RiesgoEvaluator.EvaluarMetaProporcionada`: `ReglaRiesgoMetaCalorica` (`CaloriasMinimas`/`DeficitMaximo`). Una regla se **viola** si la meta cae bajo el mínimo o el déficit excede el máximo; se toma la violada de menor `Orden`; si ninguna, la de mayor `Orden` (sin riesgo destacado). La meta nunca se sobrescribe.

## Guardas de seguridad

`NutritionSafetyGuard.EnsureAllowed` — para `PerderPeso`: bloquea si edad < 18, embarazo o lactancia activa. No diagnostica; solo impide aplicar el algoritmo normal y exige valoración profesional.

## Respeto al esquema

- Entidades/registros modelan `ObjetivoNutricional`, `EstimacionEnergetica`, `MetaCalorica`, `ReglaRiesgoObjetivo`, `ReglaRiesgoMetaCalorica`, `NivelRiesgo` sin duplicar tablas.
- `ICalorieCalculator` está en Domain: sin EF Core, Npgsql, ASP.NET, Ollama (garantizado por tests estructurales).

## Evidencia

- `dotnet build`: 0 errores, 0 warnings.
- `dotnet test` Nutriologo.Domain.Tests: 26/26 pasan (incluye caso 92→82 kg en 22 semanas: ritmo 0.4545 kg/sem, 500 kcal/día de déficit, meta = TDEE−500).
