namespace Nutriologo.Application.Tests;

using Nutriologo.Application.Plans;
using Xunit;

public class PlanConsumptionComparisonTests
{
    private static readonly DateTime D1 = new(2026, 10, 1);
    private static readonly DateTime D2 = new(2026, 10, 2);

    [Fact]
    public void CoincidenciaExacta()
    {
        var r = PlanConsumptionComparisonService.Compare(
            new[] { new PlanItemDto(D1, 1, 5, 2, 1, 300) },
            new[] { new ConsumptionItemDto(D1, 1, 5, 2, 1, 300) });
        Assert.Single(r);
        Assert.Equal(ComparacionEstado.Coincidencia, r[0].Estado);
        Assert.Equal(0m, r[0].DiferenciaCantidad);
    }

    [Fact]
    public void CantidadConsumidaDiferente()
    {
        var r = PlanConsumptionComparisonService.Compare(
            new[] { new PlanItemDto(D1, 1, 5, 2, 1, 300) },
            new[] { new ConsumptionItemDto(D1, 1, 5, 3, 1, 450) });
        Assert.Equal(ComparacionEstado.CantidadDiferente, r[0].Estado);
        Assert.Equal(1m, r[0].DiferenciaCantidad);
        Assert.Equal(150m, r[0].DiferenciaCalorias);
    }

    [Fact]
    public void PlaneadoSinConsumo()
    {
        var r = PlanConsumptionComparisonService.Compare(new[] { new PlanItemDto(D1, 1, 5, 2, 1, 300) }, Array.Empty<ConsumptionItemDto>());
        Assert.Equal(ComparacionEstado.PlanificadoSinConsumo, r[0].Estado);
    }

    [Fact]
    public void ConsumoSinPlan()
    {
        var r = PlanConsumptionComparisonService.Compare(Array.Empty<PlanItemDto>(), new[] { new ConsumptionItemDto(D1, 1, 5, 2, 1, 300) });
        Assert.Equal(ComparacionEstado.ConsumidoSinPlan, r[0].Estado);
    }

    [Fact]
    public void MultiplesAlimentosYDias()
    {
        var plan = new[]
        {
            new PlanItemDto(D1, 1, 5, 2, 1, 300), new PlanItemDto(D1, 1, 7, 1, 1, 160),
            new PlanItemDto(D2, 3, 5, 2, 1, 300),
        };
        var real = new[]
        {
            new ConsumptionItemDto(D1, 1, 5, 2, 1, 300), new ConsumptionItemDto(D2, 3, 7, 1, 1, 160),
        };
        var r = PlanConsumptionComparisonService.Compare(plan, real);
        Assert.Equal(4, r.Count);
        Assert.Single(r, x => x.Estado == ComparacionEstado.Coincidencia);
        Assert.Equal(2, r.Count(x => x.Estado == ComparacionEstado.PlanificadoSinConsumo));
        Assert.Single(r, x => x.Estado == ComparacionEstado.ConsumidoSinPlan);
    }

    [Fact]
    public void NoModificaFuentes()
    {
        var plan = new List<PlanItemDto> { new(D1, 1, 5, 2, 1, 300) };
        var real = new List<ConsumptionItemDto> { new(D1, 1, 5, 2, 1, 300) };
        PlanConsumptionComparisonService.Compare(plan, real);
        Assert.Single(plan); Assert.Single(real);
        Assert.Equal(2m, plan[0].Cantidad); Assert.Equal(2m, real[0].Cantidad);
    }
}
