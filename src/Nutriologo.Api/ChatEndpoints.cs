using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nutriologo.Api.Auth;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Infrastructure.Conversations;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class ChatEndpoints
{
    public record TurnoRequest(string Rol, string Texto);
    public record MensajeRequest(string Texto, IReadOnlyList<TurnoRequest>? Historial = null);

    public static void MapChat(this WebApplication app)
    {
        app.MapPost("/api/conversacion/mensaje", async (MensajeRequest r, ClaimsPrincipal user, IChatInterpreter interpreter,
            IConversationalResponder conversacional, AppDbContext db, IFoodCatalogSource catalog, ChatTurnService turnos) =>
        {
            var idUsuario = JwtIssuer.UserId(user);

            // ============ 0. SLOT FILLING DETERMINISTA ============
            // Se intenta ANTES de llamar al LLM. Un mensaje corto ("sí, la
            // grande", "50ml") lo resuelve la tabla de patches en C#: un 7B en
            // CPU no resuelve esa coreferencia de forma fiable, y wasted una
            // inferencia de ~14 s en intentarlo.
            var avance = await turnos.IntentarAvanzarAsync(idUsuario, r.Texto);

            // Cancelación: no es una aclaración ni un error, es un cierre explícito.
            if (avance is { Completo: false } && avance.Estado.Estado == EstadoSlot.Cancelado)
            {
                var conv = await turnos.ObtenerConversacionAsync(idUsuario);
                await turnos.GuardarMensajeAsync(conv, true, r.Texto, idTipo: 1);
                await turnos.GuardarMensajeAsync(conv, false, avance.Pregunta ?? "Ok, lo dejamos así.", idTipo: 1);
                return Results.Ok(new
                {
                    tipo = "respuesta",
                    texto = avance.Pregunta ?? "Ok, lo dejamos así.",
                });
            }

            if (avance is not null && !avance.Completo)
            {
                await turnos.GuardarMensajeAsync(
                    await turnos.ObtenerConversacionAsync(idUsuario), true, r.Texto, idTipo: 1);
                await turnos.GuardarMensajeAsync(
                    await turnos.ObtenerConversacionAsync(idUsuario), false,
                    avance.Pregunta ?? "¿Puedes precisar?", idTipo: 2);
                return Results.Ok(new { tipo = "aclaracion", pregunta = avance.Pregunta, motivo = "slot pendiente" });
            }

            if (avance is { Completo: true })
            {
                var conversacion = await turnos.ObtenerConversacionAsync(idUsuario);
                await turnos.GuardarMensajeAsync(conversacion, true, r.Texto, idTipo: 1);

                // Tras resolver el patch la cantidad existe pero la unidad puede
                // seguir abierta: se completa con la porción del catálogo antes de
                // registrar, para no exigir al usuario una unidad que ya se sabe.
                await turnos.CompletarUnidadesDesdeCatalogoAsync(avance.Estado);

                var itemsListos = MealSlotTracker.Listos(avance.Estado)
                    .Select(i => new MealItemInput(i.Nombre, i.Cantidad, i.Unidad))
                    .ToList();

                var registro = await MealRegistrar.RegisterAsync(db, catalog, idUsuario, DateTime.UtcNow, null, itemsListos);
                await turnos.LimpiarDraftAsync(conversacion);

                if (!registro.Ok)
                    return Results.Ok(new { tipo = "aclaracion", pregunta = registro.Mensaje, motivo = "registro rechazado" });

                await turnos.GuardarMensajeAsync(conversacion, false,
                    "Comida registrada correctamente.", idTipo: 3);
                return Results.Ok(new { tipo = "registrado", id = registro.Id, mensaje = registro.Mensaje });
            }

            // ============ 1. INTÉRPRETE LLM: clasifica intención + extrae candidatos ============
            // Historial: 2 turnos (match intérprete; el estado estructurado da la memoria).
            // El draft se lee UNA vez acá y se reutiliza abajo (sin segunda llamada a BD).
            var draftActual = await turnos.LeerDraftAsync(await turnos.ObtenerConversacionAsync(idUsuario));
            var estadoPrompt = draftActual.Items
                .Select(i => (i.Nombre, i.Cantidad, i.Unidad)).ToList();

            AiResult<ChatInterpretation> resultado;
            try
            {
                var historialInterprete = r.Historial?.TakeLast(2)
                    .Select(t => new ChatTurn(t.Rol, t.Texto)).ToList();

                resultado = await interpreter.InterpretAsync(
                    new ChatRequest(r.Texto, idUsuario, historialInterprete, estadoPrompt));
            }
            catch (Exception ex)
            {
                var detail = ex.Message.Contains("no disponible o salida inválida", StringComparison.OrdinalIgnoreCase)
                    ? "El modelo está procesando la petición. Inténtalo de nuevo en un momento."
                    : "El modelo temporalmente no respond. Por favor, espera unos segundos e intenta de nuevo.";
                return Results.Problem(detail, statusCode: 503);
            }

            var interp = resultado.Value;

            // Reutilizar el draft ya leído en el paso 1 (evita llamada extra a BD)
            var hayDraft = draftActual.Activa;
            var hayCandidatos = interp.Candidatos.Any(c => c.NombreDetectado != "(sin identificar)");

            // Heurística: es intento de registro si menciona verbos de comer SIN ser pregunta
            // Preguntas ("?", "qué", "cómo", "cuál", "recomienda") NO son registro
            var esPregunta = r.Texto.Contains("?")
                || r.Texto.StartsWith("qué", StringComparison.OrdinalIgnoreCase)
                || r.Texto.StartsWith("cómo", StringComparison.OrdinalIgnoreCase)
                || r.Texto.StartsWith("cuál", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("recomienda", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("sugiere", StringComparison.OrdinalIgnoreCase);

            var pareceRegistro = !esPregunta && (
                r.Texto.Contains("comí", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("comi", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("cené", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("cene", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("desayun", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("almorz", StringComparison.OrdinalIgnoreCase)
                || r.Texto.Contains("merend", StringComparison.OrdinalIgnoreCase)
            );

            var esConversacional = (interp.Intent == AiIntent.ConsultarBalance
                || interp.Intent == AiIntent.ListarComidas
                || (interp.Intent == AiIntent.Desconocida && !hayDraft && !hayCandidatos && !pareceRegistro));

            if (esConversacional)
            {
                // Hidratar contexto SOLO en esta ruta (paralelo, una sola vez)
                var contexto = await HidratarContextoUsuarioAsync(idUsuario, db);

                // Historial para respondedor: 6 turnos (match frontend MAX_MENSAJES_HISTORIAL)
                var historialRespondedor = r.Historial?.TakeLast(6)
                    .Select(t => new ChatTurn(t.Rol, t.Texto)).ToList();

                var respuesta = await conversacional.ResponderAsync(
                    new ConversationalRequest(r.Texto, historialRespondedor, contexto));

                // Guardar turno en conversación
                var conv = await turnos.ObtenerConversacionAsync(idUsuario);
                await turnos.GuardarMensajeAsync(conv, true, r.Texto, idTipo: 1);
                await turnos.GuardarMensajeAsync(conv, false, respuesta.Texto, idTipo: 1);

                return Results.Ok(new
                {
                    tipo = "respuesta",
                    texto = respuesta.Texto,
                    fallback = respuesta.UsaFallback
                });
            }

            // ============ 3. INTENCIONES DE REGISTRO/ACCIÓN: flujo actual ============
            var decision = ClarificationPolicy.Decide(interp);
            if (decision.RequiereAclaracion)
            {
                var pregunta = decision.Pregunta;
                if (r.Texto.Contains("aprox", StringComparison.OrdinalIgnoreCase) && pregunta is not null)
                    pregunta += " Si prefieres, puedes cargarlo con tu mejor estimado desde Registrar.";
                return Results.Ok(new { tipo = "aclaracion", pregunta, motivo = decision.Motivo, interp.Candidatos });
            }

            if (interp.Intent != AiIntent.RegistrarComida)
                return Results.Ok(new { tipo = "aclaracion", pregunta = "¿Qué te gustaría registrar o consultar?", motivo = "intención no soportada aún" });

            // Normalizar SIEMPRE: el LLM dice "unidad"/"pieza"/"gr" y MealRegistrar
            // busca por código de UnidadMedida. Sin esto, "Unidad desconocida".
            var candidatos = interp.Candidatos
                .Where(c => c.NombreDetectado != "(sin identificar)")
                .Select(c => (Nombre: c.NombreDetectado, c.Cantidad,
                              Unidad: Cuantificador.NormalizarUnidad(c.Unidad)))
                .ToList();

            // El draft se abre PRIMERO: al hacerlo se completa lo que el catálogo
            // sabe (porción natural del alimento). Recién después se evalúa si
            // falta algo. Calcular "incompletos" antes devolvía
            // "slot pendiente" con un form que en realidad ya estaba completo.
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
                return Results.Ok(new { tipo = "aclaracion", pregunta, motivo = "slot pendiente" });
            }

            // El draft quedó completo con datos del catálogo: se registra ya.
            var listosDelDraft = MealSlotTracker.Listos(draft);
            if (listosDelDraft.Count > 0)
            {
                var conv = await turnos.ObtenerConversacionAsync(idUsuario);
                await turnos.GuardarMensajeAsync(conv, true, r.Texto, idTipo: 1);
                var reg = await MealRegistrar.RegisterAsync(db, catalog, idUsuario, DateTime.UtcNow, null,
                    listosDelDraft.Select(i => new MealItemInput(i.Nombre, i.Cantidad, i.Unidad)).ToList());
                await turnos.LimpiarDraftAsync(conv);
                if (!reg.Ok)
                    return Results.Ok(new { tipo = "aclaracion", pregunta = reg.Mensaje, motivo = "registro rechazado" });
                await turnos.GuardarMensajeAsync(conv, false, "Comida registrada correctamente.", idTipo: 3);
                return Results.Ok(new { tipo = "registrado", id = reg.Id, mensaje = reg.Mensaje });
            }

            var items = candidatos
                .Where(c => c.Cantidad is not null && c.Unidad is not null)
                .Select(c => new MealItemInput(c.Nombre, c.Cantidad!.Value, c.Unidad!, null))
                .ToList();

            if (items.Count == 0)
                return Results.Ok(new { tipo = "aclaracion", pregunta = "¿Qué comiste? Indicá alimento y cantidad.", motivo = "sin cantidades estructuradas" });

            var registrado = await MealRegistrar.RegisterAsync(db, catalog, idUsuario, DateTime.UtcNow, null, items);
            if (!registrado.Ok)
                return Results.Ok(new { tipo = "aclaracion", pregunta = registrado.Mensaje, motivo = "registro rechazado" });

            return Results.Ok(new { tipo = "registrado", id = registrado.Id, mensaje = registrado.Mensaje });
        }).RequireRateLimiting("business").RequireAuthorization();
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

        // 4. Peso (últimos 2)
        var pesos = await db.Database.SqlQueryRaw<PesoDto>(
            """
            SELECT m."Valor", m."FechaMedicion"
            FROM "MedicionCorporal" m
            JOIN "TipoMedicionCorporal" t ON t."Id" = m."IdTipoMedicionCorporal"
            WHERE m."IdUsuario" = {0} AND (t."Codigo" = 'PESO' OR t."Codigo" = 'PESO_KG')
            ORDER BY m."FechaMedicion" DESC
            LIMIT 2
            """, idUsuario).ToListAsync();

        // Mapeo a DTOs (igual que antes)
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

        return new ContextoUsuario(perfilResumen, objetivoResumen, balanceHoy, pesoResumen);
    }

    // DTOs internos para SQL raw
    private record ObjetivoVigenteDto(decimal MetaKcal, DateTime FechaInicio, decimal? RitmoMin, decimal? RitmoMax, int NivelRiesgo, string DescRiesgo);
    private record MacroFilaDto(string Codigo, decimal PorBase, decimal Cantidad, decimal BaseGramos);
    private record PesoDto(decimal Valor, DateTime FechaMedicion);
    private record IdNombreDto(int Id, string Nombre);
}