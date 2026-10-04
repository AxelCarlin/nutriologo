namespace Nutriologo.Infrastructure.AI;

using System.Net.Http.Json;
using System.Text.Json;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.AI;
using Nutriologo.Application.Contracts;

/// <summary>Adapter de visión sobre Ollama. Aplica el mismo guard estructurado que chat.
/// Nota: en GTX 1070 los modelos probados no garantizan conformidad de esquema todavía (ver docs/benchmark-g8-vision.md).</summary>
public class OllamaVisionAnalyzer : IVisionAnalyzer
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaVisionAnalyzer(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

    public async Task<AiResult<VisionAnalysis>> AnalyzeAsync(VisionRequest request, CancellationToken ct = default)
    {
        byte[] bytes = await File.ReadAllBytesAsync(request.RutaImagen, ct);
        var imageB64 = Convert.ToBase64String(bytes);

        var payload = new
        {
            model = _model,
            stream = false,
            format = "json",
            options = new { temperature = 0, num_ctx = 4096 },
            messages = new[]
            {
                new { role = "user", content = "Analiza esta imagen de comida y devuelve SOLO el JSON del esquema.", images = new[] { imageB64 } },
            },
            system = OllamaChatInterpreter.SystemPrompt,
        };

        var started = DateTime.UtcNow;
        using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";

        if (!StructuredOutputValidator.TryParse(content, out var parsed, out var error) || parsed is null)
            throw new AiGuardException($"Salida de visión inválida: {error}. Raw: {content}");

        var candidatos = parsed.Candidatos;
        decimal confianzaGlobal = candidatos.Count == 0 ? 0m : candidatos.Average(c => c.Confianza);
        return new AiResult<VisionAnalysis>(new VisionAnalysis(confianzaGlobal, candidatos, content), _model, null,
            (long)(DateTime.UtcNow - started).TotalMilliseconds, content);
    }
}
