namespace Nutriologo.Application.Tests;

using Nutriologo.Application.Plans;
using Nutriologo.Domain.Catalog;
using Xunit;

public class ImportMealPlanTests
{
    private sealed class Cat : IFoodCatalogSource
    {
        public IReadOnlyList<FoodCatalogItem> Todos() => new[]
        {
            new FoodCatalogItem(1, "Pollo"), new FoodCatalogItem(2, "Arroz"), new FoodCatalogItem(3, "Tortilla"),
        };
    }

    private static readonly ValidateMealPlanCandidateUseCase Uc = new(new Cat());

    [Fact]
    public void Exito_ConAlimentosResueltos()
    {
        var r = Uc.Validate(new MealPlanCandidate("Plan base", new[]
        {
            new MealPlanItem("pollo", 150, "g", null), new MealPlanItem("arroz", 200, "g", null),
        }, 0.9m, Array.Empty<string>()));
        Assert.True(r.Exito);
        Assert.Equal(2, r.AlimentosResueltos!.Count);
    }

    [Fact]
    public void SinNombre_Falla() => Assert.False(Uc.Validate(new MealPlanCandidate(" ", Array.Empty<MealPlanItem>(), 0.9m, Array.Empty<string>())).Exito);

    [Fact]
    public void SinItems_Aclaracion()
    {
        var r = Uc.Validate(new MealPlanCandidate("Plan", Array.Empty<MealPlanItem>(), 0.9m, Array.Empty<string>()));
        Assert.False(r.Exito); Assert.True(r.RequiereAclaracion);
    }

    [Fact]
    public void CantidadInvalida_Rechaza() =>
        Assert.False(Uc.Validate(new MealPlanCandidate("Plan", new[] { new MealPlanItem("pollo", 0, "g", null) }, 0.9m, Array.Empty<string>())).Exito);

    [Fact]
    public void AlimentoDesconocido_AclaracionSinPersistir()
    {
        var r = Uc.Validate(new MealPlanCandidate("Plan", new[] { new MealPlanItem("pizza", 1, "pieza", null) }, 0.9m, Array.Empty<string>()));
        Assert.False(r.Exito); Assert.True(r.RequiereAclaracion); Assert.Null(r.AlimentosResueltos);
    }

    [Fact]
    public void AlimentoAmbiguo_Aclaracion()
    {
        var r = Uc.Validate(new MealPlanCandidate("Plan", new[] { new MealPlanItem("pollo asado", 1, "pieza", null) }, 0.9m, Array.Empty<string>()));
        Assert.False(r.Exito);
    }
}
