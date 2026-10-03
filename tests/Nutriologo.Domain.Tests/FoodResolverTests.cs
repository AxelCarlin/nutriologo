namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Catalog;
using Xunit;

public class FoodResolverTests
{
    private sealed class InMemoryCatalog : IFoodCatalogSource
    {
        public List<FoodCatalogItem> Items = new();
        public IReadOnlyList<FoodCatalogItem> Todos() => Items;
    }

    private static InMemoryCatalog Seed()
    {
        var c = new InMemoryCatalog();
        c.Items.AddRange(new[]
        {
            new FoodCatalogItem(1, "Huevo", MetodoPreparacion: "cocido"),
            new FoodCatalogItem(2, "Arroz", MetodoPreparacion: "hervido"),
            new FoodCatalogItem(3, "Pollo", MetodoPreparacion: "cocido"),
            new FoodCatalogItem(4, "Pollo asado", Cocina: "mexicana", MetodoPreparacion: "asado"),
            new FoodCatalogItem(5, "Tortilla", Cocina: "mexicana"),
            new FoodCatalogItem(6, "Frijoles", Cocina: "mexicana", Sinonimos: new[] { "frijol refrito", "frijol" }),
            new FoodCatalogItem(7, "Aguacate", Sinonimos: new[] { "palta" }),
        });
        return c;
    }

    [Fact]
    public void Exacta_Resuelve() =>
        Assert.Equal(1, DeterministicFoodResolver.Resolver("Huevo", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Mayusculas_YAcentos_Normaliza() =>
        Assert.Equal(7, DeterministicFoodResolver.Resolver("ÁGUACATE", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Plural_Singulariza() =>
        Assert.Equal(7, DeterministicFoodResolver.Resolver("aguacates", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Sinonimo_Resuelve() =>
        Assert.Equal(7, DeterministicFoodResolver.Resolver("palta", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Sinonimo_Frijol() =>
        Assert.Equal(6, DeterministicFoodResolver.Resolver("frijol refrito", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Espacios_Sobrantes_Normaliza() =>
        Assert.Equal(2, DeterministicFoodResolver.Resolver("  arroz  ", Seed()).Candidatos[0].IdAlimento);

    [Fact]
    public void Parcial_ProduceCandidatosConAclaracion()
    {
        var r = DeterministicFoodResolver.Resolver("asado", Seed());
        Assert.True(r.RequiereAclaracion);
        Assert.Contains(r.Candidatos, c => c.Nombre == "Pollo asado");
    }

    [Fact]
    public void ContextoPreparacion_Desambigua()
    {
        var r = DeterministicFoodResolver.Resolver("pollo", Seed(), contextoPreparacion: "asado");
        Assert.Equal(4, r.Candidatos[0].IdAlimento);
    }

    [Fact]
    public void Inexistente_RequiereAclaracion_YNoCrea()
    {
        var r = DeterministicFoodResolver.Resolver("pollo teriyaki", Seed());
        Assert.True(r.RequiereAclaracion);
        Assert.Empty(r.Candidatos.Where(c => c.Nombre == "pollo teriyaki"));
    }

    [Fact]
    public void CatalogoVacio_RequiereAclaracion() =>
        Assert.True(DeterministicFoodResolver.Resolver("pollo", new InMemoryCatalog()).RequiereAclaracion);

    [Fact]
    public void TextoVacio_RequiereAclaracion() =>
        Assert.True(DeterministicFoodResolver.Resolver("   ", Seed()).RequiereAclaracion);

    [Fact]
    public void NoCreaEntidades_SoloResuelveExistentes()
    {
        var cat = Seed();
        var antes = cat.Items.Count;
        DeterministicFoodResolver.Resolver("pizza", cat);
        Assert.Equal(antes, cat.Items.Count);
    }
}
