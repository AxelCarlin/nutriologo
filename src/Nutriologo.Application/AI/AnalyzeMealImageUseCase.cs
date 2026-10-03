namespace Nutriologo.Application.AI;

using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Domain.Catalog;

/// <summary>Resultado del análisis visual: predicciones crudas + candidatos resueltos.
/// La verdad nutricional NO se deriva de la visión; requiere resolver y calcular en Domain.</summary>
public record MealImageAnalysis(
    decimal ConfianzaGlobal,
    IReadOnlyList<FoodCandidate> PrediccionesCrudas,
    IReadOnlyList<FoodResolutionOutcome> Resoluciones,
    string Modelo,
    string? Version,
    long DuracionMs);

public class AnalyzeMealImageUseCase
{
    private readonly IVisionAnalyzer _vision;
    private readonly IFoodCatalogSource _catalog;

    public AnalyzeMealImageUseCase(IVisionAnalyzer vision, IFoodCatalogSource catalog)
    {
        _vision = vision;
        _catalog = catalog;
    }

    public async Task<MealImageAnalysis> ExecuteAsync(VisionRequest request, CancellationToken ct = default)
    {
        var result = await _vision.AnalyzeAsync(request, ct);
        var resoluciones = result.Value.Candidatos
            .Select(c => DeterministicFoodResolver.Resolver(c.NombreDetectado, _catalog, c.Preparacion))
            .ToList();
        return new MealImageAnalysis(result.Value.ConfianzaGlobal, result.Value.Candidatos, resoluciones,
            result.Modelo, result.Version, result.DuracionMs);
    }
}
