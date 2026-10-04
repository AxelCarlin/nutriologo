using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class ChatEndpoints
{
    public record MensajeRequest(int IdUsuario, string Texto);

    public static void MapChat(this WebApplication app)
    {
        app.MapPost("/api/conversacion/mensaje", async (MensajeRequest r, IChatInterpreter interpreter,
            AppDbContext db, IFoodCatalogSource catalog) =>
        {
            AiResult<ChatInterpretation> resultado;
            try
            {
                resultado = await interpreter.InterpretAsync(new ChatRequest(r.Texto, r.IdUsuario));
            }
            catch (Exception ex)
            {
                return Results.Problem($"IA no disponible o salida inválida: {ex.Message}", statusCode: 502);
            }

            var interp = resultado.Value;
            var decision = ClarificationPolicy.Decide(interp);
            if (decision.RequiereAclaracion)
                return Results.Ok(new { tipo = "aclaracion", pregunta = decision.Pregunta, motivo = decision.Motivo, interp.Candidatos });

            if (interp.Intent != AiIntent.RegistrarComida)
                return Results.Ok(new { tipo = "aclaracion", pregunta = "¿Qué te gustaría registrar o consultar?", motivo = "intención no soportada aún" });

            var items = interp.Candidatos
                .Where(c => c.Cantidad is not null && c.Unidad is not null)
                .Select(c => new MealItemInput(c.NombreDetectado, c.Cantidad!.Value, c.Unidad!, c.Preparacion))
                .ToList();

            if (items.Count == 0)
                return Results.Ok(new { tipo = "aclaracion", pregunta = "¿Qué comiste? Indicá alimento y cantidad.", motivo = "sin cantidades estructuradas" });

            var registrado = await MealRegistrar.RegisterAsync(db, catalog, r.IdUsuario, DateTime.UtcNow, null, items);
            if (!registrado.Ok)
                return Results.Ok(new { tipo = "aclaracion", registrado.RequiereAclaracion, registrado.Mensaje });

            return Results.Ok(new { tipo = "registrado", id = registrado.Id, mensaje = registrado.Mensaje });
        });
    }
}
