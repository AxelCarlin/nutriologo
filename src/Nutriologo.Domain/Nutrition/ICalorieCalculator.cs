namespace Nutriologo.Domain.Nutrition;

public enum SexoBiologico { Masculino, Femenino }

public enum TipoObjetivo { PerderPeso, MantenerPeso, GanarPeso }

public enum EstadoFisiologicoCodigo { Ninguno, Embarazo, Lactancia }

public record CalorieInput(
    decimal PesoKg,
    decimal AlturaCm,
    int EdadAnios,
    SexoBiologico Sexo,
    decimal FactorActividad,
    TipoObjetivo Objetivo,
    decimal? PesoObjetivoKg = null,
    decimal? SemanasObjetivo = null);

public record CalorieEstimate(
    decimal MetabolismoBasal,
    decimal GastoEnergeticoTotal,
    decimal? KgAjustar,
    decimal? RitmoSemanalKg,
    decimal? PorcentajeCambio,
    decimal? DeficitDiarioRequerido,
    decimal? MetaCaloricaEstimada);

public interface ICalorieCalculator
{
    CalorieEstimate Estimate(CalorieInput input);
}
