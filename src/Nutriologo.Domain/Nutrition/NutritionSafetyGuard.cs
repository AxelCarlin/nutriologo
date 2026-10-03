namespace Nutriologo.Domain.Nutrition;

using Nutriologo.Domain.Exceptions;

/// <summary>
/// Guardas de seguridad. No diagnostican: solo impiden aplicar el algoritmo normal
/// de pérdida de peso en poblaciones excluidas.
/// </summary>
public static class NutritionSafetyGuard
{
    public static void EnsureAllowed(TipoObjetivo objetivo, int edadAnios, EstadoFisiologicoCodigo estado)
    {
        if (objetivo != TipoObjetivo.PerderPeso) return;

        if (edadAnios < 18)
            throw new DomainException("No se aplican objetivos de pérdida de peso a menores de 18 años.");

        if (estado is EstadoFisiologicoCodigo.Embarazo or EstadoFisiologicoCodigo.Lactancia)
            throw new DomainException("Embarazo/lactancia: no se aplica el algoritmo normal de pérdida de peso; requiere valoración profesional.");
    }
}
