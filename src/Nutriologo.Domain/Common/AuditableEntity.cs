namespace Nutriologo.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime FechaCreacion { get; protected set; }
    public DateTime FechaModificacion { get; protected set; }
    public short Estatus { get; protected set; }
    public int IdUsuarioCrea { get; protected set; }
    public int? IdUsuarioModifica { get; protected set; }
}
