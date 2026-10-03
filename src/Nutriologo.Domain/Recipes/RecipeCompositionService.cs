namespace Nutriologo.Domain.Recipes;

using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Exceptions;

/// <summary>Nutrientes de un alimento: lista de (IdNutriente, cantidad por base en gramos).</summary>
public record AlimentoNutricional(int IdAlimento, decimal CantidadBaseGramos, IReadOnlyList<(int IdNutriente, decimal Cantidad)> Nutrientes);

public interface IRecipeCompositionSource
{
    IReadOnlyList<IngredienteReceta> IngredientesDe(int idRecetaAlimento);
    AlimentoNutricional? NutrientesDeAlimento(int idAlimento);
    int IdAlimentoDeReceta(int idRecetaAlimento);
    IReadOnlyList<ConversionUnidad> Conversiones { get; }
    int IdUnidadGramos { get; }
}

public static class RecipeCompositionService
{
    public const int MaxProfundidad = 32;

    /// <summary>Suma nutrientes de todos los ingredientes de la receta, recursivo, con detección de ciclos.</summary>
    public static Dictionary<int, decimal> Componer(int idRecetaAlimento, IRecipeCompositionSource source)
    {
        var totales = new Dictionary<int, decimal>();
        var pila = new HashSet<int>();
        ComponerRecursivo(idRecetaAlimento, source, pila, totales, 0);
        return totales;
    }

    private static void ComponerRecursivo(int idReceta, IRecipeCompositionSource source, HashSet<int> pila,
        Dictionary<int, decimal> totales, int profundidad)
    {
        if (profundidad > MaxProfundidad) throw new DomainException("Profundidad máxima de anidación excedida.");
        if (!pila.Add(idReceta)) throw new DomainException($"Ciclo detectado en receta {idReceta}.");
        try
        {
            foreach (var ing in source.IngredientesDe(idReceta).OrderBy(i => i.Orden))
            {
                ing.ValidarXor();
                decimal gramos = ing.CantidadGramos
                    ?? ConversionService.Convert(ing.Cantidad, ing.IdUnidadMedida, source.IdUnidadGramos, source.Conversiones);

                if (ing.IdAlimento is { } idAlimento)
                {
                    var nut = source.NutrientesDeAlimento(idAlimento)
                        ?? throw new DomainException($"Referencia inexistente: alimento {idAlimento}.");
                    foreach (var (idN, cant) in nut.Nutrientes)
                        totales[idN] = totales.GetValueOrDefault(idN) + NutricionScaling.Escalar(cant, nut.CantidadBaseGramos, gramos);
                }
                else if (ing.IdRecetaComponente is { } idSub)
                {
                    var sub = new Dictionary<int, decimal>();
                    ComponerRecursivo(idSub, source, pila, sub, profundidad + 1);
                    // Gramos del ingrediente como fracción de la receta sub-componente:
                    // se escala el total de la receta sub por (gramos del ingrediente / 100 g de la sub-receta agregada)
                    // — ver nota en docs; simplificación: se suma el total de la sub-receta por unidad de ingrediente (Cantidad en piezas/porciones).
                    foreach (var kv in sub)
                        totales[kv.Key] = totales.GetValueOrDefault(kv.Key) + kv.Value * ing.Cantidad;
                }
                else throw new DomainException("Ingrediente inválido.");
            }
        }
        finally { pila.Remove(idReceta); }
    }
}
