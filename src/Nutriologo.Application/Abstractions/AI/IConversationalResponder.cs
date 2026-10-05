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

/// <summary>
/// Evento del stream del respondedor.
/// <para><c>token</c>: fragmento tal cual lo emite el modelo (texto vivo).</para>
/// <para><c>done</c>: el stream terminó. <c>FinalText</c> es el texto ya validado
/// (o el fallback si las cifras no cuadraron), para reconciliar lo que se
/// transmitió token a token con lo que realmente se guarda y se muestra.</para>
/// <para><c>error</c>: el modelo falló; el llamador decide el destino.</para>
/// </summary>
public record ChatStreamEvent(
    string Type,            // "token" | "done" | "error"
    string? Text = null,    // fragmento (solo "token")
    bool Fallback = false,  // true si el texto final vino del fallback
    string? FinalText = null,
    string? Message = null
);

public interface IConversationalResponder
{
    Task<ConversationalResponse> ResponderAsync(ConversationalRequest request, CancellationToken ct = default);

    /// <summary>
    /// Versión en streaming: emite los tokens reales del modelo y cierra con un
    /// evento <c>done</c> que trae el texto final validado. No lanza por fallo de
    /// red: lo reporta como evento y devuelve el fallback, igual que la ruta
    /// sin streaming.
    /// </summary>
    IAsyncEnumerable<ChatStreamEvent> ResponderStreamAsync(ConversationalRequest request, CancellationToken ct = default);
}