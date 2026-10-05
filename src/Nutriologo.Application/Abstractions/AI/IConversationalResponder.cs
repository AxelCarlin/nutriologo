namespace Nutriologo.Application.Abstractions.AI;

public record ConversationalRequest(
    string Texto,
    IReadOnlyList<ChatTurn>? Historial = null,   // últimos 6 turnos
    ContextoUsuario? Contexto = null
);

public record ConversationalResponse(
    string Texto,
    bool UsaFallback   // true si valió fallback por timeout/error/validación
);

public interface IConversationalResponder
{
    Task<ConversationalResponse> ResponderAsync(ConversationalRequest request, CancellationToken ct = default);
}