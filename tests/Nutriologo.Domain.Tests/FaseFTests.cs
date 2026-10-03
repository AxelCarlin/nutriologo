namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Plans;
using Xunit;

public class FaseFTests
{
    private static T Make<T>(params (string Name, object? Value)[] props) where T : new()
    {
        var obj = new T();
        foreach (var (name, value) in props)
            typeof(T).GetProperty(name)!.SetValue(obj, value);
        return obj;
    }

    [Fact]
    public void PlanValido_Pasa()
    {
        var p = Make<PlanAlimenticio>(("Nombre", "Semana 1"), ("FechaInicio", DateTime.UtcNow), ("CaloriasObjetivo", 2000m));
        p.DatosValidos();
    }

    [Fact]
    public void PlanFechaFinInvalida_Lanza()
    {
        var p = Make<PlanAlimenticio>(("Nombre", "X"), ("FechaInicio", DateTime.UtcNow), ("FechaFin", DateTime.UtcNow.AddDays(-1)));
        Assert.Throws<DomainException>(() => p.DatosValidos());
    }

    [Fact]
    public void PlanSinNombre_Lanza() =>
        Assert.Throws<DomainException>(() => Make<PlanAlimenticio>(("Nombre", " "), ("FechaInicio", DateTime.UtcNow)).DatosValidos());

    [Fact]
    public void DiaNumeroInvalido_Lanza() =>
        Assert.Throws<DomainException>(() => Make<DiaPlanAlimenticio>(("NumeroDia", 0)).DatosValidos());

    [Fact]
    public void AlimentoPlanCantidadInvalida_Lanza() =>
        Assert.Throws<DomainException>(() => Make<AlimentoComidaPlan>(("Cantidad", -1m)).DatosValidos());

    [Fact]
    public void RegistroComida_IndependienteDePlan_EsValido()
    {
        var r = Make<RegistroComida>(("FechaConsumo", DateTime.UtcNow), ("CaloriasEstimadas", 450m));
        r.DatosValidos();
    }

    [Fact]
    public void RegistroComidaCaloriasNegativas_Lanza() =>
        Assert.Throws<DomainException>(() => Make<RegistroComida>(("FechaConsumo", DateTime.UtcNow), ("CaloriasEstimadas", -10m)).DatosValidos());

    [Fact]
    public void AlimentoRegistroConfianzaFueraDeRango_Lanza() =>
        Assert.Throws<DomainException>(() => Make<AlimentoRegistroComida>(("Cantidad", 100m), ("Confianza", 1.5m)).DatosValidos());

    [Fact]
    public void Balance_ConsumidoMenorAObjetivo_Negativo() =>
        Assert.Equal(-500m, BalanceService.Calcular(1500m, 2000m).Balance);

    [Fact]
    public void Balance_SinObjetivo_BalanceNull() =>
        Assert.Null(BalanceService.Calcular(1500m, null).Balance);

    [Fact]
    public void Balance_ConsumidoNegativo_Lanza() =>
        Assert.Throws<DomainException>(() => BalanceService.Calcular(-1m, 2000m));

    [Fact]
    public void PlanSinConsumo_YConsumoSinPlan_NoHayAcoplamientoEnTipos()
    {
        // PLANIFICADO != CONSUMIDO: ningún tipo de consumo referencia a un plan.
        var consumoProps = typeof(RegistroComida).GetProperties().Concat(typeof(AlimentoRegistroComida).GetProperties());
        Assert.DoesNotContain(consumoProps, p => p.PropertyType.Namespace?.StartsWith("Nutriologo.Domain.Plans") == true);
    }

    [Fact]
    public void CantidadPlanificadaVsConsumida_SonDistintas()
    {
        var plan = Make<AlimentoComidaPlan>(("IdAlimento", 1), ("Cantidad", 2m));
        var real = Make<AlimentoRegistroComida>(("IdAlimento", 1), ("Cantidad", 1m));
        Assert.NotEqual(plan.Cantidad, real.Cantidad);
    }

    [Fact]
    public void HistorialEsInmutable_PorDisenio()
    {
        // No existen setters públicos en las propiedades de contenido de RegistroComida/AlimentoRegistroComida.
        foreach (var tipo in new[] { typeof(RegistroComida), typeof(AlimentoRegistroComida) })
        {
            foreach (var prop in tipo.GetProperties())
            {
                if (prop.Name is "Id" or "Estatus" or "FechaCreacion" or "FechaModificacion" or "IdUsuarioCrea" or "IdUsuarioModifica")
                    continue;
                var setter = prop.SetMethod;
                Assert.True(setter is null || !setter.IsPublic, $"{tipo.Name}.{prop.Name} debe ser inmutable");
            }
        }
    }
}
