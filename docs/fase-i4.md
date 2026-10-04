# Fase I4 — Registro de comida

## Backend

- `RegisterMealValidator` (Application): valida cantidades positivas + alimentos resolubles con `DeterministicFoodResolver` (G3). Catálogo vía `IFoodCatalogSource` → `EfCoreFoodCatalogSource` (Infrastructure).
- Endpoints: `POST /api/comidas` (valida, persiste `RegistroComida` + `AlimentoRegistroComida`, deriva calorías desde `AlimentoNutriente` × `NutricionScaling`), `GET /api/comidas`, `GET /api/balance` (suma `CaloriasEstimadas` por usuario/fecha), `GET /api/alimentos`.
- Calorías derivadas del catálogo, no introducidas manualmente.

## Frontend

`RegistroComidaView` (`/registrar`): items dinámicos, loading/error, muestra balance real del día.

## Verificación

- `POST /api/comidas` con pollo+arroz → items con 247.5 y 260 kcal persistidos; balance del día suma `RegistroComida.CaloriasEstimadas`.
- Tests: Domain 73/73, Application 43/43, Infrastructure 3/3. Build backend 0 errores. Frontend build OK, Vitest 1/1.

## Limitación conocida (documentada)

La derivación de calorías asume que `Cantidad` está en la unidad base del alimento (G). Unidades PIEZA requieren usar `PorcionAlimento.CantidadGramos` como puente — mejora pendiente (no bloquea I5).
