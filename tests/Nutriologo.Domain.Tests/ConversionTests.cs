namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Exceptions;
using Xunit;

public class ConversionTests
{
    private static readonly ConversionUnidad[] Conv =
    [
        Make(1, 2, 1000m), // KG -> G
        Make(2, 1, 0.001m), // G -> KG
        Make(3, 4, 240m),   // TAZA -> ML
    ];

    private static ConversionUnidad Make(int o, int d, decimal f)
    {
        var c = new ConversionUnidad();
        typeof(ConversionUnidad).GetProperty("IdUnidadOrigen")!.SetValue(c, o);
        typeof(ConversionUnidad).GetProperty("IdUnidadDestino")!.SetValue(c, d);
        typeof(ConversionUnidad).GetProperty("FactorConversion")!.SetValue(c, f);
        return c;
    }

    [Fact]
    public void Convertir_Kg_a_G() => Assert.Equal(1500m, ConversionService.Convert(1.5m, 1, 2, Conv));

    [Fact]
    public void Convertir_Taza_a_Ml() => Assert.Equal(240m, ConversionService.Convert(1m, 3, 4, Conv));

    [Fact]
    public void MismaUnidad_RetornaIgual() => Assert.Equal(42m, ConversionService.Convert(42m, 1, 1, Conv));

    [Fact]
    public void SinConversionDirecta_Lanza() => Assert.Throws<DomainException>(() => ConversionService.Convert(1m, 2, 4, Conv));

    [Fact]
    public void Escalar_ComposicionPor100g()
    {
        Assert.Equal(77.5m, NutricionScaling.Escalar(155m, 100m, 50m)); // huevo: 155 kcal/100g -> pieza 50g
    }

    [Fact]
    public void Escalar_BaseInvalida_Lanza() => Assert.Throws<DomainException>(() => NutricionScaling.Escalar(10m, 0m, 50m));

    [Fact]
    public void Escalar_Negativo_Lanza() => Assert.Throws<DomainException>(() => NutricionScaling.Escalar(10m, 100m, -1m));
}
