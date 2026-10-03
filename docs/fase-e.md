# Fase E — Recetas y alimentos compuestos

## Entidades

`RecetaAlimento`, `IngredienteReceta`, `RecetaUsuario` mapeadas a EF conforme al schema (sin cambios en `NutriologoDB.sql`).

## Invariantes

- **XOR estricto** en `IngredienteReceta`: exactamente uno de `IdAlimento`/`IdRecetaComponente` (validado en constructor).
- Cantidad > 0; referencia existente o `DomainException`.

## Composición de recetas

`RecipeCompositionService.Componer` (Domain, puro):

1. Recorre ingredientes por `Orden`.
2. Convierte cantidad a gramos: `CantidadGramos` directo, o vía `ConversionService` (Fase D) si la unidad difiere.
3. Alimento: escala nutrientes por `NutricionScaling` (Fase D).
4. Sub-receta: compone recursivamente y multiplica por la `Cantidad` del ingrediente (interpretada como número de porciones completas de la sub-receta — default razonable, documentado).
5. Ciclos directos e indirectos: DFS con pila → `DomainException`. Límite de profundidad 32.

## Tests

Simple, anidada, ciclo indirecto (3↔4), ciclo directo (5→5), XOR inválido (ambos/ninguno), cantidad 0, referencia inexistente. 41/41 verdes. Build 0 errores.
