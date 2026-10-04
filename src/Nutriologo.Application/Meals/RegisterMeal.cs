namespace Nutriologo.Application.Meals;

using Nutriologo.Domain.Catalog;

public record MealItemInput(string NombreAlimento, decimal Cantidad, string Unidad, string? Preparacion = null);
public record MealInput(int IdUsuario, DateTime FechaConsumo, IReadOnlyList<MealItemInput> Items);

public record MealValidationResult(bool Exito, bool RequiereAclaracion, string Mensaje, IReadOnlyList<ResolvedFood> Alimentos);

public static class RegisterMealValidator
{
    /// <summary>Valida antes de persistir: cantidades positivas y alimentos resolubles.
    /// Reutiliza DeterministicFoodResolver (G3). No calcula calorías.</summary>
    public static MealValidationResult Validate(MealInput input, IFoodCatalogSource catalog)
    {
        if (input.Items.Count == 0) return new(false, true, "La comida no tiene alimentos.", Array.Empty<ResolvedFood>());

        var resueltos = new List<ResolvedFood>();
        foreach (var it in input.Items)
        {
            if (it.Cantidad <= 0) return new(false, false, $"Cantidad inválida para '{it.NombreAlimento}'.", Array.Empty<ResolvedFood>());
            var r = DeterministicFoodResolver.Resolver(it.NombreAlimento, catalog, it.Preparacion);
            if (r.RequiereAclaracion || r.Candidatos.Count != 1)
                return new(false, true, $"'{it.NombreAlimento}': {r.Motivo}", Array.Empty<ResolvedFood>());
            resueltos.Add(r.Candidatos[0]);
        }
        return new(true, false, "Válido", resueltos);
    }
}
