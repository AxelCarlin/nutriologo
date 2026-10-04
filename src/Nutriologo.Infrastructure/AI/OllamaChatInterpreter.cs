namespace Nutriologo.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.AI;

public class OllamaChatInterpreter : IChatInterpreter
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaChatInterpreter(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

    public const string SystemPrompt =
        "Eres un asistente de nutrición. Responde ÚNICAMENTE con JSON válido, sin texto adicional. " +
        "Esquema: {\"intencion\":string,\"requiere_aclaracion\":boolean,\"alimentos\":[{\"nombre\":string,\"cantidad\":number|null,\"unidad\":string|null,\"preparacion\":string|null,\"confianza\":number,\"incertidumbres\":[string]}]}. " +
        "Intención en: registrar_comida, registrar_medicion, crear_objetivo, consultar_balance, desconocida. " +
        "Solo usa nombres de alimentos del catálogo conocido (huevo, arroz, pollo, tortilla, frijoles, aguacate, aceite, salsa, agua, refresco). " +
        "Si el alimento no está en el catálogo o la información es insuficiente, pon requiere_aclaracion=true y describe la incertidumbre en incertidumbres. " +
        "Usa confianza en [0,1] y null cuando no se indique. Nunca inventes cantidades ni preparaciones.";

    public async Task<AiResult<ChatInterpretation>> InterpretAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            stream = false,
            format = "json",
            options = new { temperature = 0, num_ctx = 4096 },
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = request.Texto },
            },
        };

        var started = DateTime.UtcNow;
        using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        long evalNs = doc.RootElement.TryGetProperty("eval_duration", out var ed) && ed.ValueKind == JsonValueKind.Number
            ? ed.GetInt64() : 0;

        if (!StructuredOutputValidator.TryParse(content, out var parsed, out var error) || parsed is null)
            throw new Application.Contracts.AiGuardException($"Salida del modelo inválida: {error}. Raw: {content}");

        return new AiResult<ChatInterpretation>(parsed, _model, null,
            (long)(DateTime.UtcNow - started).TotalMilliseconds, content);
    }
}
