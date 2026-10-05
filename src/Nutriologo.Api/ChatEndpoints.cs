using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nutriologo.Api.Auth;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Infrastructure.Conversations;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

/// <summary>El modelo (Ollama) no respondió. El pipeline la traduce a 503 en JSON o a `error` en SSE.</summary>
public sealed class ModeloNoDisponibleException(string mensaje) : Exception(mensaje);

/// <summary>
/// Un alimento que el chat entendió y entrega para que el usuario lo cargue
/// él mismo. El chat no escribe nada: solo arma esto y lo pasa.
/// </summary>
public record ItemRedirigido(string Nombre, decimal Cantidad, string Unidad);

/// <summary>
/// A dónde va el usuario y con qué datos. El frontend la traduce en un botón.
/// <para><c>Vista</c> es una ruta de la SPA, no una URL absoluta: el cliente
/// decide cómo navegar.</para>
/// </summary>
public record RedireccionDestino(
    string Vista,
    string Etiqueta,
    IReadOnlyList<ItemRedirigido> Items);

/// <summary>
/// Resultado de un turno. Un único shape: lo consume el endpoint JSON y también
/// el evento `result` del stream SSE, para que no haya dos verdades que divergir.
/// <para>Las propiedades nulas se omiten: el contrato histórico devolvía solo
/// las claves del caso (<c>aclaracion</c> sin <c>id</c>, <c>respuesta</c> sin
/// <c>candidatos</c>) y el cliente no debe empezar a leer campos fantasma.</para>
/// </summary>
public record ChatTurnResult(
    string Tipo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Texto = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pregunta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Motivo = null,
    bool Fallback = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FoodCandidate>? Candidatos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RedireccionDestino? Redireccion = null);

/// <summary>
/// Reporte de trabajo REAL del pipeline. Cada <c>PasoAsync</c> corresponde a una
/// operación que el código está ejecutando de verdad (leer el draft, llamar al
/// intérprete, hidratar contexto, escribir en BD, redactar). No hay temporizadores
/// ni pasos guionizados: si la fase no se ejecutó, no se emite.
/// </summary>
public interface IChatProgress
{
    Task PasoAsync(string nombre, string estado, string? detalle = null);
    Task TokenAsync(string texto);
}

/// <summary>No-op: el endpoint JSON no necesita reportar progreso.</summary>
public sealed class SinProgreso : IChatProgress
{
    public static readonly SinProgreso Instancia = new();
    public Task PasoAsync(string nombre, string estado, string? detalle = null) => Task.CompletedTask;
    public Task TokenAsync(string texto) => Task.CompletedTask;
}

public static class ChatEndpoints
{
    public record TurnoRequest(string Rol, string Texto);
    public record MensajeRequest(string Texto, IReadOnlyList<TurnoRequest>? Historial = null);

    public static void MapChat(this WebApplication app)
    {
        // ============ Endpoint JSON (conservado: lo usan tests y la suite) ============
        app.MapPost("/api/conversacion/mensaje", async (MensajeRequest r, ClaimsPrincipal user, IChatInterpreter interpreter,
            IConversationalResponder conversacional, AppDbContext db, IFoodCatalogSource catalog, ChatTurnService turnos) =>
        {
            var idUsuario = JwtIssuer.UserId(user);

            try
            {
                var res = await EjecutarTurnoAsync(r, idUsuario, interpreter, conversacional, db, turnos,
                    SinProgreso.Instancia, CancellationToken.None);
                return Results.Ok(res);
            }
            catch (ModeloNoDisponibleException ex)
            {
                return Results.Problem(ex.Message, statusCode: 503);
            }
        }).RequireRateLimiting("business").RequireAuthorization();

        // ============ Endpoint SSE: mismo pipeline, con fases y tokens reales ============
        app.MapPost("/api/conversacion/stream", async (MensajeRequest r, ClaimsPrincipal user, IChatInterpreter interpreter,
            IConversationalResponder conversacional, AppDbContext db, IFoodCatalogSource catalog, ChatTurnService turnos,
            IOptions<JsonOptions> jsonOptions, HttpContext http) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var ct = http.RequestAborted;
            var reloj = Stopwatch.StartNew();

            http.Response.StatusCode = 200;
            http.Response.ContentType = "text/event-stream; charset=utf-8";
            http.Response.Headers.CacheControl = "no-cache, no-store";
            http.Response.Headers["X-Accel-Buffering"] = "no";

            // Se usan las opciones de JSON de la app: `JsonSerializer.Serialize`
            // con las opciones por defecto escribe PascalCase y el cliente
            // leería `undefined` en todos los campos.
            var sink = new SseProgress(http.Response, ct, jsonOptions.Value.SerializerOptions);

            try
            {
                var res = await EjecutarTurnoAsync(r, idUsuario, interpreter, conversacional, db, turnos, sink, ct);
                await sink.EnviarAsync("result", res);
                await sink.EnviarAsync("done", new { ms = reloj.ElapsedMilliseconds });
            }
            catch (OperationCanceledException)
            {
                // El cliente abortó (botón Detener / Esc). No es un error: no se
                // escribe nada porque el socket ya está cerrado.
            }
            catch (ModeloNoDisponibleException ex)
            {
                await sink.EnviarAsync("error", new { message = ex.Message });
                await sink.EnviarAsync("done", new { ms = reloj.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                await sink.EnviarAsync("error", new
                {
                    message = "No pude completar la petición. Inténtalo de nuevo en un momento.",
                    detalle = ex.Message
                });
                await sink.EnviarAsync("done", new { ms = reloj.ElapsedMilliseconds });
            }
        }).RequireRateLimiting("business").RequireAuthorization();
    }

    /// <summary>
    /// Pipeline de un turno. Es la ÚNICA implementación: el endpoint JSON la
    /// llama sin reporte, el SSE con reporte de fases y tokens. Así el chat
    /// web y la suite de tests ejecutan exactamente el mismo código.
    /// </summary>
    private static async Task<ChatTurnResult> EjecutarTurnoAsync(
        MensajeRequest r,
        int idUsuario,
        IChatInterpreter interpreter,
        IConversationalResponder conversacional,
        AppDbContext db,
        ChatTurnService turnos,
        IChatProgress progreso,
        CancellationToken ct)
    {
        // ============ 0. SLOT FILLING DETERMINISTA ============
        // Se intenta ANTES de llamar al LLM. Un mensaje corto ("sí, la
        // grande", "50ml") lo resuelve la tabla de patches en C#: un 7B en
        // CPU no resuelve esa coreferencia de forma fiable, y wasted una
        // inferencia de ~14 s en intentarlo.
        await progreso.PasoAsync("lectura_turno", "start", "Revisando tu conversación");
        var avance = await turnos.IntentarAvanzarAsync(idUsuario, r.Texto);
        await progreso.PasoAsync("lectura_turno", "ok", "Revisando tu conversación");

        // Cancelación: no es una aclaración ni un error, es un cierre explícito.
        if (avance is { Completo: false } && avance.Estado.Estado == EstadoSlot.Cancelado)
        {
            var texto = avance.Pregunta ?? "Ok, lo dejamos así.";
            var conv = await turnos.ObtenerConversacionAsync(idUsuario);
            await turnos.GuardarMensajeAsync(conv, true, r.Texto, idTipo: 1);
            await turnos.GuardarMensajeAsync(conv, false, texto, idTipo: 1);
            return new ChatTurnResult("respuesta", Texto: texto);
        }

        if (avance is not null && !avance.Completo)
        {
            await turnos.GuardarMensajeAsync(
                await turnos.ObtenerConversacionAsync(idUsuario), true, r.Texto, idTipo: 1);
            await turnos.GuardarMensajeAsync(
                await turnos.ObtenerConversacionAsync(idUsuario), false,
                avance.Pregunta ?? "¿Puedes precisar?", idTipo: 2);
            return new ChatTurnResult("aclaracion", Pregunta: avance.Pregunta, Motivo: "slot pendiente");
        }

        if (avance is { Completo: true })
        {
            var conversacion = await turnos.ObtenerConversacionAsync(idUsuario);
            await turnos.GuardarMensajeAsync(conversacion, true, r.Texto, idTipo: 1);

            // Tras resolver el patch la cantidad existe pero la unidad puede
            // seguir abierta: se completa con la porción del catálogo antes de
            // armar la redirección, para no exigir al usuario una unidad que ya
            // se sabe.
            await turnos.CompletarUnidadesDesdeCatalogoAsync(avance.Estado);

            var itemsListos = MealSlotTracker.Listos(avance.Estado)
                .Select(i => new ItemRedirigido(i.Nombre, i.Cantidad, i.Unidad))
                .ToList();

            // El chat NO escribe: el borrador se limpia y el usuario revisa y
            // guarda en la vista de Registro con los datos ya cargados.
            await turnos.LimpiarDraftAsync(conversacion);
            return await CerrarConRedireccionAsync(turnos, idUsuario, itemsListos,
                "Listo. Te llevé los datos al formulario: revisá y guardá vos.");
        }

        // ============ 1. INTÉRPRETE LLM: clasifica intención + extrae candidatos ============
        // Historial: 2 turnos (match intérprete; el estado estructurado da la memoria).
        // El draft se lee UNA vez acá y se reutiliza abajo (sin segunda llamada a BD).
        await progreso.PasoAsync("interpretacion", "start", "Interpretando tu mensaje");
        var draftActual = await turnos.LeerDraftAsync(await turnos.ObtenerConversacionAsync(idUsuario));
        var estadoPrompt = draftActual.Items
            .Select(i => (i.Nombre, i.Cantidad, i.Unidad)).ToList();

        AiResult<ChatInterpretation> resultado;
        try
        {
            var historialInterprete = r.Historial?.TakeLast(2)
                .Select(t => new ChatTurn(t.Rol, t.Texto)).ToList();

            resultado = await interpreter.InterpretAsync(
                new ChatRequest(r.Texto, idUsuario, historialInterprete, estadoPrompt), ct);
            await progreso.PasoAsync("interpretacion", "ok", "Interpretando tu mensaje");
        }
        catch (Exception ex)
        {
            await progreso.PasoAsync("interpretacion", "fail", "Interpretando tu mensaje");
            var detail = ex.Message.Contains("no disponible o salida inválida", StringComparison.OrdinalIgnoreCase)
                ? "El modelo está procesando la petición. Inténtalo de nuevo en un momento."
                : "El modelo temporalmente no respondió. Espera unos segundos e intenta de nuevo.";
            throw new ModeloNoDisponibleException(detail);
        }

        var interp = resultado.Value;

        // Reutilizar el draft ya leído en el paso 1 (evita llamada extra a BD)
        var hayDraft = draftActual.Activa;
        var hayCandidatos = interp.Candidatos.Any(c => c.NombreDetectado != "(sin identificar)");

        // Heurística: es intento de registro si menciona verbos de comer SIN ser pregunta
        // Preguntas ("?", "qué", "cómo", "cuál", "recomienda", "sugiere") NO son registro
        // Saludos y despedidas tampoco son registro
        var esPregunta = r.Texto.Contains("?")
            || r.Texto.StartsWith("qué", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("cómo", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("cuál", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("recomienda", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("sugiere", StringComparison.OrdinalIgnoreCase);

        var esSaludoDespedida = r.Texto.StartsWith("hola", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("buenos", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("buenas", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("adiós", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("adios", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("hasta", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("chao", StringComparison.OrdinalIgnoreCase)
            || r.Texto.StartsWith("bye", StringComparison.OrdinalIgnoreCase);

        var pareceRegistro = !esPregunta && !esSaludoDespedida && (
            r.Texto.Contains("comí", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("comi", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("cené", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("cene", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("desayun", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("almorz", StringComparison.OrdinalIgnoreCase)
            || r.Texto.Contains("merend", StringComparison.OrdinalIgnoreCase)
        );

        // Rutas conversacionales: balance, lista, desconocida no-registro, preguntas, saludos
        var esConversacional = (interp.Intent == AiIntent.ConsultarBalance
            || interp.Intent == AiIntent.ListarComidas
            || esPregunta
            || esSaludoDespedida
            || (interp.Intent == AiIntent.Desconocida && !hayDraft && !hayCandidatos && !pareceRegistro));

        if (esConversacional)
        {
            // Hidratar contexto SOLO en esta ruta (una sola vez)
            await progreso.PasoAsync("contexto", "start", "Cargando tus datos de hoy");
            var contexto = await HidratarContextoUsuarioAsync(idUsuario, db);
            await progreso.PasoAsync("contexto", "ok", "Cargando tus datos de hoy");

            // Historial para respondedor: 6 turnos (match frontend MAX_MENSAJES_HISTORIAL)
            var historialRespondedor = r.Historial?.TakeLast(6)
                .Select(t => new ChatTurn(t.Rol, t.Texto)).ToList();

            var respuesta = await ResponderAsync(conversacional,
                new ConversationalRequest(r.Texto, historialRespondedor, contexto), progreso, ct);

            // Guardar turno en conversación
            var conv = await turnos.ObtenerConversacionAsync(idUsuario);
            await turnos.GuardarMensajeAsync(conv, true, r.Texto, idTipo: 1);
            await turnos.GuardarMensajeAsync(conv, false, respuesta.Texto, idTipo: 1);

            return new ChatTurnResult("respuesta", Texto: respuesta.Texto, Fallback: respuesta.UsaFallback);
        }

        // ============ 3. INTENCIONES QUE EL CHAT NO PUEDE EJECUTAR ============
        // El bot conversa, analiza y redirige. Lo que escribe en la base lo
        // hace el usuario, en la vista correspondiente, apretando Guardar.

        // Mediciones y objetivo se redirigen ANTES de cualquier aclaración: la
        // política de aclaración habla de alimentos y secuestra estas intenciones
        // ("calculame la meta" terminaba pidiendo qué comiste).
        //
        // PERO solo si el mensaje no habla de comida. El modelo clasifica por
        // texto y se equivoca: "Comí pizza" llegaba clasificado como
        // crear_objetivo y el bot respondía "la meta se calcula con tus datos"
        // cuando el usuario acaba de decir que comió. Nadie pide la meta
        // calórica y una comida en el mismo mensaje.
        //
        // El guardia mira las PALABRAS DEL USUARIO (`pareceRegistro`: comí, cené,
        // desayuné, almorcé, merendé) y no lo que devolvió el modelo: cuando el
        // modelo se equivoca, también suele devolver la lista de alimentos
        // vacía, así que su salida no sirve como señal.
        if (!pareceRegistro)
        {
            if (interp.Intent == AiIntent.RegistrarMedicion)
                return await RedirigirAsync(turnos, idUsuario, r.Texto, "mediciones", "IR A MEDICIONES",
                    "No puedo guardar mediciones desde el chat. El peso y las medidas se cargan en su pantalla para que queden en tu historial.");

            if (interp.Intent == AiIntent.CrearObjetivo)
                return await RedirigirAsync(turnos, idUsuario, r.Texto, "objetivo", "CALCULAR MI OBJETIVO",
                    "La meta se calcula con tus datos y las reglas de seguridad. Te llevo a la pantalla para que la confirmes vos.");
        }

        var decision = ClarificationPolicy.Decide(interp);
        if (decision.RequiereAclaracion)
        {
            var pregunta = decision.Pregunta;
            if (r.Texto.Contains("aprox", StringComparison.OrdinalIgnoreCase) && pregunta is not null)
                pregunta += " Si prefieres, puedes cargarlo con tu mejor estimado desde Registrar.";
            return new ChatTurnResult("aclaracion", Pregunta: pregunta, Motivo: decision.Motivo, Candidatos: interp.Candidatos);
        }

        if (interp.Intent != AiIntent.RegistrarComida)
            return new ChatTurnResult("aclaracion", Pregunta: "¿Qué querés que analicemos: tu balance, tus comidas o un alimento?", Motivo: "intención no soportada aún");

        // Normalizar SIEMPRE: el LLM dice "unidad"/"pieza"/"gr" y el formulario
        // busca por código de UnidadMedida. Sin esto, "Unidad desconocida".
        var candidatos = interp.Candidatos
            .Where(c => c.NombreDetectado != "(sin identificar)")
            .Select(c => (Nombre: c.NombreDetectado, c.Cantidad,
                          Unidad: Cuantificador.NormalizarUnidad(c.Unidad)))
            .ToList();

        // El draft se abre PRIMERO: al hacerlo se completa lo que el catálogo
        // sabe (porción natural del alimento). Recién después se evalúa si
        // falta algo. Calcular "incompletos" antes devolvía
        // "slot pendiente" con un form en realidad ya completo.
        var draft = await turnos.AbrirDraftAsync(idUsuario, candidatos);

        // Sin cantidad el catálogo no puede aportar unidad por porción, pero
        // sí la unidad por defecto que usa BaseNutricionalAlimento (G).
        foreach (var it in draft.Items)
            if (it.Cantidad is null) it.Unidad = "G";

        if (draft.Estado != EstadoSlot.Completo)
        {
            var conversacion = await turnos.ObtenerConversacionAsync(idUsuario);
            await turnos.GuardarMensajeAsync(conversacion, true, r.Texto, idTipo: 1);
            var pregunta = MealSlotTracker.PreguntaPara(draft)
                ?? "¿Qué comiste? Indicá alimento y cantidad.";
            await turnos.GuardarMensajeAsync(conversacion, false, pregunta, idTipo: 2);
            return new ChatTurnResult("aclaracion", Pregunta: pregunta, Motivo: "slot pendiente");
        }

        // Está completo: el chat solo arma la redirección. Nada se escribe acá.
        var listos = MealSlotTracker.Listos(draft)
            .Select(i => new ItemRedirigido(i.Nombre, i.Cantidad, i.Unidad))
            .ToList();

        if (listos.Count == 0)
            return new ChatTurnResult("aclaracion", Pregunta: "¿Qué comiste? Indicá alimento y cantidad.", Motivo: "sin cantidades estructuradas");

        await turnos.GuardarMensajeAsync(
            await turnos.ObtenerConversacionAsync(idUsuario), true, r.Texto, idTipo: 1);
        return await CerrarConRedireccionAsync(turnos, idUsuario, listos,
            $"Listo: {listos.Count} {(listos.Count == 1 ? "alimento" : "alimentos")}. Revisá y guardá vos en el formulario.");
    }

    /// <summary>
    /// Redirección con datos precargados. El chat arma el borrador, lo entrega y
    /// se desvincula: el guardado lo hace el usuario en la vista destino.
    /// </summary>
    private static async Task<ChatTurnResult> CerrarConRedireccionAsync(
        ChatTurnService turnos, int idUsuario, IReadOnlyList<ItemRedirigido> items, string texto)
    {
        var conv = await turnos.ObtenerConversacionAsync(idUsuario);
        await turnos.GuardarMensajeAsync(conv, false, texto, idTipo: 1);
        return new ChatTurnResult("redirigir",
            Texto: texto,
            Redireccion: new RedireccionDestino("registrar", "IR A REGISTRAR", items));
    }

    /// <summary>Redirección simple (sin datos que llevar).</summary>
    private static async Task<ChatTurnResult> RedirigirAsync(
        ChatTurnService turnos, int idUsuario, string textoUsuario, string vista, string etiqueta, string texto)
    {
        var conv = await turnos.ObtenerConversacionAsync(idUsuario);
        await turnos.GuardarMensajeAsync(conv, true, textoUsuario, idTipo: 1);
        await turnos.GuardarMensajeAsync(conv, false, texto, idTipo: 1);
        return new ChatTurnResult("redirigir",
            Texto: texto,
            Redireccion: new RedireccionDestino(vista, etiqueta, Array.Empty<ItemRedirigido>()));
    }

    /// <summary>
    /// Con reporte de progreso: transmite los tokens que el modelo emite de verdad
    /// y devuelve el texto final validado. Sin reporte: la llamada normal.
    /// </summary>
    private static async Task<ConversationalResponse> ResponderAsync(
        IConversationalResponder conversacional,
        ConversationalRequest req,
        IChatProgress progreso,
        CancellationToken ct)
    {
        if (progreso is SinProgreso)
            return await conversacional.ResponderAsync(req, ct);

        await progreso.PasoAsync("respuesta", "start", "Redactando tu respuesta");

        var acumulado = new System.Text.StringBuilder();
        ChatStreamEvent? fin = null;
        var fallo = (string?)null;

        try
        {
            await foreach (var ev in conversacional.ResponderStreamAsync(req, ct))
            {
                if (ev.Type == "token")
                {
                    if (!string.IsNullOrEmpty(ev.Text))
                    {
                        acumulado.Append(ev.Text);
                        await progreso.TokenAsync(ev.Text!);
                    }
                }
                else if (ev.Type == "done")
                {
                    fin = ev;
                }
                else if (ev.Type == "error")
                {
                    fallo = ev.Message ?? "El modelo no devolvió una respuesta válida.";
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // El stream se cortó a mitad: se conserva lo escrito y se devuelve
            // el fallback, igual que la ruta sin streaming.
            fallo = ex.Message;
        }

        await progreso.PasoAsync("respuesta", fallo is null ? "ok" : "fail", "Redactando tu respuesta");

        // Sin evento `done` (p.ej. el modelo cerró el stream sin completar):
        // se cae a la ruta sin streaming para no devolver texto a medias.
        if (fin is null && fallo is null)
            return await conversacional.ResponderAsync(req, ct);

        var textoFinal = fin?.FinalText;
        if (fallo is not null || string.IsNullOrWhiteSpace(textoFinal))
        {
            var seguro = await conversacional.ResponderAsync(req, ct);
            return seguro.UsaFallback
                ? seguro
                : new ConversationalResponse(acumulado.ToString().Trim() is { Length: > 0 } parcial
                    ? parcial
                    : seguro.Texto, UsaFallback: true);
        }

        return new ConversationalResponse(textoFinal, fin!.Fallback);
    }

    /// <summary>Escribe eventos SSE reales: <c>event: paso|token|result|error|done</c>.</summary>
    private sealed class SseProgress(HttpResponse response, CancellationToken ct, JsonSerializerOptions opciones) : IChatProgress
    {
        public Task PasoAsync(string nombre, string estado, string? detalle = null) =>
            EnviarAsync("paso", new { nombre, estado, detalle });

        public Task TokenAsync(string texto) =>
            EnviarAsync("token", new { texto });

        public async Task EnviarAsync(string evento, object payload)
        {
            if (ct.IsCancellationRequested) return;
            var json = JsonSerializer.Serialize(payload, opciones);
            await response.WriteAsync($"event: {evento}\ndata: {json}\n\n", ct);
            await response.Body.FlushAsync(ct);
        }
    }

    internal static async Task<ContextoUsuario> HidratarContextoUsuarioAsync(int idUsuario, AppDbContext db)
    {
        // SECUENCIAL: EF Core no permite operaciones concurrentes en el mismo DbContext.
        // Las consultas son rápidas (PK/FK, índices); la latencia añadida es mínima.

        // 1. Perfil
        var perfil = await db.PerfilesUsuario.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario && p.Estatus == 1)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.IdSexoBiologico,
                p.IdNivelActividadFisica,
                p.FechaNacimiento,
                p.Altura
            })
            .FirstOrDefaultAsync();

        // 2. Objetivo vigente: solo MetaCalorica. No existe ritmo almacenado en
        // ningún lado (ObjetivoNutricional está vacío y nada lo escribe), así
        // que no se inventa: RitmoMin/Max van null y el riesgo queda
        // "Desconocido" (NivelRiesgo 0) en vez de afirmar "Bajo" sin evaluar.
        var objetivo = await db.Database.SqlQueryRaw<ObjetivoVigenteDto>(
            """
            SELECT m."CaloriasObjetivo" AS "MetaKcal", m."FechaInicio" AS "FechaInicio",
                   NULL AS "RitmoMin", NULL AS "RitmoMax",
                   0 AS "NivelRiesgo", 'Sin evaluar' AS "DescRiesgo"
            FROM "MetaCalorica" m
            WHERE m."IdUsuario" = {0} AND m."Estatus" = 1
              AND (m."FechaFin" IS NULL OR m."FechaFin" >= CURRENT_DATE)
            ORDER BY m."FechaInicio" DESC
            LIMIT 1
            """, idUsuario).FirstOrDefaultAsync();

        // 3. Balance hoy (comidas + macros en una query cada una)
        var hoy = DateTime.UtcNow.Date;

        var comidasHoy = await db.RegistrosComida.AsNoTracking()
            .Where(x => x.IdUsuario == idUsuario && x.Estatus == 1 && x.FechaConsumo.Date == hoy)
            .OrderBy(x => x.FechaConsumo)
            .Select(x => new { x.Id, x.Nombre, x.CaloriasEstimadas, x.FechaConsumo, x.IdTipoComida })
            .ToListAsync();

        var filasMacros = await db.Database.SqlQueryRaw<MacroFilaDto>(
            """
            SELECT n."Codigo" AS "Codigo", an."Cantidad" AS "PorBase",
                   COALESCE(arc."CantidadGramos", arc."Cantidad") AS "Cantidad",
                   bn."CantidadBase" AS "BaseGramos"
            FROM "AlimentoRegistroComida" arc
            JOIN "RegistroComida" rc ON rc."Id" = arc."IdRegistroComida"
            JOIN "AlimentoFuente" af ON af."IdAlimento" = arc."IdAlimento"
            JOIN "BaseNutricionalAlimento" bn ON bn."IdAlimentoFuente" = af."Id"
            JOIN "AlimentoNutriente" an ON an."IdAlimentoFuente" = af."Id"
            JOIN "Nutriente" n ON n."Id" = an."IdNutriente"
            WHERE rc."IdUsuario" = {0} AND rc."FechaConsumo"::date = {1}::date
              AND n."Codigo" IN ('PROTEINA','GRASA','CARBOHIDRATO') AND bn."CantidadBase" > 0
            """, idUsuario, hoy).ToListAsync();

        var pesos = await db.Database.SqlQueryRaw<PesoDto>(
            """
            SELECT m."Valor", m."FechaMedicion"
            FROM "MedicionCorporal" m
            JOIN "TipoMedicionCorporal" t ON t."Id" = m."IdTipoMedicionCorporal"
            WHERE m."IdUsuario" = {0} AND (t."Codigo" = 'PESO' OR t."Codigo" = 'PESO_KG')
            ORDER BY m."FechaMedicion" DESC
            LIMIT 2
            """, idUsuario).ToListAsync();

        var kcalHoy = comidasHoy.Sum(c => c.CaloriasEstimadas ?? 0m);
        int? metaKcal = objetivo == null ? null : (int)Math.Round(objetivo.MetaKcal);
        var macros = filasMacros
            .GroupBy(f => f.Codigo)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(f => f.PorBase * f.Cantidad / f.BaseGramos), 1));

        var macroResumen = new MacroResumen(
            ProteinaG: (int)Math.Round(macros.GetValueOrDefault("PROTEINA", 0)),
            GrasaG: (int)Math.Round(macros.GetValueOrDefault("GRASA", 0)),
            CarbohidratoG: (int)Math.Round(macros.GetValueOrDefault("CARBOHIDRATO", 0))
        );

        var tiposComida = await db.Database.SqlQueryRaw<IdNombreDto>(
            """SELECT "Id", "Codigo" AS "Nombre" FROM "TipoComida" """).ToListAsync();
        var tipoPorId = tiposComida.ToDictionary(t => t.Id, t => t.Nombre);

        // Mapeo a DTOs
        PerfilResumen? perfilResumen = null;
        if (perfil != null)
        {
            var edad = DateTime.UtcNow.Year - perfil.FechaNacimiento.Year;
            if (DateTime.UtcNow.Month < perfil.FechaNacimiento.Month ||
                (DateTime.UtcNow.Month == perfil.FechaNacimiento.Month && DateTime.UtcNow.Day < perfil.FechaNacimiento.Day))
                edad--;

            var sexo = perfil.IdSexoBiologico == 2 ? "F" : "M";

            var actCodigo = perfil.IdNivelActividadFisica switch
            {
                1 => "SEDENTARIO",
                2 => "LIGERO",
                3 => "MODERADO",
                4 => "ACTIVO",
                5 => "MUY_ACTIVO",
                _ => "MODERADO"
            };

            perfilResumen = new PerfilResumen(edad, sexo, (int)Math.Round(perfil.Altura), actCodigo);
        }

        ObjetivoResumen? objetivoResumen = null;
        if (objetivo != null)
        {
            var (tipo, ritmo) = objetivo.RitmoMin.HasValue && objetivo.RitmoMax.HasValue
                ? (objetivo.RitmoMin < 0 ? ("Perder", Math.Round(objetivo.RitmoMin.Value, 2)) : ("Ganar", Math.Round(objetivo.RitmoMax.Value, 2)))
                : ("Mantener", 0m);

            var riesgoEtiqueta = objetivo.NivelRiesgo switch
            {
                1 => "Moderado",
                2 => "Alto",
                3 => "Bajo",
                _ => "Desconocido"
            };

            objetivoResumen = new ObjetivoResumen(
                Tipo: tipo,
                MetaKcal: (int)Math.Round(objetivo.MetaKcal),
                RitmoKgSem: ritmo,
                Riesgo: riesgoEtiqueta
            );
        }

        var comidasResumen = comidasHoy.Select(c => new ComidaResumen(
            Nombre: c.IdTipoComida != null && tipoPorId.TryGetValue(c.IdTipoComida.Value, out var tc) ? tc : c.Nombre ?? "Comida",
            Kcal: (int)Math.Round(c.CaloriasEstimadas ?? 0m),
            Hora: c.FechaConsumo.ToLocalTime().ToString("HH:mm")
        )).ToArray();

        var balanceHoy = new BalanceHoy(
            KcalConsumidas: (int)Math.Round(kcalHoy),
            MetaKcal: metaKcal,
            Macros: macroResumen,
            Comidas: comidasResumen
        );

        PesoResumen? pesoResumen = null;
        if (pesos.Count > 0)
        {
            var ultimo = pesos[0];
            var delta = pesos.Count > 1 ? Math.Round((ultimo.Valor - pesos[1].Valor) * 10m) / 10m : (decimal?)null;
            pesoResumen = new PesoResumen(
                UltimoKg: Math.Round(ultimo.Valor, 1),
                Fecha: ultimo.FechaMedicion.ToString("yyyy-MM-dd"),
                DeltaKg: delta
            );
        }

        // 5. Planes alimentarios del usuario: SOLO LECTURA. Es lo que el bot
        // analiza ("estudiar la información del usuario o de planes
        // alimenticos"). Filtra SIEMPRE por IdUsuario: nada de otro usuario.
        var planesResumen = await LeerPlanesResumenAsync(idUsuario, db);

        return new ContextoUsuario(perfilResumen, objetivoResumen, balanceHoy, pesoResumen,
            Planes: planesResumen);
    }

    /// <summary>
    /// Lee los planes del usuario en forma plana para que el modelo los pueda
    /// analizar. Es una lectura: no crea, no modifica y no toca nada.
    /// Se limita a 3 planes y 12 comidas por plan porque el contexto entra en el
    /// prompt (num_ctx 4096): volcar el plan entero lo desborda y lo inútil.
    /// </summary>
    private static async Task<PlanResumen[]> LeerPlanesResumenAsync(int idUsuario, AppDbContext db)
    {
        // El Id va en el SELECT: sin él no se puede agrupar cada comida con su
        // plan, y agrupar por nombre sería frágil (dos planes pueden llamarse
        // igual). Los activos primero: son los que el usuario está siguiendo.
        var planes = await db.Database.SqlQueryRaw<PlanResumenDto>(
            """
            SELECT p."Id" AS "IdPlan", p."Nombre" AS "Nombre",
                   (p."FechaFin" IS NULL) AS "Activo",
                   COALESCE(d."Dias", 0) AS "Dias"
            FROM "PlanAlimenticio" p
            LEFT JOIN LATERAL (
                SELECT COUNT(*)::int AS "Dias"
                FROM "DiaPlanAlimenticio" x
                WHERE x."IdPlanAlimenticio" = p."Id" AND x."Estatus" = 1
            ) d ON TRUE
            WHERE p."IdUsuario" = {0} AND p."Estatus" = 1
            ORDER BY (p."FechaFin" IS NULL) DESC, COALESCE(p."FechaFin", p."FechaInicio") DESC
            LIMIT 3
            """, idUsuario).ToListAsync();

        if (planes.Count == 0) return [];

        var ids = planes.Select(p => p.IdPlan).ToArray();

        var comidas = await db.Database.SqlQueryRaw<ComidaPlanResumenDto>(
            """
            SELECT d."IdPlanAlimenticio" AS "IdPlan", COALESCE(tc."Codigo", 'Comida') AS "Momento",
                   COALESCE(a."Nombre", '(alimento del catálogo)') AS "Alimento",
                   acp."Cantidad" AS "Cantidad", COpU."Codigo" AS "Unidad"
            FROM "AlimentoComidaPlan" acp
            JOIN "ComidaPlan" cp ON cp."Id" = acp."IdComidaPlan" AND cp."Estatus" = 1
            JOIN "DiaPlanAlimenticio" d ON d."Id" = cp."IdDiaPlanAlimenticio" AND d."Estatus" = 1
            JOIN "TipoComida" tc ON tc."Id" = cp."IdTipoComida"
            LEFT JOIN "Alimento" a ON a."Id" = acp."IdAlimento"
            LEFT JOIN "UnidadMedida" COpU ON COpU."Id" = acp."IdUnidadMedida"
            WHERE d."IdPlanAlimenticio" = ANY({0}) AND acp."Estatus" = 1
            ORDER BY d."IdPlanAlimenticio", cp."Id", acp."Orden"
            """, ids).ToListAsync();

        return planes
            .Select(p => new PlanResumen(
                Nombre: p.Nombre,
                Estado: p.Activo ? "Activo" : "Archivado",
                Dias: p.Dias,
                Comidas: comidas
                    .Where(c => c.IdPlan == p.IdPlan)
                    // El prompt no da lugar a un volcado: 12 por plan y se
                    // declara el total, así el modelo sabe que hay más.
                    .Take(12)
                    .Select(c => new ComidaPlanResumen(
                        Momento: c.Momento,
                        Alimento: c.Alimento,
                        Cantidad: c.Cantidad,
                        Unidad: string.IsNullOrEmpty(c.Unidad) ? "U" : c.Unidad))
                    .ToArray()))
            .ToArray();
    }

    // DTOs internos para SQL raw
    private record ObjetivoVigenteDto(decimal MetaKcal, DateTime FechaInicio, decimal? RitmoMin, decimal? RitmoMax, int NivelRiesgo, string DescRiesgo);
    private record MacroFilaDto(string Codigo, decimal PorBase, decimal Cantidad, decimal BaseGramos);
    private record PesoDto(decimal Valor, DateTime FechaMedicion);
    private record IdNombreDto(int Id, string Nombre);
    private record PlanResumenDto(int IdPlan, string Nombre, bool Activo, int Dias);
    private record ComidaPlanResumenDto(int IdPlan, string Momento, string Alimento, decimal Cantidad, string Unidad);
}