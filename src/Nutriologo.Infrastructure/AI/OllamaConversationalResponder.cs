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
Eres el nutriólogo del usuario. Respondes en español de España, en TUTEO, y con las
frases en mayúscula normal. Tono cercano y profesional.

REGLAS OBLIGATORIAS:
- HABLA EN TUTEO. Formas prohibidas: contame, preguntame, podés, querés, tenés, revisá, guardá, indicá, llevás, escribís, pensás, priorizá. No escribas ninguna, ni en una respuesta ni en un ejemplo.
- NUNCA calcules kcal, macros, riesgos, IMC, TMB, ritmos: usa SOLO los datos que te pasan en ContextoUsuario.
- NUNCA digas que registraste, guardaste o anotaste algo. NO TIENES esa capacidad: no escribes en la base.
- CONTESTAS DE FRENTE, sin relleno. Nada de "¡Excelente pregunta!", "¡Vamos allá!",
  "para mantenerte en el camino correcto", "espero que te sirva" ni comentarios de
  ánimo al final.
  La última frase NO es una pregunta de cortesía: si te falta un dato, pregunta por el
  dato ("¿Cuánto comiste?"); si no te falta nada, termina en la respuesta.
- Cuando el usuario tenga que hacer algo en otra pantalla, NÓMBRALA con su ruta exacta
  (/registrar, /mediciones, /objetivo, /perfil). La interfaz convierte esa ruta en un
  botón: sin la ruta no hay botón y la respuesta queda muerta.
- Máximo 3 frases por respuesta. Una pregunta de seguimiento como máximo, y solo si hace falta.
- Usa cifras EXACTAS del contexto: "Llevas {kcal} kcal hoy de {meta} kcal de meta."
- NO inventes números. Si el contexto no tiene un dato, di que no lo tienes.
- Si el usuario pregunta fuera de nutrición → redirige amablemente: "Eso no lo puedo responder, pero te ayudo con tu alimentación."
- Si ContextoUsuario es null o incompleto → usa el fallback provisto por el sistema.

VISTAS DONDE EL USUARIO CARGA SUS DATOS (nómbralas cuando le pidas guardar algo):
/registrar (comidas) · /mediciones (peso y medidas) · /objetivo (meta calórica) · /perfil (datos personales)

EJEMPLOS (placeholders, NO números reales):
Usuario: "¿Cómo voy hoy?"
Asistente: "Llevas {kcal} kcal hoy de {meta} kcal de meta, en {n_comidas} comidas ({lista_comidas}). Tu meta es {tipo_objetivo}."

Usuario: "¿Qué ceno?"
Asistente: "Te quedan {restante} kcal. Con tu objetivo de {tipo_objetivo}, prioriza proteína y verduras: {sugerencia}."

Usuario: "Registré el desayuno" (NO lo hizo: el chat nunca escribe)
Asistente: "No puedo guardar registros desde aquí; esa carga se hace en tu pantalla. Si me dices qué comiste, te armo la lista y la llevo cargada a /registrar."

Usuario: "¿Cuánto peso?" (sin peso en contexto)
Asistente: "No tengo tu peso actual. Regístralo en /mediciones o actualiza tu perfil en /perfil."

Usuario: "Hola"
Asistente: "Hola. Dime qué comiste o qué quieres consultar."
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
        catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException || ex is JsonException || ex is OperationCanceledException)
        {
            _log?.LogWarning(ex, "OllamaConversationalResponder fallback: {Message}", ex.Message);
            return Fallback(request.Contexto, request.Texto);
        }
        catch (Exception ex)
        {
            // Cualquier otra excepción de red/timeout → fallback (no 500)
            _log?.LogWarning(ex, "OllamaConversationalResponder fallback (otro): {Message}", ex.Message);
            return Fallback(request.Contexto, request.Texto);
        }
    }

    /// <summary>
    /// Stream real de Ollama. Los <c>token</c> son tal cual los emite el modelo;
    /// el cierre trae <c>FinalText</c> ya validado (o el fallback) para que el
    /// cliente pueda reconciliar. Un corte a mitad no lanza: se reporta y se cae
    /// al fallback, igual que la ruta sin streaming.
    /// </summary>
    public async IAsyncEnumerable<ChatStreamEvent> ResponderStreamAsync(
        ConversationalRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var acumulado = new System.Text.StringBuilder();
        var enumerador = EmitirTokensAsync(request, acumulado, ct).GetAsyncEnumerator(ct);
        string? fallo = null;

        try
        {
            while (true)
            {
                ChatStreamEvent? ev = null;
                var avanzo = false;

                try
                {
                    avanzo = await enumerador.MoveNextAsync();
                    if (avanzo) ev = enumerador.Current;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log?.LogWarning(ex, "Stream del respondedor cortado: {Message}", ex.Message);
                    fallo = ex.Message;
                }

                if (avanzo && ev is not null)
                    yield return ev;

                if (!avanzo || fallo is not null)
                    break;
            }
        }
        finally
        {
            await enumerador.DisposeAsync();
        }

        if (ct.IsCancellationRequested)
            yield break;

        // El texto ya salir token a token se re-valida entero: si el modelo
        // inventó cifras, lo que se transmitió se descarta y manda el fallback.
        var completo = acumulado.ToString();
        var validado = fallo is null ? ValidarCifras(completo, request.Contexto) : null;

        if (fallo is not null)
        {
            _log?.LogWarning(fallo, "Respondedor en streaming: fallo de transporte, va fallback");
            var fb = Fallback(request.Contexto, request.Texto);
            yield return new ChatStreamEvent("done", Fallback: true, FinalText: fb.Texto, Message: fallo);
            yield break;
        }

        if (validado is null)
        {
            _log?.LogWarning("ValidarCifras falló en streaming: el modelo inventó cifras. Va fallback.");
            var fb = Fallback(request.Contexto, request.Texto);
            yield return new ChatStreamEvent("done", Fallback: true, FinalText: fb.Texto);
            yield break;
        }

        yield return new ChatStreamEvent("done", Fallback: false, FinalText: validado);
    }

    /// <summary>
    /// Enumerador interno: solo emite tokens. Sin <c>catch</c> a propósito,
    /// porque C# no permite <c>yield</c> dentro de un try con catch; el manejo de
    /// errores vive en <see cref="ResponderStreamAsync"/>, que envuelve a este.
    /// </summary>
    private async IAsyncEnumerable<ChatStreamEvent> EmitirTokensAsync(
        ConversationalRequest request,
        System.Text.StringBuilder acumulado,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new List<object>
        {
            new { role = "system", content = ConversationalSystemPrompt }
        };

        if (request.Contexto != null)
            messages.Add(new { role = "system", content = $"CONTEXTO_USUARIO:\n{RenderizarContexto(request.Contexto)}" });
        else
            messages.Add(new { role = "system", content = "CONTEXTO_USUARIO: sin datos disponibles. Usa fallback." });

        if (request.Historial is { Count: > 0 })
            foreach (var t in request.Historial.TakeLast(6))
                messages.Add(new { role = t.Rol == "assistant" ? "assistant" : "user", content = t.Texto });

        messages.Add(new { role = "user", content = request.Texto });

        var payload = new
        {
            model = _model,
            stream = true,
            options = new
            {
                temperature = 0.7,
                num_predict = 512,
                num_ctx = 4096,
                repeat_penalty = 1.1
            },
            messages = messages.ToArray()
        };

        if (_isDevelopment && _log != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                _log.LogInformation("OLLAMA CONVERSATIONAL STREAM PAYLOAD: {Payload}", json);
            }
            catch { /* ignore logging errors */ }
        }

        using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            // El parseo vive fuera del bucle: `yield` no puede estar dentro de
            // un try con catch (CS1626).
            var leido = LeerLineaStream(line, out var token);
            if (leido == LineaStream.Fin) yield break;

            if (leido == LineaStream.Token && token is { Length: > 0 })
            {
                acumulado.Append(token);
                yield return new ChatStreamEvent("token", Text: token);
            }
        }
    }

    private enum LineaStream { Vacia, Token, Fin }

    /// <summary>
    /// Lee una línea NDJSON de /api/chat. No lanza: una línea corrupta se
    /// clasifica como <see cref="LineaStream.Vacia"/> y el stream sigue.
    /// </summary>
    private static LineaStream LeerLineaStream(string line, out string? token)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (root.TryGetProperty("done", out var doneProp) && doneProp.ValueKind == JsonValueKind.True)
            {
                token = null;
                return LineaStream.Fin;
            }

            if (root.TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var contentProp))
            {
                token = contentProp.GetString();
                return LineaStream.Token;
            }
        }
        catch (JsonException)
        {
            // Línea corrupta: se ignora, no se aborta el stream.
        }

        token = null;
        return LineaStream.Vacia;
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

        // Planes del usuario: lo que el bot puede analizar. No puede crearlos
        // ni modificarlos, así que van marcados como solo lectura.
        if (c.Planes is { Length: > 0 })
        {
            foreach (var p in c.Planes)
            {
                sb.AppendLine($"PLAN ({p.Estado}, {p.Dias} dias, solo lectura): {p.Nombre}");
                foreach (var cp in p.Comidas)
                    sb.AppendLine($"  - {cp.Momento}: {cp.Alimento} {Num(cp.Cantidad)}{cp.Unidad}");
            }
        }
        else
        {
            sb.AppendLine("PLAN: sin planes alimentarios.");
        }

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

        // Planes: cantidades y días del plan son cifras reales del contexto.
        // Sin esto, responder "tu plan tiene 150g de arroz" caería en fallback
        // por un número que SÍ está medido.
        if (ctx.Planes is { Length: > 0 })
            foreach (var p in ctx.Planes)
            {
                if (p.Dias > 0) permitidos.Add(p.Dias);
                foreach (var cp in p.Comidas)
                    permitidos.Add((int)Math.Round(cp.Cantidad));
            }

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

    /// <summary>
    /// Formatea un número a la española sin depender de la cultura del
    /// contenedor: miles con punto, decimales con coma.
    /// Antes usaba "N0" con la cultura del proceso, y 4035 kcal salían como
    /// "4,035", que se lee como cuatro mil treinta y cinco coma cero.
    /// </summary>
    private static string Num(decimal v, int decimales = 0)
    {
        var negativo = v < 0;
        var abs = Math.Abs(v);
        var entero = (long)Math.Floor(abs);
        var frac = abs - entero;

        var miles = entero.ToString(CultureInfo.InvariantCulture);
        var salida = string.Empty;
        while (miles.Length > 3)
        {
            salida = "." + miles[^3..] + salida;
            miles = miles[..^3];
        }
        salida = miles + salida;

        if (decimales > 0)
        {
            var d = frac.ToString("F" + decimales, CultureInfo.InvariantCulture)[2..];
            salida += "," + d;
        }

        return (negativo ? "-" : "") + salida;
    }

    /// <summary>
    /// Respuesta de respaldo cuando el modelo no está disponible o inventó cifras.
    /// Regla: solo afirma lo que está medido en el contexto, y lo dice sin rodeos.
    /// Antes devolvía siempre "¿Qué te gustaría registrar o consultar?", que no
    /// respondía nada: el usuario veía la misma frase para cualquier pregunta.
    /// <para>Tuteo, sin voseo, y con la ruta de la vista nombrada cuando hay que
    /// ir a otro lado: el cliente saca de ahí el botón.</para>
    /// </summary>
    private static ConversationalResponse Fallback(ContextoUsuario? ctx, string userText)
    {
        var t = userText.ToLowerInvariant().Trim();

        // Saludos y despedidas: no necesitan datos.
        if (EsSaludo(t))
            return new ConversationalResponse("Hola. Cuéntame qué comiste o qué quieres consultar.", UsaFallback: true);
        if (EsDespedida(t))
            return new ConversationalResponse("Hasta luego.", UsaFallback: true);

        var peso = ctx?.Peso;
        var objetivo = ctx?.Objetivo;
        var hoy = ctx?.Hoy;

        // Peso.
        if (t.Contains("peso") || t.Contains("cuánto pes") || t.Contains("cuanto pes"))
        {
            if (peso == null)
                return new ConversationalResponse("No tengo mediciones de tu peso todavía. Regístralas en /mediciones.", UsaFallback: true);
            var delta = peso.DeltaKg is null
                ? "no tengo una medición anterior para comparar."
                : $"{(peso.DeltaKg.Value >= 0 ? "+" : "")}{Num(peso.DeltaKg.Value, 1)} kg respecto de la anterior.";
            return new ConversationalResponse($"Tu último peso es {Num(peso.UltimoKg, 1)} kg ({peso.Fecha}); {delta}", UsaFallback: true);
        }

        // Objetivo.
        if (t.Contains("objetivo") || t.Contains("meta"))
        {
            if (objetivo == null)
                return new ConversationalResponse("No tienes una meta calórica definida. Calcúlala en /objetivo.", UsaFallback: true);
            return new ConversationalResponse($"Tu objetivo es {objetivo.Tipo.ToLowerInvariant()} en {Num(objetivo.MetaKcal)} kcal diarias. Riesgo evaluado: {objetivo.Riesgo.ToLowerInvariant()}.", UsaFallback: true);
        }

        // Lista de comidas registradas hoy.
        if (t.Contains("qué com") || t.Contains("que com") || t.Contains("comidas tengo")
            || t.Contains("registrad") || t.Contains("qué hay") || t.Contains("que hay"))
        {
            var comidas = hoy?.Comidas;
            if (comidas == null || comidas.Length == 0)
                return new ConversationalResponse("Hoy todavía no tienes comidas registradas.", UsaFallback: true);

            // Con muchas comidas se listan las últimas: volcar las 17 del día
            // no es una respuesta, es un volcado.
            var ultimas = comidas.Reverse().Take(4).Reverse().ToArray();
            var detalle = string.Join("; ", ultimas.Select(c => $"{c.Nombre} a las {c.Hora} ({Num(c.Kcal)} kcal)"));
            var resto = comidas.Length - ultimas.Length;
            var extra = resto > 0 ? $" Son {comidas.Length} en total." : "";
            return new ConversationalResponse($"Hoy registraste {comidas.Length} {(comidas.Length == 1 ? "comida" : "comidas")}: {detalle}.{extra}", UsaFallback: true);
        }

        // Balance / "cómo voy": el caso más consultado, con kcal y macros reales.
        if (t.Contains("calor") || t.Contains("cómo voy") || t.Contains("como voy")
            || t.Contains("balance") || t.Contains("estado") || t.Contains("resumen")
            || t.Contains("qué tal") || t.Contains("que tal") || t.Contains("cómo vengo"))
        {
            if (hoy == null || (hoy.KcalConsumidas == 0 && (hoy.Comidas?.Length ?? 0) == 0))
                return new ConversationalResponse("Hoy todavía no tienes comidas registradas, así que no hay balance que mostrar.", UsaFallback: true);

            var kcal = hoy.KcalConsumidas;
            var m = hoy.Macros;
            var cabecera = hoy.MetaKcal is { } meta
                ? $"Llevas {Num(kcal)} de {Num(meta)} kcal hoy"
                : $"Llevas {Num(kcal)} kcal hoy y no tienes meta definida";
            var quedan = hoy.MetaKcal is { } mk ? $" Te quedan {Num(Math.Max(0, mk - kcal))} kcal." : "";
            var macros = $" Proteínas {Num(m.ProteinaG)} g, grasas {Num(m.GrasaG)} g, carbohidratos {Num(m.CarbohidratoG)} g.";
            return new ConversationalResponse(cabecera + "." + quedan + macros, UsaFallback: true);
        }

        // Cualquier otra consulta: se dice qué se SÍ puede responder, sin inventar.
        var opciones = new List<string>();
        if (hoy is { KcalConsumidas: > 0 }) opciones.Add("tu balance de hoy");
        if (peso != null) opciones.Add("tu peso");
        if (objetivo != null) opciones.Add("tu objetivo");
        if (hoy?.Comidas is { Length: > 0 }) opciones.Add("las comidas que registraste hoy");

        var cierre = opciones.Count > 0
            ? "Ahora mismo puedo responderte por " + string.Join(", ", opciones) + "."
            : "Todavía no tienes datos registrados, así que no hay nada que resumir.";

        return new ConversationalResponse($"No pude responder eso con detalle.{cierre}", UsaFallback: true);
    }

    private static bool EsSaludo(string t) =>
        t.StartsWith("hola") || t.StartsWith("buenos") || t.StartsWith("buenas")
        || t.StartsWith("hey") || t.StartsWith("qué tal");

    private static bool EsDespedida(string t) =>
        t.StartsWith("adiós") || t.StartsWith("adios") || t.StartsWith("hasta")
        || t.StartsWith("chao") || t.StartsWith("bye") || t.StartsWith("nos vemos");
}