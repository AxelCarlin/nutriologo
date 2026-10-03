namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Nutrition;
using Xunit;

public class CalorieCalculatorTests
{
    private readonly ICalorieCalculator _calc = new MifflinStJeorCalorieCalculator();

    [Fact]
    public void Bmr_MifflinStJeor_Male()
    {
        var r = _calc.Estimate(new CalorieInput(80, 175, 30, SexoBiologico.Masculino, 1.0m, TipoObjetivo.MantenerPeso));
        Assert.Equal(1748.75m, r.MetabolismoBasal);
    }

    [Fact]
    public void Bmr_MifflinStJeor_Female()
    {
        var r = _calc.Estimate(new CalorieInput(60, 165, 30, SexoBiologico.Femenino, 1.0m, TipoObjetivo.MantenerPeso));
        Assert.Equal(600m + 6.25m * 165m - 150m - 161m, r.MetabolismoBasal);
    }

    [Fact]
    public void Tdee_IsBmrTimesFactor()
    {
        var r = _calc.Estimate(new CalorieInput(80, 175, 30, SexoBiologico.Masculino, 1.55m, TipoObjetivo.MantenerPeso));
        Assert.Equal(Math.Round(1748.75m * 1.55m, 2), r.GastoEnergeticoTotal);
    }

    [Fact]
    public void CasoValidacion_92_a_82_en_22_semanas()
    {
        var r = _calc.Estimate(new CalorieInput(92, 175, 35, SexoBiologico.Masculino, 1.55m,
            TipoObjetivo.PerderPeso, PesoObjetivoKg: 82, SemanasObjetivo: 22));

        Assert.Equal(10m, r.KgAjustar);
        Assert.Equal(0.4545m, r.RitmoSemanalKg);
        Assert.Equal(Math.Round(10m / 92m * 100m, 4), r.PorcentajeCambio);
        Assert.Equal(500m, r.DeficitDiarioRequerido);
        Assert.Equal(2857.81m - 500m, r.MetaCaloricaEstimada);
    }

    [Fact]
    public void GanarPeso_MetaEsSuperavit()
    {
        var r = _calc.Estimate(new CalorieInput(70, 170, 28, SexoBiologico.Masculino, 1.4m,
            TipoObjetivo.GanarPeso, PesoObjetivoKg: 75, SemanasObjetivo: 20));
        Assert.True(r.DeficitDiarioRequerido < 0);
        Assert.True(r.MetaCaloricaEstimada > r.GastoEnergeticoTotal);
    }

    [Theory]
    [InlineData(0, 175, 30, 1.0)]
    [InlineData(80, 0, 30, 1.0)]
    [InlineData(80, 175, 0, 1.0)]
    [InlineData(80, 175, 30, 0)]
    public void DatosInvalidos_LanzanDomainException(decimal p, decimal a, int e, decimal f) =>
        Assert.Throws<DomainException>(() => _calc.Estimate(
            new CalorieInput(p, a, e, SexoBiologico.Masculino, f, TipoObjetivo.MantenerPeso)));

    [Fact]
    public void ObjetivoInconsistente_LanzaDomainException() =>
        Assert.Throws<DomainException>(() => _calc.Estimate(
            new CalorieInput(80, 175, 30, SexoBiologico.Masculino, 1.2m,
                TipoObjetivo.PerderPeso, PesoObjetivoKg: 85, SemanasObjetivo: 10)));

    [Fact]
    public void SemanasInvalidas_LanzaDomainException() =>
        Assert.Throws<DomainException>(() => _calc.Estimate(
            new CalorieInput(80, 175, 30, SexoBiologico.Masculino, 1.2m,
                TipoObjetivo.PerderPeso, PesoObjetivoKg: 75, SemanasObjetivo: 0)));
}

public class NutritionSafetyGuardTests
{
    [Fact]
    public void MenorDeEdad_ConPerdidaDePeso_Bloquea() =>
        Assert.Throws<DomainException>(() => NutritionSafetyGuard.EnsureAllowed(
            TipoObjetivo.PerderPeso, 17, EstadoFisiologicoCodigo.Ninguno));

    [Theory]
    [InlineData(EstadoFisiologicoCodigo.Embarazo)]
    [InlineData(EstadoFisiologicoCodigo.Lactancia)]
    public void EmbarazoOLactancia_ConPerdidaDePeso_Bloquea(EstadoFisiologicoCodigo estado) =>
        Assert.Throws<DomainException>(() => NutritionSafetyGuard.EnsureAllowed(
            TipoObjetivo.PerderPeso, 30, estado));

    [Fact]
    public void AdultoSinCondicion_Permite() =>
        NutritionSafetyGuard.EnsureAllowed(TipoObjetivo.PerderPeso, 30, EstadoFisiologicoCodigo.Ninguno);

    [Fact]
    public void MantenerPeso_NoAplicaGuardaDePerdida() =>
        NutritionSafetyGuard.EnsureAllowed(TipoObjetivo.MantenerPeso, 17, EstadoFisiologicoCodigo.Embarazo);
}

public class RiesgoEvaluatorTests
{
    private static readonly ReglaRiesgoObjetivoDef[] ReglasRitmo =
    [
        new("LENTO", null, 0.5m, 1, 1, "Ritmo lento"),
        new("MODERADO", 0.5m, 1.0m, 2, 2, "Ritmo moderado"),
        new("AGRESIVO", 1.0m, null, 3, 3, "Ritmo agresivo"),
    ];

    [Theory]
    [InlineData(0.3, "LENTO")]
    [InlineData(0.5, "LENTO")]
    [InlineData(0.8, "MODERADO")]
    [InlineData(1.2, "AGRESIVO")]
    public void EvaluarRitmo_SeleccionaBandaCorrecta(decimal ritmo, string codigo) =>
        Assert.Equal(codigo, RiesgoEvaluator.EvaluarRitmo(ritmo, ReglasRitmo).ReglaAplicada.Codigo);

    [Fact]
    public void EvaluarRitmo_Negativo_Lanza() =>
        Assert.Throws<DomainException>(() => RiesgoEvaluator.EvaluarRitmo(-0.1m, ReglasRitmo));

    [Fact]
    public void EvaluarRitmo_SinBanda_Lanza() =>
        Assert.Throws<DomainException>(() => RiesgoEvaluator.EvaluarRitmo(0.3m,
            new[] { new ReglaRiesgoObjetivoDef("X", 1m, 2m, 1, 1, "x") }));

    private static readonly ReglaRiesgoMetaCaloricaDef[] ReglasMeta =
    [
        new("AGRESIVO", null, 500m, 1, 3, "Déficit excesivo"),
        new("MUY_BAJA_INGESTA", 1200m, null, 2, 3, "Meta por debajo del mínimo"),
        new("MODERADO", null, 1000m, 3, 2, "Déficit moderado"),
        new("BAJO", null, null, 4, 1, "Sin riesgo destacado"),
    ];

    [Fact]
    public void EvaluarMetaProporcionada_ConservaYEvaluaSinSobrescribir()
    {
        var r = RiesgoEvaluator.EvaluarMetaProporcionada(1900m, 2857.81m, ReglasMeta);
        Assert.Equal(1900m, r.MetaCalorica);
        Assert.Equal(957.81m, r.DeficitCalculado);
        Assert.True(r.PorcentajeDeficit > 0);
        Assert.Equal("AGRESIVO", r.ReglaAplicada.Codigo);
    }

    [Fact]
    public void MetaSinViolaciones_DevuelveBandaMasPermisiva()
    {
        var r = RiesgoEvaluator.EvaluarMetaProporcionada(2500m, 2857.81m, ReglasMeta);
        Assert.Equal("BAJO", r.ReglaAplicada.Codigo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void MetaInvalida_Lanza(decimal meta) =>
        Assert.Throws<DomainException>(() => RiesgoEvaluator.EvaluarMetaProporcionada(meta, 2500m, ReglasMeta));
}
