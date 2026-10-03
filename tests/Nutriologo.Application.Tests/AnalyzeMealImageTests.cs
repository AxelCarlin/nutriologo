namespace Nutriologo.Application.Tests;

using Nutriologo.Application.AI;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Domain.Catalog;
using Xunit;

public class AnalyzeMealImageTests
{
    private sealed class FakeVision : IVisionAnalyzer
    {
        public Task<AiResult<VisionAnalysis>> AnalyzeAsync(VisionRequest r, CancellationToken ct = default) =>
            Task.FromResult(new AiResult<VisionAnalysis>(
                new VisionAnalysis(0.82m, new[]
                {
                    new FoodCandidate("tacos", 2, "piezas", null, 0.9m, new[] { "tipo de carne?" }),
                    new FoodCandidate("pizza", 1, "rebanada", null, 0.7m, Array.Empty<string>()),
                }), "fake-model", "1.0", 123));
    }

    private sealed class Cat : IFoodCatalogSource
    {
        public IReadOnlyList<FoodCatalogItem> Todos() => new[]
        {
            new FoodCatalogItem(1, "Taco", MetodoPreparacion: "asado"),
            new FoodCatalogItem(2, "Arroz", MetodoPreparacion: "hervido"),
        };
    }

    [Fact]
    public async Task Pipeline_Vision_Resolver_SinPersistirVerdad()
    {
        var uc = new AnalyzeMealImageUseCase(new FakeVision(), new Cat());
        var r = await uc.ExecuteAsync(new VisionRequest("/tmp/foto.jpg"));

        Assert.Equal("fake-model", r.Modelo);
        Assert.Equal(2, r.PrediccionesCrudas.Count);
        Assert.Equal(2, r.Resoluciones.Count);
        Assert.Contains(r.Resoluciones, x => x.RequiereAclaracion && x.Motivo.Contains("inexistente"));
        Assert.Contains(r.Resoluciones, x => !x.RequiereAclaracion && x.Candidatos[0].IdAlimento == 1);
        // La visión no entrega verdades nutricionales: el resultado contiene predicciones + resoluciones, no calorías.
        Assert.Null(typeof(MealImageAnalysis).GetProperty("Calorias"));
    }
}
