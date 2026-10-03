namespace Nutriologo.Domain.Nutrition;

/// <summary>
/// Bandas configurables (no hardcodeadas), equivalentes a ReglaRiesgoObjetivo.
/// Selección: reglas ordenadas por Orden; se elige la primera cuyo intervalo contenga el ritmo.
/// </summary>
public record ReglaRiesgoObjetivoDef(string Codigo, decimal? RitmoMinimo, decimal? RitmoMaximo, int Orden, int IdNivelRiesgo, string Descripcion);

public record EvaluacionRitmo(decimal RitmoSemanalKg, ReglaRiesgoObjetivoDef ReglaAplicada);

/// <summary>
/// Bandas configurables equivalentes a ReglaRiesgoMetaCalorica.
/// Semántica: una regla se VIOLA cuando la meta cae por debajo de CaloriasMinimas
/// o el déficit excede DeficitMaximo. Se selecciona la regla violada de menor Orden
/// (más severa primero). Si ninguna se viola, se devuelve la de mayor Orden
/// (banda más permisiva / sin riesgo destacado).
/// </summary>
public record ReglaRiesgoMetaCaloricaDef(string Codigo, decimal? CaloriasMinimas, decimal? DeficitMaximo, int Orden, int IdNivelRiesgo, string Descripcion);

public record EvaluacionMeta(decimal MetaCalorica, decimal Tdee, decimal DeficitCalculado, decimal PorcentajeDeficit, ReglaRiesgoMetaCaloricaDef ReglaAplicada);

public static class RiesgoEvaluator
{
    public static EvaluacionRitmo EvaluarRitmo(decimal ritmoSemanalKg, IEnumerable<ReglaRiesgoObjetivoDef> reglas)
    {
        if (ritmoSemanalKg < 0) throw new Exceptions.DomainException("Ritmo no puede ser negativo.");
        var regla = reglas.OrderBy(r => r.Orden).FirstOrDefault(r =>
            (r.RitmoMinimo is null || ritmoSemanalKg >= r.RitmoMinimo) &&
            (r.RitmoMaximo is null || ritmoSemanalKg <= r.RitmoMaximo));
        if (regla is null) throw new Exceptions.DomainException("Ninguna regla de riesgo aplica al ritmo calculado.");
        return new EvaluacionRitmo(ritmoSemanalKg, regla);
    }

    /// <summary>Evalúa una meta proporcionada sin modificarla.</summary>
    public static EvaluacionMeta EvaluarMetaProporcionada(decimal metaCalorica, decimal tdee, IEnumerable<ReglaRiesgoMetaCaloricaDef> reglas)
    {
        if (metaCalorica <= 0) throw new Exceptions.DomainException("Meta calórica debe ser positiva.");
        if (tdee <= 0) throw new Exceptions.DomainException("TDEE debe ser positivo.");
        decimal deficit = Math.Round(tdee - metaCalorica, 2);
        decimal porcentaje = Math.Round(deficit / tdee * 100m, 4);
        bool Violada(ReglaRiesgoMetaCaloricaDef r) =>
            (r.CaloriasMinimas is not null && metaCalorica < r.CaloriasMinimas) ||
            (r.DeficitMaximo is not null && deficit > r.DeficitMaximo);

        var regla = reglas.Where(Violada).OrderBy(r => r.Orden).FirstOrDefault()
                   ?? reglas.OrderByDescending(r => r.Orden).FirstOrDefault();
        if (regla is null) throw new Exceptions.DomainException("No hay reglas de riesgo configuradas.");
        return new EvaluacionMeta(metaCalorica, tdee, deficit, porcentaje, regla);
    }
}
