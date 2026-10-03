namespace Nutriologo.Domain.Nutrition;

using Nutriologo.Domain.Exceptions;

/// <summary>
/// Mifflin-St Jeor: BMR = 10*peso(kg) + 6.25*altura(cm) - 5*edad + s (s = +5 masculino, -161 femenino).
/// TDEE = BMR * factorActividad. 1 kg ≈ 7700 kcal.
/// </summary>
public class MifflinStJeorCalorieCalculator : ICalorieCalculator
{
    public const decimal KcalPorKg = 7700m;

    public CalorieEstimate Estimate(CalorieInput input)
    {
        if (input.PesoKg <= 0) throw new DomainException("Peso debe ser positivo.");
        if (input.AlturaCm <= 0) throw new DomainException("Altura debe ser positiva.");
        if (input.EdadAnios <= 0) throw new DomainException("Edad debe ser positiva.");
        if (input.FactorActividad <= 0) throw new DomainException("Factor de actividad debe ser positivo.");

        decimal bmr = 10m * input.PesoKg + 6.25m * input.AlturaCm - 5m * input.EdadAnios
                      + (input.Sexo == SexoBiologico.Masculino ? 5m : -161m);
        decimal tdee = Math.Round(bmr * input.FactorActividad, 2);

        if (input.Objetivo == TipoObjetivo.MantenerPeso)
            return new CalorieEstimate(Math.Round(bmr, 2), tdee, 0m, 0m, 0m, 0m, tdee);

        if (input.PesoObjetivoKg is null || input.PesoObjetivoKg <= 0)
            throw new DomainException("Peso objetivo requerido para un objetivo de cambio de peso.");
        if (input.SemanasObjetivo is null || input.SemanasObjetivo <= 0)
            throw new DomainException("Semanas de objetivo deben ser positivas.");

        decimal delta = input.Objetivo == TipoObjetivo.PerderPeso
            ? input.PesoKg - input.PesoObjetivoKg.Value
            : input.PesoObjetivoKg.Value - input.PesoKg;

        if (delta <= 0)
            throw new DomainException("Objetivo de peso inconsistente con el tipo de objetivo.");

        decimal ritmo = Math.Round(delta / input.SemanasObjetivo.Value, 4);
        decimal porcentaje = Math.Round(delta / input.PesoKg * 100m, 4);
        decimal deficitDiario = Math.Round(delta * KcalPorKg / (input.SemanasObjetivo.Value * 7m), 2);
        if (input.Objetivo == TipoObjetivo.GanarPeso) deficitDiario = -deficitDiario; // superávit
        decimal meta = Math.Round(tdee - deficitDiario, 2);

        return new CalorieEstimate(Math.Round(bmr, 2), tdee, delta, ritmo, porcentaje, deficitDiario, meta);
    }
}
