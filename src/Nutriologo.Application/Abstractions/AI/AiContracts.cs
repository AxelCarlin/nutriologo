namespace Nutriologo.Application.Abstractions.AI;

public enum ConfidenceLevel { Alta, Media, Baja, RequiereAclaracion }

public enum AiIntent { RegistrarComida, RegistrarMedicion, CrearObjetivo, ConsultarBalance, Desconocida }

/// <summary>Candidato estructurado propuesto por IA. Nunca es verdad numérica final.</summary>
public record FoodCandidate(
    string NombreDetectado,
    decimal? Cantidad,
    string? Unidad,
    string? Preparacion,
    decimal Confianza,
    IReadOnlyList<string> Incertidumbres);

public record ChatRequest(string Texto, int? IdUsuario = null);
public record ChatInterpretation(AiIntent Intent, IReadOnlyList<FoodCandidate> Candidatos, IReadOnlyList<string> Incertidumbres);

public record VisionRequest(string RutaImagen, string? MimeType = null);
public record VisionAnalysis(decimal ConfianzaGlobal, IReadOnlyList<FoodCandidate> Candidatos, string? RawJson = null);

public record AiResult<T>(T Value, string Modelo, string? Version, long DuracionMs, string? RawJson = null);

public interface IChatInterpreter
{
    Task<AiResult<ChatInterpretation>> InterpretAsync(ChatRequest request, CancellationToken ct = default);
}

public interface IVisionAnalyzer
{
    Task<AiResult<VisionAnalysis>> AnalyzeAsync(VisionRequest request, CancellationToken ct = default);
}

public record FoodCandidateInput(string Texto);
public record FoodResolution(IReadOnlyList<FoodCandidate> CandidatosResueltos);

public interface IFoodCandidateResolver
{
    Task<FoodResolution> ResolveAsync(FoodCandidateInput input, CancellationToken ct = default);
}

public record ClarificationDecision(bool RequiereAclaracion, string? Pregunta, string Motivo);

public interface IClarificationGenerator
{
    ClarificationDecision Decide(ChatInterpretation interpretation);
}
