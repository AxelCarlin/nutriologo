namespace Nutriologo.Application.Abstractions.AI;

public static class ConfidenceClassifier
{
    // Umbrales por defecto; configurables al construir el servicio real.
    public const decimal UmbralAlta = 0.8m;
    public const decimal UmbralMedia = 0.5m;

    public static ConfidenceLevel Classify(decimal confianza, IReadOnlyCollection<string> incertidumbres)
    {
        if (confianza < 0 || confianza > 1)
            throw new Domain.Exceptions.DomainException("Confianza fuera de [0,1].");
        if (incertidumbres is { Count: > 0 }) return ConfidenceLevel.RequiereAclaracion;
        if (confianza >= UmbralAlta) return ConfidenceLevel.Alta;
        if (confianza >= UmbralMedia) return ConfidenceLevel.Media;
        return ConfidenceLevel.Baja;
    }
}

public static class ClarificationPolicy
{
    /// <summary>Pregunta solo si la incertidumbre puede cambiar materialmente
    /// calorías, alimento, cantidad o preparación.</summary>
    public static ClarificationDecision Decide(ChatInterpretation interp)
    {
        if (interp.Intent == AiIntent.Desconocida)
            return new(true, "¿Qué te gustaría registrar o consultar?", "intención desconocida");

        if (interp.Candidatos.Count == 0)
            return new(true, "No identifiqué alimentos. ¿Podrías describir lo que comiste?", "sin candidatos");

        var criticas = interp.Candidatos
            .Where(c => c.Incertidumbres.Count > 0 || c.Confianza < ConfidenceClassifier.UmbralMedia)
            .ToList();

        if (criticas.Count == 0)
            return new(false, null, "información suficiente");

        var primera = criticas[0];
        return new(true, $"Para '{primera.NombreDetectado}': ¿podrías precisar {(primera.Preparacion is null ? "la preparación (p.ej. con aceite)" : "la cantidad")}?",
            "incertidumbre material");
    }
}
