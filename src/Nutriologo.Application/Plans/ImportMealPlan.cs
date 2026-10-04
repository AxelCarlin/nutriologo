namespace Nutriologo.Application.Plans;

using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.AI;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Exceptions;

/// <summary>Candidato estructurado extraído por IA a partir de texto/imagen/formulario.</summary>
public record MealPlanCandidate(
    string NombrePlan,
    IReadOnlyList<MealPlanItem> Items,
    decimal ConfianzaGlobal,
    IReadOnlyList<string> Incertidumbres);

public record MealPlanItem(string NombreAlimento, decimal Cantidad, string Unidad, string? Preparacion);

public record PlanImportResult(
    bool Exito,
    bool RequiereAclaracion,
    string Mensaje,
    IReadOnlyList<ResolvedFood>? AlimentosResueltos,
    MealPlanCandidate? Candidato);

public class ValidateMealPlanCandidateUseCase
{
    private readonly IFoodCatalogSource _catalog;

    public ValidateMealPlanCandidateUseCase(IFoodCatalogSource catalog) => _catalog = catalog;

    /// <summary>Valida el candidato antes de persistir: resolución determinista de alimentos,
    /// cantidades positivas y unidades convertibles. No crea ni modifica catálogo.</summary>
    public PlanImportResult Validate(MealPlanCandidate candidato)
    {
        if (string.IsNullOrWhiteSpace(candidato.NombrePlan))
            return new(false, false, "Nombre de plan requerido.", null, null);
        if (candidato.Items.Count == 0)
            return new(false, true, "El plan no contiene alimentos.", null, null);

        var resueltos = new List<ResolvedFood>();
        foreach (var item in candidato.Items)
        {
            if (item.Cantidad <= 0) return new(false, false, $"Cantidad inválida para '{item.NombreAlimento}'.", null, null);
            var outcome = DeterministicFoodResolver.Resolver(item.NombreAlimento, _catalog, item.Preparacion);
            if (outcome.RequiereAclaracion || outcome.Candidatos.Count != 1)
                return new(false, true, $"Alimento '{item.NombreAlimento}' requiere aclaración: {outcome.Motivo}", null, null);
            resueltos.Add(outcome.Candidatos[0]);
        }
        return new(true, false, "Válido", resueltos, candidato);
    }
}
