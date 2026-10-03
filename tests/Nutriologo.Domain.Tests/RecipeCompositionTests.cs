namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Recipes;
using Xunit;

public class RecipeCompositionTests
{
    private const int G = 2;

    private sealed class InMemorySource : IRecipeCompositionSource
    {
        public Dictionary<int, List<IngredienteReceta>> Ingredients = new();
        public Dictionary<int, AlimentoNutricional> Foods = new();
        public IReadOnlyList<ConversionUnidad> Conversiones => Array.Empty<ConversionUnidad>();
        public int IdUnidadGramos => G;

        public IReadOnlyList<IngredienteReceta> IngredientesDe(int id) => Ingredients[id];
        public AlimentoNutricional? NutrientesDeAlimento(int id) => Foods.GetValueOrDefault(id);
        public int IdAlimentoDeReceta(int id) => id;
    }

    private static InMemorySource Build()
    {
        var s = new InMemorySource();
        // Alimentos: id 1=tortilla, 3=pollo, 8=salsa
        s.Foods[1] = new AlimentoNutricional(1, 100m, new[] { (1, 218m), (2, 5.7m) }); // energia, proteina por 100g
        s.Foods[3] = new AlimentoNutricional(3, 100m, new[] { (1, 165m), (2, 31m) });
        s.Foods[8] = new AlimentoNutricional(8, 100m, new[] { (1, 20m), (2, 1m) });

        // Receta 1: Taco = tortilla 30g + pollo 100g + salsa 20g
        s.Ingredients[1] = new List<IngredienteReceta>
        {
            new(1, 1, null, 30m, G, 1, 30m),
            new(1, 3, null, 100m, G, 2, 100m),
            new(1, 8, null, 20m, G, 3, 20m),
        };
        // Receta 2: Taco doble = 2 tacos
        s.Ingredients[2] = new List<IngredienteReceta> { new(2, null, 1, 2m, G, 1) };
        // Ciclo indirecto 3<->4
        s.Ingredients[3] = new List<IngredienteReceta> { new(3, null, 4, 1m, G, 1) };
        s.Ingredients[4] = new List<IngredienteReceta> { new(4, null, 3, 1m, G, 1) };
        // Ciclo directo 5->5
        s.Ingredients[5] = new List<IngredienteReceta> { new(5, null, 5, 1m, G, 1) };
        return s;
    }

    [Fact]
    public void RecetaSimple_SumaNutrientes()
    {
        var s = Build();
        var r = RecipeCompositionService.Componer(1, s);
        Assert.Equal(218m * 30m / 100m + 165m + 20m * 20m / 100m, r[1], precision: 4);
        Assert.Equal(5.7m * 30m / 100m + 31m + 1m * 20m / 100m, r[2], precision: 4);
    }

    [Fact]
    public void RecetaAnidada_EscalaSubReceta()
    {
        var s = Build();
        var simple = RecipeCompositionService.Componer(1, s);
        var doble = RecipeCompositionService.Componer(2, s);
        Assert.Equal(simple[1] * 2m, doble[1], precision: 4);
    }

    [Fact]
    public void CicloIndirecto_Lanza() => Assert.Throws<DomainException>(() => RecipeCompositionService.Componer(3, Build()));

    [Fact]
    public void CicloDirecto_Lanza() => Assert.Throws<DomainException>(() => RecipeCompositionService.Componer(5, Build()));

    [Fact]
    public void ReferenciaInexistente_Lanza()
    {
        var s = Build();
        s.Ingredients[9] = new List<IngredienteReceta> { new(9, 999, null, 50m, G, 1, 50m) };
        Assert.Throws<DomainException>(() => RecipeCompositionService.Componer(9, s));
    }

    [Fact]
    public void XorInvalido_Ambos_Lanza() =>
        Assert.Throws<DomainException>(() => new IngredienteReceta(1, 1, 2, 10m, G, 1));

    [Fact]
    public void XorInvalido_Ninguno_Lanza() =>
        Assert.Throws<DomainException>(() => new IngredienteReceta(1, null, null, 10m, G, 1));

    [Fact]
    public void CantidadInvalida_Lanza() =>
        Assert.Throws<DomainException>(() => new IngredienteReceta(1, 1, null, 0m, G, 1));
}
