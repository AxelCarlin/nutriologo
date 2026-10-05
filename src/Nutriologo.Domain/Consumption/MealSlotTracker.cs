namespace Nutriologo.Domain.Consumption;

using System.Globalization;
using Nutriologo.Domain.Catalog;
using System.Text.RegularExpressions;

/// <summary>
/// Relleno determinista de slots ("comida en curso").
///
/// Existe porque un LLM de 7B en CPU no resuelve correferencia de forma fiable:
/// "sí, la grande" no comparte tokens con "tortilla", y el modelo responde
/// "sin candidatos". Acá la referencia se resuelve en C#, con tabla propia.
///
/// Principio: el LLM extrae ALIMENTOS cuando el mensaje es una frase nueva.
/// Toda respuesta corta posterior ("sí, la grande", "50ml", "dos más") la
/// resuelve esta clase, sin llamar al modelo.
/// </summary>
public enum SlotAlimento { Ninguno, Nombre, Cantidad, Unidad }

public enum EstadoSlot { Inactivo, EsperandoAlimento, EsperandoCantidad, EsperandoUnidad, Completo, Cancelado }

/// <summary>Un alimento acumulándose dentro de una comida en curso.</summary>
public sealed class ItemEnCurso
{
    public string Nombre { get; init; } = "";
    public decimal? Cantidad { get; set; }
    public string? Unidad { get; set; }
    /// <summary>Origen del dato: el modelo propone, el tracker confirma.</summary>
    public bool ConfirmadoPorTracker { get; set; }

    public SlotAlimento SlotPendiente => Cantidad is null ? SlotAlimento.Cantidad
        : Unidad is null ? SlotAlimento.Unidad
        : SlotAlimento.Ninguno;
}

/// <summary>Estado completo de la comida que se está armando.</summary>
public sealed class ComidaEnCurso
{
    public List<ItemEnCurso> Items { get; } = new();
    public EstadoSlot Estado { get; set; } = EstadoSlot.Inactivo;

    public bool Activa => Estado is EstadoSlot.EsperandoAlimento or EstadoSlot.EsperandoCantidad
        or EstadoSlot.EsperandoUnidad or EstadoSlot.Completo;

    public ItemEnCurso? Pendiente => Items.LastOrDefault(i => i.SlotPendiente != SlotAlimento.Ninguno);
}

/// <summary>
/// Resultado de intentar avanzar el form con un mensaje del usuario.
/// </summary>
public sealed record ResultadoSlot
{
    public required bool Reconocido { get; init; }
    public required bool Completo { get; init; }
    public string? Pregunta { get; init; }
    public ComidaEnCurso Estado { get; init; } = new();
    public IReadOnlyList<(string Nombre, decimal Cantidad, string Unidad)> Items { get; init; } = Array.Empty<(string, decimal, string)>();

    /// <summary>Frase corta que no se pudo interpretar y no abre form nuevo.</summary>
    public static ResultadoSlot NoReconocido(ComidaEnCurso estado, string? pregunta = null) =>
        new() { Reconocido = false, Completo = false, Estado = estado, Pregunta = pregunta };
}

/// <summary>
/// Tabla determinista dePATCHS de tamaño y de confirmaciones.
/// Sin LLM. Este es el punto donde se resuelve la coreferencia.
/// </summary>
public static class Cuantificador
{
    private static readonly Dictionary<string, decimal> Parches = new(StringComparer.Ordinal)
    {
        ["grande"] = 1.0m, ["grandes"] = 1.0m, ["enorme"] = 1.0m, ["gigante"] = 1.0m,
        ["chica"] = 0.5m, ["chicas"] = 0.5m, ["pequena"] = 0.5m, ["pequeña"] = 0.5m, ["chiquita"] = 0.5m,
        ["mediana"] = 0.75m, ["normal"] = 0.75m, ["promedio"] = 0.75m,
        ["mitad"] = 0.5m, ["media"] = 0.5m, ["doble"] = 2.0m, ["triple"] = 3.0m,
    };

    // Palabras que confirman lo que el modelo ya propuso, sin aportar dato nuevo.
    private static readonly HashSet<string> Confirmaciones = new(StringComparer.Ordinal)
    {
        "si", "dale", "ok", "oka", "va", "yes", "claro", "correcto", "correcta", "exacto", "exacto",
        "esa", "ese", "esa misma", "asi", "bien", "hecho", "confirmo", "listo", "sí", "si", "justo",
    };

    private static readonly HashSet<string> Cancelaciones = new(StringComparer.Ordinal)
    {
        "cancela", "cancélalo", "cancelar", "olvidalo", "olvídalo", "olvida", "olvídalo ya",
        "nada", "soltar", "suelta", "descartar", "descarta", "ya no", "dejalo", "déjalo",
    };

    private static readonly Regex Numero = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex CantidadConUnidad = new(
        @"(\d+(?:[.,]\d+)?)\s*(g|gr|gramos?|ml|mililitros?|pza|piezas?|unidad|unidades|tazas?|l|litros?|kg|kilos?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Unidad canónica (código de UnidadMedida) a partir del texto.</summary>
    public static string? UnidadDesdeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().ToLowerInvariant();
        if (t is "g" or "gr" or "gramo" or "gramos") return "G";
        if (t is "kg" or "kilo" or "kilos") return "KG";
        if (t is "ml" or "mililitro" or "mililitros") return "ML";
        if (t is "l" or "litro" or "litros") return "L";
        if (t is "pza" or "pzas" or "pieza" or "piezas") return "PIEZA";
        if (t is "taza" or "tazas") return "TAZA";
        if (t is "unidad" or "unidades" or "u") return "PIEZA";
        return null;
    }

    /// <summary>
    /// Normaliza la unidad que propone el LLM al código de UnidadMedida.
    ///
    /// El JSON Schema puede forzar el enum de `intencion` (valores cerrados que
    /// el código conoce) pero NO puede restringir `unidad` a los códigos de la
    /// tabla: son datos, no constantes del schema. El modelo responde "unidad",
    /// "pieza", "gr" indistintamente, y MealRegistrar busca por código exacto.
    /// Sin esta normalización el registro falla con "Unidad desconocida".
    /// </summary>
    public static string? NormalizarUnidad(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().ToLowerInvariant();

        // Ya es un código válido en mayúsculas.
        var comoCodigo = t.ToUpperInvariant();
        if (codigosConocidos.Contains(comoCodigo)) return comoCodigo;

        return UnidadDesdeTexto(t);
    }

    /// <summary>Códigos de UnidadMedida existentes en el catálogo.</summary>
    private static readonly HashSet<string> codigosConocidos = new(StringComparer.Ordinal)
    { "G", "KG", "ML", "L", "PIEZA", "TAZA", "KCAL" };

    /// <summary>True si el mensaje NO abre un form nuevo (es una respuesta corta).</summary>
    public static bool EsRespuestaCorta(string texto)
    {
        var t = texto.Trim().ToLowerInvariant();
        if (t.Length == 0) return true;
        if (t.Length > 60) return false;

        // Confirmación / cancelación explícita: siempre respuesta corta.
        // Se comparan con y sin acentos porque el usuario escribe como habla.
        if (Confirmaciones.Contains(t) || Cancelaciones.Contains(t)
            || Confirmaciones.Contains(SinTildes(t)) || Cancelaciones.Contains(SinTildes(t)))
            return true;

        // Patch de tamaño o cantidad: "50ml", "la grande", "2 piezas".
        if (PatchDeTamano(t) is not null) return true;
        if (CantidadConUnidad.IsMatch(t)) return true;
        // Número suelto: "3", "100".
        if (Numero.IsMatch(t) && t.Length <= 12) return true;

        // Una sola palabra sin número: casi seguro responde al form.
        return !t.Contains(' ');
    }

    public static string SinTildes(string s) => s.Replace("í", "i").Replace("á", "a").Replace("é", "e")
        .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

    public static bool EsCancelacion(string texto)
    {
        var t = texto.Trim().ToLowerInvariant();
        return Cancelaciones.Contains(t) || Cancelaciones.Contains(SinTildes(t));
    }

    public static bool EsConfirmacion(string texto)
    {
        var t = texto.Trim().ToLowerInvariant();
        return Confirmaciones.Contains(t) || Confirmaciones.Contains(SinTildes(t));
    }

    /// <summary>
    /// Extrae (cantidad, unidad) de un texto corto. Soporta "50ml", "2 piezas",
    /// "3 tazas", "un poco" no (devuelve null cantidad).
    /// </summary>
    public static (decimal? Cantidad, string? Unidad)? ExtraerCantidadUnidad(string texto)
    {
        var m = CantidadConUnidad.Match(texto);
        if (m.Success)
        {
            var n = decimal.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            return (n, UnidadDesdeTexto(m.Groups[2].Value));
        }
        var solo = Numero.Match(texto);
        if (solo.Success)
        {
            var n = decimal.Parse(solo.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            return (n, null);
        }
        return null;
    }

    /// <summary>
    /// Devuelve el patch de tamaño si el texto lo contiene: "la grande" → 1.0.
    /// Interpretado como FRACCIÓN de la porción, no como cantidad absoluta.
    /// </summary>
    public static decimal? PatchDeTamano(string texto)
    {
        var t = texto.Trim().ToLowerInvariant();
        foreach (var (k, v) in Parches)
            if (t.Contains(k.Trim(), StringComparison.Ordinal)) return v;
        return null;
    }

    /// <summary>Lista de patches conocidos (para el SystemPrompt y los tests).</summary>
    public static IReadOnlyCollection<string> ParchesConocidos => Parches.Keys;
}

/// <summary>
/// Motor del form. puro, sin dependencias: testeable sin DB ni LLM.
/// </summary>
public static class MealSlotTracker
{
    /// <summary>
    /// Avanza el form con un mensaje corto del usuario.
    /// Devuelve Reconocido=false si el mensaje no era una respuesta al form.
    /// </summary>
    public static ResultadoSlot Avanzar(ComidaEnCurso estado, string texto)
    {
        if (!estado.Activa) return ResultadoSlot.NoReconocido(estado);

        // Un mensaje que NO es respuesta corta cierra cualquier form en curso:
        // es una frase nueva, no un refinamiento del item pendiente.
        if (!Cuantificador.EsRespuestaCorta(texto))
            return ResultadoSlot.NoReconocido(estado);

        if (Cuantificador.EsCancelacion(texto))
        {
            estado.Items.Clear();
            estado.Estado = EstadoSlot.Cancelado;
            return ResultadoSlot.NoReconocido(estado);
        }

        var pendiente = estado.Pendiente;

        // Confirmación pura: no cambia nada, pero confirma que sefillslot.
        if (Cuantificador.EsConfirmacion(texto) && Cuantificador.ExtraerCantidadUnidad(texto) is null
            && Cuantificador.PatchDeTamano(texto) is null)
        {
            if (pendiente is not null) pendiente.ConfirmadoPorTracker = true;
            Recalcular(estado);
            return new ResultadoSlot
            {
                Reconocido = true,
                Completo = estado.Estado == EstadoSlot.Completo,
                Estado = estado,
                Pregunta = PreguntaPara(estado),
            };
        }

        // Patch de tamaño: resuelve la cantidad del item pendiente.
        var patch = Cuantificador.PatchDeTamano(texto);
        if (patch is not null && pendiente is not null)
        {
            pendiente.Cantidad = patch.Value;
            pendiente.ConfirmadoPorTracker = true;
            Recalcular(estado);
            return new ResultadoSlot
            {
                Reconocido = true,
                Completo = estado.Estado == EstadoSlot.Completo,
                Estado = estado,
                Pregunta = PreguntaPara(estado),
            };
        }

        // Cantidad (+ unidad si viene pegada): "50ml", "2 piezas".
        var extraccion = Cuantificador.ExtraerCantidadUnidad(texto);
        if (extraccion is not null && pendiente is not null)
        {
            var (cantidad, unidad) = extraccion.Value;
            if (cantidad is not null && cantidad > 0) pendiente.Cantidad = cantidad;
            if (unidad is not null) pendiente.Unidad = unidad;
            pendiente.ConfirmadoPorTracker = true;
            Recalcular(estado);
            return new ResultadoSlot
            {
                Reconocido = true,
                Completo = estado.Estado == EstadoSlot.Completo,
                Estado = estado,
                Pregunta = PreguntaPara(estado),
            };
        }

        return ResultadoSlot.NoReconocido(estado, PreguntaPara(estado));
    }

    /// <summary>
    /// Recalcula el estado global tras mutar un slot. Sin esto, un item que
    /// pasa de pendiente a completo dejaba el form en "EsperandoCantidad"
    /// para siempre y nunca se persistía.
    /// </summary>
    public static EstadoSlot Recalcular(ComidaEnCurso e)
    {
        // NO se adivina la unidad. "3" puede ser 3 piezas o 3 gramos: asumir
        // gramos produciría calorías silenciosamente erradas, que es peor que
        // preguntar. El slot Unidad queda abierto y se pregunta.
        e.Estado = PendienteEstado(e);
        return e.Estado;
    }

    /// <summary>
    /// Abre o extiende un form con los candidatos que propuso el LLM.
    /// Los items con cantidad y unidad quedan completos; los demás quedan
    /// pendientes de confirmación por el tracker.
    /// </summary>
    public static ComidaEnCurso Abrir(IEnumerable<(string Nombre, decimal? Cantidad, string? Unidad)> candidatos)
    {
        var estado = new ComidaEnCurso();
        foreach (var (nombre, cantidad, unidad) in candidatos)
        {
            // La unidad se conserva aunque falte la cantidad: si el modelo ya dijo
            // "1 PIEZA" (la porción del catálogo), esa unidad es válida y
            // descartarla obligaría a preguntar de nuevo algo que ya se sabe.
            estado.Items.Add(new ItemEnCurso
            {
                Nombre = nombre,
                Cantidad = cantidad,
                Unidad = Cuantificador.NormalizarUnidad(unidad),
            });
        }
        estado.Estado = PendienteEstado(estado);
        return estado;
    }

    private static EstadoSlot PendienteEstado(ComidaEnCurso e)
    {
        if (e.Items.Count == 0) return EstadoSlot.EsperandoAlimento;
        if (e.Items.Any(i => i.Cantidad is null)) return EstadoSlot.EsperandoCantidad;
        if (e.Items.Any(i => i.Unidad is null)) return EstadoSlot.EsperandoUnidad;
        return EstadoSlot.Completo;
    }

    /// <summary>
    /// Pregunta por el slot EXACTO que falta. Es la diferencia entre
    /// "no pude" y "¿cuántas tortillas?".
    /// </summary>
    public static string? PreguntaPara(ComidaEnCurso e)
    {
        var pendiente = e.Pendiente;
        if (pendiente is null) return null;
        return pendiente.SlotPendiente switch
        {
            SlotAlimento.Cantidad => $"¿Cuántas {FoodTextNormalizer.Singularize(pendiente.Nombre.ToLowerInvariant())}?",
            SlotAlimento.Unidad => $"¿En qué unidad, {pendiente.Nombre.ToLowerInvariant()}?",
            _ => null,
        };
    }

    /// <summary>Items listos para persistir. Vacío si algo quedó sin completar.</summary>
    public static IReadOnlyList<(string Nombre, decimal Cantidad, string Unidad)> Listos(ComidaEnCurso e)
    {
        if (e.Estado != EstadoSlot.Completo) return Array.Empty<(string, decimal, string)>();
        return e.Items
            .Where(i => i.Cantidad is not null && i.Unidad is not null)
            .Select(i => (i.Nombre, i.Cantidad!.Value, i.Unidad!))
            .ToList();
    }
}