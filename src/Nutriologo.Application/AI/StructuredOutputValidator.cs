namespace Nutriologo.Application.AI;

using System.Text.Json;
using Nutriologo.Application.Abstractions.AI;

/// <summary>Schema canónico JSON para la salida del LLM de chat.</summary>
public static class StructuredOutputValidator
{
    public static bool TryParse(string raw, out ChatInterpretation? result, out string error)
    {
        result = null;
        error = "";
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("intencion", out var intencionEl) || intencionEl.ValueKind != JsonValueKind.String)
            { error = "intencion ausente o no string"; return false; }
            if (!root.TryGetProperty("requiere_aclaracion", out var aclarEl) ||
                (aclarEl.ValueKind != JsonValueKind.True && aclarEl.ValueKind != JsonValueKind.False))
            { error = "requiere_aclaracion ausente o no booleano"; return false; }
            if (!root.TryGetProperty("alimentos", out var alimEl) ||
                (alimEl.ValueKind != JsonValueKind.Array && alimEl.ValueKind != JsonValueKind.Null))
            { error = "alimentos ausente o no array"; return false; }

            var intent = intencionEl.GetString() switch
            {
                "registrar_comida" or "RegistrarComida" => AiIntent.RegistrarComida,
                "registrar_medicion" or "RegistrarMedicion" => AiIntent.RegistrarMedicion,
                "crear_objetivo" or "CrearObjetivo" => AiIntent.CrearObjetivo,
                "consultar_balance" or "ConsultarBalance" => AiIntent.ConsultarBalance,
                _ => AiIntent.Desconocida,
            };

            var candidatos = new List<FoodCandidate>();
            if (alimEl.ValueKind == JsonValueKind.Array)
            foreach (var a in alimEl.EnumerateArray())
            {
                if (!a.TryGetProperty("nombre", out var nEl) || nEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(nEl.GetString()))
                { error = "alimento sin nombre válido"; return false; }

                decimal? cantidad = a.TryGetProperty("cantidad", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                    ? cEl.GetDecimal() : null;
                if (cantidad is not null && cantidad <= 0) { error = "cantidad debe ser positiva"; return false; }

                var unidad = a.TryGetProperty("unidad", out var uEl) && uEl.ValueKind == JsonValueKind.String ? uEl.GetString() : null;
                var preparacion = a.TryGetProperty("preparacion", out var pEl) && pEl.ValueKind == JsonValueKind.String ? pEl.GetString() : null;

                var incertidumbres = new List<string>();
                if (a.TryGetProperty("incertidumbres", out var iEl) && iEl.ValueKind == JsonValueKind.Array)
                    foreach (var i in iEl.EnumerateArray())
                        if (i.ValueKind == JsonValueKind.String) incertidumbres.Add(i.GetString()!);

                decimal confianza = 0.5m;
                if (a.TryGetProperty("confianza", out var cfEl) && cfEl.ValueKind == JsonValueKind.Number)
                    confianza = cfEl.GetDecimal();
                if (confianza < 0 || confianza > 1) { error = "confianza fuera de [0,1]"; return false; }

                candidatos.Add(new FoodCandidate(nEl.GetString()!, cantidad, unidad, preparacion, confianza, incertidumbres));
            }

            bool requiere = aclarEl.GetBoolean();
            // Guard: intención de registrar comida sin candidatos utilizables ⇒ forzar aclaración.
            if (intent == AiIntent.RegistrarComida && candidatos.Count == 0 && !requiere)
            {
                requiere = true;
                candidatos.Add(new FoodCandidate("(sin identificar)", null, null, null, 0m,
                    new[] { "información insuficiente para registrar alimentos" }));
            }

            result = new ChatInterpretation(intent, candidatos, candidatos.SelectMany(c => c.Incertidumbres).ToList());
            return true;
        }
        catch (JsonException ex)
        {
            error = "JSON malformado: " + ex.Message;
            return false;
        }
    }
}
