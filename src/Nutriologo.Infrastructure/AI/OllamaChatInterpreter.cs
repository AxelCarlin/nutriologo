namespace Nutriologo.Infrastructure.AI;

using System.Globalization;
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
        "Eres un asistente de nutrición. Responde ÚNICAMENTE con JSON válido que cumpla el esquema. " +
        "Intención (una de): registrar_comida, registrar_medicion, crear_objetivo, consultar_balance, listar_comidas, desconocida. " +
        "\"cuántas calorías llevo\" o \"cómo voy hoy\" es consultar_balance; \"qué comidas tengo registradas\" o \"qué comí hoy\" es listar_comidas; " +
        "en ambos casos responde requiere_aclaracion=false y alimentos=[]. " +
        "Saludos o charla sin contenido alimenticio es desconocida con requiere_aclaracion=true y alimentos=[]. " +
        "Solo usa nombres de alimentos del catálogo conocido (huevo, arroz, pollo, tortilla, frijoles, aguacate, aceite, salsa, agua, refresco). " +
        "Usa confianza en [0,1] y null cuando no se indique. Nunca inventes cantidades ni preparaciones. " +
        "Si no hay alimentos, usa \"alimentos\":[] (nunca null). " +
        "Cuando el usuario indique porciones ('dos huevos', 'tres tortillas', '150 gramos de arroz') completa SIEMPRE cantidad y unidad en cada alimento. " +
        "Si falta un dato (ej. dijo 'tortillas' sin cantidad), pon cantidad=null y deja que el sistema pregunte. " +
        // El bloque de estado es la parte que hace funcionar la referencia.
        // Sin él, el modelo de 7B responde 'desconocida' ante 'sí, la grande'.
        "<estado_actual>{estado}</estado_actual> " +
        "Si hay un estado con comida en curso, el mensaje del usuario probablemente responde a un slot pendiente; " +
        "usa los alimentos del estado y NO digas desconocida. Si no hay estado, ignóralo por completo.";

    /// <summary>
    /// JSON Schema estricto. `format: "json"` solo restringe la FORMA del JSON;
    /// el enum de intención queda abierto y el modelo devuelve cosas como
    /// "Registrar consumo alimenticio", que el validator mapea a Desconocida.
    /// El schema lo fuerza a nivel de logits, no por persuasión del prompt.
    /// </summary>
    public const string SchemaJson = """
    {
      "type": "object",
      "properties": {
        "intencion": {
          "type": "string",
          "enum": ["registrar_comida","registrar_medicion","crear_objetivo",
                   "consultar_balance","listar_comidas","desconocida"]
        },
        "requiere_aclaracion": { "type": "boolean" },
        "alimentos": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "nombre": { "type": "string" },
              "cantidad": { "type": ["number","null"] },
              "unidad": { "type": ["string","null"] },
              "preparacion": { "type": ["string","null"] },
              "confianza": { "type": "number" },
              "incertidumbres": { "type": "array", "items": { "type": "string" } }
            },
            "required": ["nombre","cantidad","unidad","confianza"]
          }
        }
      },
      "required": ["intencion","requiere_aclaracion","alimentos"]
    }
    """;

    /// <summary>
    /// Renderiza el draft activo como bloque estructurado para el prompt.
    /// Menos tokens que el historial crudo y con más señal.
    /// </summary>
    public static string RenderizarEstado(IReadOnlyList<(string Nombre, decimal? Cantidad, string? Unidad)> items)
    {
        if (items.Count == 0) return "sin comida en curso";
        var partes = items.Select(i =>
        {
            var cant = i.Cantidad is null ? "?" : i.Cantidad.Value.ToString("0.##", CultureInfo.InvariantCulture);
            var uni = i.Unidad ?? "?";
            return $"{i.Nombre} ({cant} {uni})";
        });
        return "comida en curso: " + string.Join(", ", partes);
    }

public async Task<AiResult<ChatInterpretation>> InterpretAsync(ChatRequest request, CancellationToken ct = default)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = SystemPrompt.Replace("{estado}",
                    RenderizarEstado(request.ComidaEnCurso ?? Array.Empty<(string, decimal?, string?)>()))
            }
        };
        // Historial recortado: más turnos empeoran el resultado (context rot).
        // El estado estructurado del system prompt es lo que aporta la memoria.
        // Memoria de corto plazo: últimos 2 turnos + el estado estructurado del
        // system prompt.
        //
        // DECISIÓN: ORDER BY + índice B-tree, NO pgvector/RAG sobre el historial.
        // Motivos, con datos de este proyecto:
        //   - Volumen real: 54 mensajes en 15 conversaciones (~3.6 por
        //     conversación). Un índice ANN sobre eso es más lento que un scan.
        //   - Semántica: "sí, la grande" no comparte tokens con "tortilla"; el
        //     cosine similarity da bajo, así que la búsqueda vectorial NO puede
        //     resolver coreferencia. RAG es la herramienta equivocada aquí.
        //   - pgvector hace búsqueda EXACTA por defecto (perfect recall); el
        //     índice HNSW solo aporta a escala.
        //   - Más historial EMPEORA el resultado (context rot): el modelo rinde
        //     mal cuando debe hacer retrieval y razonamiento en la misma llamada.
        // Revisar si: >50 mensajes por conversación Y usuarios simultáneos.
        if (request.Historial is { Count: > 0 })
            foreach (var t in request.Historial.TakeLast(2))
                messages.Add(new { role = t.Rol == "assistant" ? "assistant" : "user", content = t.Texto });
        messages.Add(new { role = "user", content = request.Texto });

        var payload = new
        {
            model = _model,
            stream = false,
            // Schema estricto, no "json": fuerza el enum de intención.
            format = System.Text.Json.JsonDocument.Parse(SchemaJson).RootElement,
            options = new { temperature = 0, num_ctx = 4096 },
            messages = messages.ToArray(),
        };

        var started = DateTime.UtcNow;
        // Endpoint de Ollama. Este cliente habla CON ollama, no contra sí mismo:
        // /api/conversacion/mensaje es del backend y devolvía 404 → EnsureSuccess
        // lanzaba → ChatEndpoints traducía a 503 "modelo ocupado".
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
