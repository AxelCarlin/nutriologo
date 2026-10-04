namespace Nutriologo.Domain.Entities;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

public class PerfilUsuario : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int IdSexoBiologico { get; private set; }
    public int IdNivelActividadFisica { get; private set; }
    public DateTime FechaNacimiento { get; private set; }
    public decimal Altura { get; private set; }
    public string AlturaUnidad { get; private set; } = default!;
    public DateTime FechaInicioPerfil { get; private set; }

    private PerfilUsuario() { }

    public PerfilUsuario(int idUsuario, int idSexoBiologico, int idNivelActividadFisica,
        DateTime fechaNacimiento, decimal altura, string alturaUnidad, DateTime fechaInicioPerfil, int idUsuarioCrea)
    {
        if (altura <= 0) throw new DomainException("Altura debe ser positiva.");
        if (fechaNacimiento > DateTime.UtcNow) throw new DomainException("FechaNacimiento no puede ser futura.");
        IdUsuario = idUsuario; IdSexoBiologico = idSexoBiologico; IdNivelActividadFisica = idNivelActividadFisica;
        FechaNacimiento = fechaNacimiento; Altura = altura; AlturaUnidad = alturaUnidad;
        FechaInicioPerfil = fechaInicioPerfil; IdUsuarioCrea = idUsuarioCrea;
        FechaCreacion = DateTime.UtcNow; FechaModificacion = DateTime.UtcNow; Estatus = 1;
    }
}

public class MedicionCorporal : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int IdTipoMedicionCorporal { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime FechaMedicion { get; private set; }
    public string? Observacion { get; private set; }

    private MedicionCorporal() { }

    public MedicionCorporal(int idUsuario, int idTipoMedicionCorporal, decimal valor, DateTime fechaMedicion, string? observacion, int idUsuarioCrea)
    {
        if (valor < 0) throw new DomainException("Valor no puede ser negativo.");
        IdUsuario = idUsuario; IdTipoMedicionCorporal = idTipoMedicionCorporal; Valor = valor;
        FechaMedicion = fechaMedicion; Observacion = observacion; IdUsuarioCrea = idUsuarioCrea;
        FechaCreacion = DateTime.UtcNow; FechaModificacion = DateTime.UtcNow; Estatus = 1;
    }
}
