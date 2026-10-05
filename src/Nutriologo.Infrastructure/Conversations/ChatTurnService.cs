namespace Nutriologo.Infrastructure.Conversations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Conversations;
using Nutriologo.Infrastructure.Persistence;

/// <summary>
/// Estado del turno de chat: qué persistimos y qué se le devuelve al cliente.
/// </summary>
public sealed record TurnoChat(
    ComidaEnCurso Estado,
    IReadOnlyList<(string Nombre, decimal Cantidad, string Unidad)> Listos,
    bool Completo,
    string? Pregunta);

/// <summary>
/// Orquesta el form de comida en curso y su persistencia.
///
/// Separa dos responsabilidades que antes estaban mezcladas en el LLM:
///   - El LLM extrae ALIMENTOS cuando el mensaje es una frase nueva.
///   - Este servicio resuelve las respuestas cortas ("sí, la grande") con la
///     tabla determinista de Cuantificador, y persiste el estado para que el
///     form sobreviva a un reinicio del proceso.
///
/// Aislamiento: toda lectura/escritura filtra por IdUsuario. Un draft nunca
/// puede ser leído ni llenado por otro usuario (mismo criterio anti-BOLA que
/// aplica el resto de la API).
/// </summary>
public sealed class ChatTurnService
{
    private const int IdRolUser = 1;
    private const int IdRolAssistant = 2;
    private const int IdTipoTexto = 1;
    private const int IdTipoClarificacion = 2;
    private const int IdTipoRegistro = 3;
    private const int IdTipoError = 4;
    private const int IdInteraccionChat = 1;

    /// <summary>Un draft más viejo que esto se considera zombi y se descarta.</summary>
    public static readonly TimeSpan VigenciaDraft = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;

    public ChatTurnService(AppDbContext db) => _db = db;

    /// <summary>
    /// Obtiene (o crea) la conversación abierta del usuario. Si la anterior
    /// venció, la cierra y arranca una nueva.
    /// </summary>
    public async Task<Conversacion> ObtenerConversacionAsync(int idUsuario, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;

        var abierta = await _db.Conversaciones
            .Where(c => c.IdUsuario == idUsuario && c.FechaFin == null)
            .OrderByDescending(c => c.FechaInicio)
            .FirstOrDefaultAsync(ct);

        if (abierta is not null && ahora - abierta.FechaInicio > TimeSpan.FromHours(12))
        {
            abierta.Cerrar(ahora);
            await _db.SaveChangesAsync(ct);
            return await CrearAsync(idUsuario, ahora, ct);
        }

        return abierta ?? await CrearAsync(idUsuario, ahora, ct);
    }

    private async Task<Conversacion> CrearAsync(int idUsuario, DateTime ahora, CancellationToken ct)
    {
        var c = new Conversacion(idUsuario, IdInteraccionChat, ahora, idUsuario);
        _db.Conversaciones.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    /// <summary>Lee el draft persistido. Devuelve uno nuevo si no hay o venció.</summary>
    public async Task<ComidaEnCurso> LeerDraftAsync(Conversacion conversacion, CancellationToken ct = default)
    {
        var raw = await _db.Conversaciones
            .Where(c => c.Id == conversacion.Id && c.IdUsuario == conversacion.IdUsuario)
            .Select(c => EF.Property<string>(c, "MealDraft"))
            .FirstOrDefaultAsync(ct);

        return Deserializar(raw);
    }

    public async Task GuardarDraftAsync(Conversacion conversacion, ComidaEnCurso estado, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        _db.Entry(conversacion).Property("MealDraft").CurrentValue = Serializar(estado, ahora);
        conversacion.Tocar(ahora);
        await _db.SaveChangesAsync(ct);
    }

    public async Task LimpiarDraftAsync(Conversacion conversacion, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        _db.Entry(conversacion).Property("MealDraft").CurrentValue = null;
        conversacion.Tocar(ahora);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Mensajes ----------

    public Task GuardarMensajeAsync(Conversacion conversacion, bool esUsuario, string contenido,
        int idTipo = 0, long? duracionMs = null, CancellationToken ct = default)
    {
        _db.Mensajes.Add(new Mensaje(
            conversacion.Id,
            esUsuario ? IdRolUser : IdRolAssistant,
            idTipo == 0 ? IdTipoTexto : idTipo,
            contenido,
            DateTime.UtcNow,
            conversacion.IdUsuario,
            idModeloIA: null,
            tokensEntrada: null,
            tokensSalida: null,
            duracionMs: duracionMs));
        return _db.SaveChangesAsync(ct);
    }

    // ---------- Turno ----------

    /// <summary>
    /// Procesa un mensaje del usuario contra el draft activo.
    /// Si el mensaje NO es respuesta corta, devuelve null para que el caller
    /// pregunte al LLM por alimentos nuevos.
    /// </summary>
    public async Task<TurnoChat?> IntentarAvanzarAsync(int idUsuario, string texto, CancellationToken ct = default)
    {
        if (!Cuantificador.EsRespuestaCorta(texto)) return null;

        var conversacion = await ObtenerConversacionAsync(idUsuario, ct);
        var estado = await LeerDraftAsync(conversacion, ct);
        if (!estado.Activa) return null;

        var r = MealSlotTracker.Avanzar(estado, texto);

        if (!r.Reconocido && estado.Estado == EstadoSlot.Cancelado)
        {
            await LimpiarDraftAsync(conversacion, ct);
            return new TurnoChat(estado, Array.Empty<(string, decimal, string)>(), false,
                "Ok, lo dejamos así.");
        }

        if (!r.Reconocido) return null;

        // La unidad pendiente se resuelve contra el catálogo ANTES de evaluar
        // si el form está completo. Si no, "la grande" daba "¿En qué unidad?"
        // por algo que PorcionAlimento ya sabe (Tortilla → PIEZA).
        await CompletarUnidadesDesdeCatalogoAsync(estado, ct);
        await GuardarDraftAsync(conversacion, estado, ct);

        var listos = MealSlotTracker.Listos(estado);
        return new TurnoChat(estado, listos, estado.Estado == EstadoSlot.Completo,
            MealSlotTracker.PreguntaPara(estado));
    }

    /// <summary>Abre un draft nuevo con los candidatos que propuso el LLM.</summary>
    public async Task<ComidaEnCurso> AbrirDraftAsync(int idUsuario,
        IEnumerable<(string Nombre, decimal? Cantidad, string? Unidad)> candidatos,
        CancellationToken ct = default)
    {
        var lista = candidatos.ToList();

        // El catálogo declara la porción natural de cada alimento (Tortilla =
        // 1 PIEZA, Arroz = 1 taza). Si el LLM dio cantidad pero no unidad,
        // usar esa porción evita preguntar algo que el catálogo ya sabe.
        // ANY() necesita un array tipado en el lado derecho; pasar string[] por Npgsql
        // lo manda como text y Postgres responde "op ANY/ALL requires array".

        // ANY() necesita un array en el lado derecho. Se tipa el parámetro como
        // NpgsqlDbType.Array: pasar string[] sin tipo hace que Npgsql lo mande
        // como text y Postgres falle con "malformed array literal".
        // El LLM responde "tortilla" en minúscula; el catálogo guarda "Tortilla".
        // Comparar con LOWER() evita que la porción no se encuentre.
        var pNombres = new NpgsqlParameter("nombres", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
        { Value = lista.Select(c => c.Nombre).ToArray() };

        var conPorcion = await _db.Database.SqlQueryRaw<PorcionAlimentoDto>(
            """
            SELECT a."Nombre"         AS "Nombre",
                   p."Cantidad"       AS "Cantidad",
                   p."CantidadGramos" AS "Gramos",
                   u."Codigo"        AS "Unidad"
            FROM "PorcionAlimento" p
            JOIN "Alimento" a          ON a."Id" = p."IdAlimento"
            LEFT JOIN "UnidadMedida" u ON u."Id" = p."IdUnidadMedida"
            WHERE LOWER(a."Nombre") = ANY (@nombres)
            """, pNombres).ToListAsync(ct);

        var porcionPorAlimento = conPorcion
            .GroupBy(p => p.Nombre.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var enriquecidos = lista.Select(c =>
        {
            if (c.Cantidad is null || c.Unidad is not null) return c;
            return porcionPorAlimento.TryGetValue(c.Nombre.ToLowerInvariant(), out var p)
                ? (c.Nombre, c.Cantidad, p.Unidad)
                : c;
        }).ToList();

        var conversacion = await ObtenerConversacionAsync(idUsuario, ct);
        var estado = MealSlotTracker.Abrir(enriquecidos);
        await GuardarDraftAsync(conversacion, estado, ct);
        return estado;
    }

    private sealed record PorcionAlimentoDto(string Nombre, decimal Cantidad, decimal? Gramos, string? Unidad);

    /// <summary>
    /// Tras resolver un patch ("la grande") queda cantidad pero puede faltar la
    /// unidad. Se completa con la porción del catálogo (Tortilla → PIEZA) en vez
    /// de exigirla al usuario: es un dato que el sistema ya tiene.
    /// </summary>
    public async Task CompletarUnidadesDesdeCatalogoAsync(ComidaEnCurso estado, CancellationToken ct = default)
    {
        var sinUnidad = estado.Items.Where(i => i.Cantidad is not null && i.Unidad is null).ToList();
        if (sinUnidad.Count == 0) return;

        var pNombres = new NpgsqlParameter("nombres", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
        { Value = sinUnidad.Select(i => i.Nombre).ToArray() };

        var conPorcion = await _db.Database.SqlQueryRaw<PorcionAlimentoDto>(
            """
            SELECT a."Nombre"         AS "Nombre",
                   p."Cantidad"       AS "Cantidad",
                   p."CantidadGramos" AS "Gramos",
                   u."Codigo"        AS "Unidad"
            FROM "PorcionAlimento" p
            JOIN "Alimento" a          ON a."Id" = p."IdAlimento"
            LEFT JOIN "UnidadMedida" u ON u."Id" = p."IdUnidadMedida"
            WHERE LOWER(a."Nombre") = ANY (@nombres)
            """, pNombres).ToListAsync(ct);

        var porNombre = conPorcion
            .GroupBy(p => p.Nombre.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var item in sinUnidad)
            if (item.Unidad is null
                && porNombre.TryGetValue(item.Nombre.ToLowerInvariant(), out var p) && p.Unidad is not null)
                item.Unidad = p.Unidad;

        MealSlotTracker.Recalcular(estado);
    }

    // ---------- Serialización ----------

    private static string? Serializar(ComidaEnCurso e, DateTime ahora) => e.Items.Count == 0 ? null :
        JsonSerializer.Serialize(new MealDraftSnapshot
        {
            Estado = e.Estado.ToString(),
            ActualizadoEn = ahora,
            Items = e.Items.Select(i => new MealDraftSnapshot.DraftItem
            {
                Nombre = i.Nombre,
                Cantidad = i.Cantidad,
                Unidad = i.Unidad,
                ConfirmadoPorTracker = i.ConfirmadoPorTracker,
            }).ToList(),
        }, Json);

    private static ComidaEnCurso Deserializar(string? raw)
    {
        var nuevo = new ComidaEnCurso();
        if (string.IsNullOrWhiteSpace(raw)) return nuevo;

        MealDraftSnapshot? snap;
        try { snap = JsonSerializer.Deserialize<MealDraftSnapshot>(raw, Json); }
        catch (JsonException) { return nuevo; }
        if (snap is null) return nuevo;

        // Draft zombi: si pasó la vigencia, se descarta en vez de preguntar sobre
        // algo que el usuario ya no recuerda.
        if (DateTime.UtcNow - snap.ActualizadoEn > VigenciaDraft) return nuevo;

        foreach (var i in snap.Items)
            nuevo.Items.Add(new ItemEnCurso
            {
                Nombre = i.Nombre,
                Cantidad = i.Cantidad,
                Unidad = i.Unidad,
                ConfirmadoPorTracker = i.ConfirmadoPorTracker,
            });

        nuevo.Estado = Enum.TryParse<EstadoSlot>(snap.Estado, out var st) ? st : EstadoSlot.Inactivo;
        return nuevo;
    }
}