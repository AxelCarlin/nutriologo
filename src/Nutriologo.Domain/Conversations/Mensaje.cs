namespace Nutriologo.Domain.Conversations;

using Nutriologo.Domain.Common;

/// <summary>
/// Mensaje dentro de una conversación. Mapea la tabla "Mensaje".
/// Persistir el historial sirve para tres cosas concretas: depurar por qué el
/// modelo respondió X, alimentar EvaluacionMensaje, y reconstruir el estado del
/// form si el proceso se reinicia entre turnos.
/// </summary>
public class Mensaje : AuditableEntity
{
    public int IdConversacion { get; private set; }
    /// <summary>Id de RolMensaje (CATALOGO): USER | ASSISTANT | SYSTEM.</summary>
    public int IdRolMensaje { get; private set; }
    /// <summary>Id de TipoMensaje (CATALOGO): TEXTO | CLARIFICACION | REGISTRO | ERROR.</summary>
    public int IdTipoMensaje { get; private set; }
    /// <summary>Id de ModeloIA (CATALOGO), opcional: solo para mensajes del asistente.</summary>
    public int? IdModeloIA { get; private set; }
    public string Contenido { get; private set; } = "";
    public DateTime FechaMensaje { get; private set; }
    public int? TokensEntrada { get; private set; }
    public int? TokensSalida { get; private set; }
    public long? DuracionMs { get; private set; }

    private Mensaje() { }

    public Mensaje(int idConversacion, int idRolMensaje, int idTipoMensaje, string contenido,
        DateTime fecha, int idUsuarioCrea = 1, int? idModeloIA = null, int? tokensEntrada = null,
        int? tokensSalida = null, long? duracionMs = null)
    {
        IdConversacion = idConversacion;
        IdRolMensaje = idRolMensaje;
        IdTipoMensaje = idTipoMensaje;
        IdModeloIA = idModeloIA;
        Contenido = contenido;
        FechaMensaje = fecha;
        TokensEntrada = tokensEntrada;
        TokensSalida = tokensSalida;
        DuracionMs = duracionMs;
        FechaCreacion = fecha;
        FechaModificacion = fecha;
        IdUsuarioCrea = idUsuarioCrea;
        Estatus = 1;
        DatosValidos();
    }

    public void DatosValidos()
    {
        if (IdConversacion <= 0) throw new Exceptions.DomainException("IdConversacion inválido.");
        if (IdRolMensaje <= 0) throw new Exceptions.DomainException("IdRolMensaje inválido.");
        if (IdTipoMensaje <= 0) throw new Exceptions.DomainException("IdTipoMensaje inválido.");
        if (string.IsNullOrWhiteSpace(Contenido)) throw new Exceptions.DomainException("Contenido vacío.");
    }
}

/// <summary>
/// Estado del form "comida en curso", serializado a JSON en una columna.
///
/// Se eligió una columna JSON en vez de una tabla SlotEnCurso nueva porque el
/// form tiene forma fija (alimento/cantidad/unidad) y una sola fila activa por
/// conversación. Una tabla normalizada solo sumaría joins para un shape
/// conocido y acotado.
/// </summary>
public sealed class MealDraftSnapshot
{
    public string Estado { get; init; } = "Inactivo";
    public DateTime ActualizadoEn { get; init; }
    public List<DraftItem> Items { get; init; } = new();

    public sealed class DraftItem
    {
        public string Nombre { get; init; } = "";
        public decimal? Cantidad { get; init; }
        public string? Unidad { get; init; }
        public bool ConfirmadoPorTracker { get; init; }
    }
}