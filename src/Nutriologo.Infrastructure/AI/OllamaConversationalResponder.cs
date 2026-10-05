namespace Nutriologo.Infrastructure.AI;

using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.AI;

public sealed class OllamaConversationalResponder : IConversationalResponder
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaConversationalResponder>? _log;
    private readonly bool _isDevelopment;

    public OllamaConversationalResponder(
        HttpClient http,
        string model,
        IConfiguration configuration,
        ILogger<OllamaConversationalResponder>? log = null)
    {
        _http = http;
        _model = model;
        _log = log;
        _isDevelopment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development";
    }

    public const string ConversationalSystemPrompt = """
Eres el nutriólogo del usuario. Responde en español, tono cercano y profesional.

REGLAS OBLIGATORIAS:
- NUNCA calcules kcal, macros, riesgos, IMC, TMB, ritmos: usa SOLO los datos que te pasan en ContextoUsuario.
- Si falta un dato necesario → di DÓNDE está (ej: "Tu objetivo no está definido. Ve a /objetivo para calcularlo").
- Máximo 3 frases por respuesta. Una pregunta de seguimiento si procede.
- Usa cifras EXACTAS del contexto: "Llevas {kcal} kcal hoy de {meta} kcal de meta."
- NO inventes números. Si el contexto no tiene un dato, di que no lo tienes.
- Si el usuario pregunta fuera de nutrición → redirige amablemente: "Eso no lo puedo responder, pero te ayudo con tu alimentación."
- Si ContextoUsuario es null o incompleto → usa el fallback provisto por el sistema.

EJEMPLOS (placeholders, NO números reales):
Usuario: "¿Cómo voy hoy?"
Asistente: "Llevas {kcal} kcal de {meta} kcal hoy. Has comido {n_comidas} veces ({lista_comidas}). Tu meta es {tipo_objetivo} a {ritmo} kg/sem. ¿Registramos la cena?"

Usuario: "¿Qué ceno?"
Asistente: "Te quedan ~{restante} kcal para tu meta. Con tu objetivo de {tipo_objetivo}, prioriza proteína y vegetales. ¿Te apetece {sugerencia}?"

Usuario: "¿Cuánto peso?" (sin peso en contexto)
Asistente: "No tengo tu peso actual. Regístralo en /mediciones o actualiza tu perfil en /perfil."

Usuario: "Hola"
Asistente: "¡Hola! ¿En qué te ayudo con tu alimentación hoy?"
""";

    public async Task<ConversationalResponse> ResponderAsync(ConversationalRequest request, CancellationToken ct = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = ConversationalSystemPrompt }
        };

        // Contexto como bloque estructurado (no few-shot con números reales)
        if (request.Contexto != null)
        {
            var ctxBlock = RenderizarContexto(request.Contexto);
            messages.Add(new { role = "system", content = $"CONTEXTO_USUARIO:\n{ctxBlock}" });
        }
        else
        {
            messages.Add(new { role = "system", content = "CONTEXTO_USUARIO: sin datos disponibles. Usa fallback." });
        }

        // Historial: últimos 6 turnos (match frontend MAX_MENSAJES_HISTORIAL)
        if (request.Historial is { Count: > 0 })
        {
            foreach (var t in request.Historial.TakeLast(6))
                messages.Add(new { role = t.Rol == "assistant" ? "assistant" : "user", content = t.Texto });
        }

        messages.Add(new { role = "user", content = request.Texto });

        var payload = new
        {
            model = _model,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 512,
                num_ctx = 4096,
                repeat_penalty = 1.1
            },
            messages = messages.ToArray()
        };

        // Logging solo en Development
        if (_isDevelopment && _log != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                _log.LogInformation("OLLAMA CONVERSATIONAL PAYLOAD: {Payload}", json);
            }
            catch { /* ignore logging errors */ }
        }

        var started = DateTime.UtcNow;

        try
        {
            using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";

            // Validar cifras: todos los números del texto deben coincidir con el contexto (o ser enteros pequeños ≤20)
            var textoValidado = ValidarCifras(content, request.Contexto);
            if (textoValidado is null)
            {
                _log?.LogWarning("ValidarCifras falló: modelo inventó cifras. Usando fallback.");
                return Fallback(request.Contexto, request.Texto);
            }

            return new ConversationalResponse(textoValidado, UsaFallback: false);
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException)
        {
            _log?.LogWarning(ex, "OllamaConversationalResponder fallback: {Message}", ex.Message);
            return Fallback(request.Contexto, request.Texto);
        }
    }

    private static string RenderizarContexto(ContextoUsuario c)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"PERFIL: {(c.Perfil != null ? $"{c.Perfil.Edad}a {c.Perfil.Sexo} {c.Perfil.AlturaCm}cm {c.Perfil.Actividad}" : "sin perfil")}");
        sb.AppendLine($"OBJETIVO: {(c.Objetivo != null ? $"{c.Objetivo.Tipo} meta={c.Objetivo.MetaKcal}kcal ritmo={c.Objetivo.RitmoKgSem:0.##}kg/sem riesgo={c.Objetivo.Riesgo}" : "sin objetivo")}");
        sb.AppendLine($"HOY: {c.Hoy?.KcalConsumidas ?? 0}kcal{(c.Hoy?.MetaKcal != null ? $"/{c.Hoy.MetaKcal}kcal" : "")} macros P={c.Hoy?.Macros.ProteinaG ?? 0}g G={c.Hoy?.Macros.GrasaG ?? 0}g C={c.Hoy?.Macros.CarbohidratoG ?? 0}g comidas={c.Hoy?.Comidas.Length ?? 0}");
        if (c.Hoy?.Comidas is { Length: > 0 })
            foreach (var cm in c.Hoy.Comidas)
                sb.AppendLine($"  - {cm.Nombre} {cm.Kcal}kcal {cm.Hora}");
        sb.AppendLine($"PESO: {(c.Peso != null ? $"{c.Peso.UltimoKg:0.#}kg {c.Peso.Fecha} delta={c.Peso.DeltaKg?.ToString("0.#") ?? "n/a"}kg" : "sin peso")}");
        return sb.ToString();
    }

    // ---------- VALIDACIÓN NUMÉRICA ROBUSTA ----------

    private static string? ValidarCifras(string texto, ContextoUsuario? ctx)
    {
        if (ctx?.Hoy == null) return texto;

        var hoy = ctx.Hoy;
        var kcalReal = hoy.KcalConsumidas;
        var metaReal = hoy.MetaKcal ?? 0;

        // Conjunto de números PERMITIDOS en la respuesta (cifras del contexto + enteros pequeños ≤20)
        var permitidos = new HashSet<int>();
        permitidos.Add((int)kcalReal);
        if (metaReal > 0) permitidos.Add(metaReal);
        if (metaReal > 0) permitidos.Add(metaReal - (int)kcalReal); // restantes
        if (hoy.Comidas != null) permitidos.Add(hoy.Comidas.Length); // n_comidas
        if (hoy.Macros != null)
        {
            permitidos.Add(hoy.Macros.ProteinaG);
            permitidos.Add(hoy.Macros.GrasaG);
            permitidos.Add(hoy.Macros.CarbohidratoG);
        }
        if (hoy.Comidas != null)
            foreach (var cm in hoy.Comidas)
                permitidos.Add(cm.Kcal);
        if (ctx.Peso?.UltimoKg is { } pw) permitidos.Add((int)Math.Round(pw));
        if (ctx.Peso?.DeltaKg is { } dk) permitidos.Add((int)Math.Round(Math.Abs(dk)));

        // Enteros pequeños permitidos sin validar (artículos, cantidades típicas, porcentajes redondeados)
        for (int i = 1; i <= 20; i++) permitidos.Add(i);

        // Extraer TODOS los números del texto (enteros y decimales)
        var numerosTexto = ExtraerNumeros(texto);

        foreach (var num in numerosTexto)
        {
            // Si es entero pequeño (≤20) → permitido (artículos, cantidades, %)
            if (num <= 20 && num == Math.Floor(num)) continue;

            // Si está en el conjunto de permitidos del contexto → OK
            if (permitidos.Contains((int)Math.Round(num))) continue;

            // Si es decimal (p.ej. peso 113.7), comparar contra el peso real ±0.5
            if (num % 1 != 0)
            {
                var pesoRef = ctx.Peso?.UltimoKg;
                if (pesoRef.HasValue && Math.Abs(num - (double)pesoRef.Value) < 0.5) continue;
            }

            // NO está en permitidos → el modelo inventó una cifra → RECHAZAR
            return null;
        }

        return texto; // OK
    }

    private static double[] ExtraerNumeros(string texto)
    {
        // Regex que captura: enteros con separadores de miles (1,240 / 1.240 / 1 240),
        // miles múltiples (1,234,567), decimales (113.7 / 113,7 / 0.5)
        var matches = Regex.Matches(
            texto,
            // Orden importa: miles y decimales primero; \d+ simple al final
            // para enteros sin separador ("1240"). Sin esa rama, los enteros
            // de 4+ dígitos no matcheaban y el validador los ignoraba.
            @"(?<!\w)(?:\d{1,3}(?:[., ]\d{3})*(?:[.,]\d+)?|\d+[.,]\d+|\d+)(?!\w)"
        );

        var resultados = new List<double>();
        foreach (Match m in matches)
        {
            var s = m.Value.Replace(" ", "").Replace("_", "");
            string limpio;
            // Múltiples grupos de miles: 1,234,567 / 1.234.567 → quitar todos
            if (Regex.IsMatch(s, @"^\d{1,3}([.,]\d{3})+$"))
            {
                limpio = Regex.Replace(s, @"[.,]", "");
            }
            else
            {
                // Heurística: un solo separador con exactamente 3 dígitos después
                // y sin el otro separador → miles; sino es decimal
                var partes = s.Split(new[] { ',', '.' }, StringSplitOptions.None);
                if (partes.Length == 2 && partes[1].Length == 3 && !s.Contains("."))
                    limpio = s.Replace(",", ""); // "1,240"
                else if (partes.Length == 2 && partes[1].Length == 3 && !s.Contains(","))
                    limpio = s.Replace(".", ""); // "1.240"
                else
                    limpio = s.Replace(",", "."); // decimal con coma → punto
            }

            if (double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                resultados.Add(val);
        }
        return resultados.ToArray();
    }
    // ---------- FIN VALIDACIÓN ----------

    private static ConversationalResponse Fallback(ContextoUsuario? ctx, string userText)
    {
        var t = userText.ToLowerInvariant();

        if (t.Contains("calor") || t.Contains("cómo voy") || t.Contains("balance"))
        {
            var kcal = ctx?.Hoy?.KcalConsumidas ?? 0;
            var meta = ctx?.Hoy?.MetaKcal;
            var txt = kcal > 0
                ? meta.HasValue
                    ? $"Llevas {kcal:N0} kcal de {meta.Value:N0} kcal hoy."
                    : $"Llevas {kcal:N0} kcal registradas hoy (sin meta definida)."
                : "Aún no tienes comidas registradas hoy.";
            return new ConversationalResponse(txt, UsaFallback: true);
        }

        if (t.Contains("comida") || t.Contains("qué comí") || t.Contains("registrad"))
        {
            var comidas = ctx?.Hoy?.Comidas ?? Array.Empty<ComidaResumen>();
            var txt = comidas.Length == 0
                ? "Aún no tienes comidas registradas hoy."
                : "Hoy registraste: " + string.Join("; ", comidas.Select((c, i) => $"{i + 1}. {c.Nombre} ({c.Kcal:N0} kcal)"));
            return new ConversationalResponse(txt, UsaFallback: true);
        }

        return new ConversationalResponse("¿Qué te gustaría registrar o consultar?", UsaFallback: true);
    }
}