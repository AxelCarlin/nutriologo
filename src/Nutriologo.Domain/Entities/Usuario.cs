namespace Nutriologo.Domain.Entities;

using Nutriologo.Domain.Common;

public class Usuario : AuditableEntity
{
    public string Nombre { get; private set; } = default!;
    public string Correo { get; private set; } = default!;
    public string ContrasenaHash { get; private set; } = default!;

    private Usuario() { }

    public Usuario(string nombre, string correo, string contrasenaHash, int idUsuarioCrea)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new Exceptions.DomainException("Nombre requerido.");
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@')) throw new Exceptions.DomainException("Correo inválido.");
        Nombre = nombre;
        Correo = correo;
        ContrasenaHash = contrasenaHash;
        FechaCreacion = DateTime.UtcNow;
        FechaModificacion = DateTime.UtcNow;
        Estatus = 1;
        IdUsuarioCrea = idUsuarioCrea;
    }
}
