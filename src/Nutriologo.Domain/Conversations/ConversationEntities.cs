namespace Nutriologo.Domain.Conversations;

using Nutriologo.Domain.Common;

/// <summary>
/// Conversación de chat. Mapea la tabla "Conversacion" del schema, que existía
/// sin entidad EF desde el diseño original.
/// </summary>
public class Conversacion : AuditableEntity
{
    public int IdUsuario { get; private set; }
    /// <summary>Id de TipoInteraccion (CATALOGO).</summary>
    public int IdTipoInteraccion { get; private set; }
    public string? Titulo { get; private set; }
    public DateTime FechaInicio { get; private set; }
    public DateTime? FechaFin { get; private set; }

    private Conversacion() { }

    public Conversacion(int idUsuario, int idTipoInteraccion, DateTime inicio, int idUsuarioCrea = 1)
    {
        IdUsuario = idUsuario;
        IdTipoInteraccion = idTipoInteraccion;
        FechaInicio = inicio;
        FechaCreacion = inicio;
        FechaModificacion = inicio;
        IdUsuarioCrea = idUsuarioCrea;
        Estatus = 1;
        DatosValidos();
    }

    public void Cerrar(DateTime fin)
    {
        FechaFin = fin;
        FechaModificacion = fin;
    }

    public void Tocar(DateTime ahora) => FechaModificacion = ahora;

    public void DatosValidos()
    {
        if (IdUsuario <= 0) throw new Exceptions.DomainException("IdUsuario inválido.");
        if (IdTipoInteraccion <= 0) throw new Exceptions.DomainException("IdTipoInteraccion inválido.");
    }
}