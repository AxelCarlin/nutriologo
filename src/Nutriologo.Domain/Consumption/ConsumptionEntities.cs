namespace Nutriologo.Domain.Consumption;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

/// <summary>CONSUMIDO: evento real. Inmutable en contenido; las correcciones se hacen
/// creando un nuevo registro y (opcionalmente) cancelando el anterior vía Estatus.
/// Nunca se sobrescriben cantidades ni calorías del registro histórico.</summary>
public class RegistroComida : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int? IdTipoComida { get; private set; }
    public DateTime FechaConsumo { get; private set; }
    public string? Nombre { get; private set; }
    public string? Descripcion { get; private set; }
    public decimal? CaloriasEstimadas { get; private set; }
    public string? Observacion { get; private set; }

    public void DatosValidos()
    {
        if (FechaConsumo == default) throw new DomainException("FechaConsumo requerida.");
        if (CaloriasEstimadas is not null && CaloriasEstimadas < 0) throw new DomainException("CaloriasEstimadas no puede ser negativa.");
    }

    public RegistroComida() { }

    public RegistroComida(int idUsuario, DateTime fechaConsumo, string? nombre = null, int? idTipoComida = null,
        decimal? caloriasEstimadas = null, string? descripcion = null, string? observacion = null, int idUsuarioCrea = 1)
    {
        IdUsuario = idUsuario; FechaConsumo = fechaConsumo; Nombre = nombre; IdTipoComida = idTipoComida;
        CaloriasEstimadas = caloriasEstimadas; Descripcion = descripcion; Observacion = observacion;
        IdUsuarioCrea = idUsuarioCrea; FechaCreacion = DateTime.UtcNow; FechaModificacion = DateTime.UtcNow; Estatus = 1;
        DatosValidos();
    }
}

public class AlimentoRegistroComida : AuditableEntity
{
    public int IdRegistroComida { get; private set; }
    public int IdAlimento { get; private set; }
    public int IdUnidadMedida { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal? CantidadGramos { get; private set; }
    public decimal? CaloriasEstimadas { get; private set; }
    public decimal? Confianza { get; private set; }
    public string? Observacion { get; private set; }

    public void DatosValidos()
    {
        if (Cantidad <= 0) throw new DomainException("Cantidad consumida debe ser positiva.");
        if (CaloriasEstimadas is not null && CaloriasEstimadas < 0) throw new DomainException("CaloriasEstimadas no puede ser negativa.");
        if (Confianza is not null && (Confianza < 0 || Confianza > 1)) throw new DomainException("Confianza debe estar en [0,1].");
    }

    public AlimentoRegistroComida() { }

    public AlimentoRegistroComida(int idRegistroComida, int idAlimento, int idUnidadMedida, decimal cantidad,
        decimal? caloriasEstimadas = null, decimal? confianza = null, string? observacion = null, int idUsuarioCrea = 1)
    {
        IdRegistroComida = idRegistroComida; IdAlimento = idAlimento; IdUnidadMedida = idUnidadMedida;
        Cantidad = cantidad; CaloriasEstimadas = caloriasEstimadas; Confianza = confianza;
        Observacion = observacion; IdUsuarioCrea = idUsuarioCrea;
        FechaCreacion = DateTime.UtcNow; FechaModificacion = DateTime.UtcNow; Estatus = 1;
        DatosValidos();
    }
}

public class BalanceCalorico : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int? IdMetaCalorica { get; private set; }
    public DateTime FechaBalance { get; private set; }
    public decimal CaloriasConsumidas { get; private set; }
    public decimal? CaloriasObjetivo { get; private set; }
    public decimal? Balance { get; private set; }
    public string? Observacion { get; private set; }
}

public static class BalanceService
{
    /// <summary>Balance determinista: consumidas − objetivo. No depende de IA.</summary>
    public static (decimal Consumidas, decimal? Objetivo, decimal? Balance) Calcular(decimal consumidas, decimal? objetivo)
    {
        if (consumidas < 0) throw new DomainException("CaloriasConsumidas no puede ser negativa.");
        if (objetivo is not null && objetivo <= 0) throw new DomainException("CaloriasObjetivo debe ser positiva.");
        return (consumidas, objetivo, objetivo is null ? null : consumidas - objetivo);
    }
}
