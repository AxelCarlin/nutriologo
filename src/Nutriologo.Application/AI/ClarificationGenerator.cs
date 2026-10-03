namespace Nutriologo.Application.AI;

using Nutriologo.Application.Abstractions.AI;

/// <summary>Adaptador: transforma la decisión de ClarificationPolicy en pregunta estructurada.
/// No decide cuándo preguntar (eso es ClarificationPolicy); solo prioriza y redacta.</summary>
public class ClarificationGenerator : IClarificationGenerator
{
    public ClarificationDecision Decide(ChatInterpretation interpretation)
    {
        var decision = ClarificationPolicy.Decide(interpretation);
        if (!decision.RequiereAclaracion) return decision;

        // Prioriza la incertidumbre más material: alimento > preparación > cantidad > unidad.
        var candidato = interpretation.Candidatos.FirstOrDefault();
        if (candidato is null) return decision;

        var incert = candidato.Incertidumbres.FirstOrDefault();
        string motivo = "incertidumbre material";
        string? pregunta = candidato.NombreDetectado is { Length: > 0 } n
            ? $"Para '{n}', ¿me confirmas el alimento exacto o marca?"
            : "¿Qué alimento fue?";

        if (candidato.Preparacion is null)
        { pregunta = $"¿Cómo preparaste '{candidato.NombreDetectado}'? ¿Usaste aceite/grasa?"; motivo = "preparación desconocida"; }
        else if (candidato.Cantidad is null)
        { pregunta = $"¿Qué cantidad de '{candidato.NombreDetectado}' consumiste?"; motivo = "cantidad desconocida"; }
        else if (incert is not null)
        { pregunta = $"Para '{candidato.NombreDetectado}': {incert}"; motivo = incert; }

        return new ClarificationDecision(true, pregunta, motivo);
    }
}
