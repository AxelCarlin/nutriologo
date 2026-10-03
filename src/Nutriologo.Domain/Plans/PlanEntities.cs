namespace Nutriologo.Domain.Plans;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

/// <summary>PLANIFICADO: lo que se pretende consumir. No representa consumo real.</summary>
public class PlanAlimenticio : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int IdTipoPlanAlimenticio { get; private set; }
    public int IdOrigenPlanAlimenticio { get; private set; }
    public int? IdProfesionalNutricion { get; private set; }
    public string Nombre { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public DateTime FechaInicio { get; private set; }
    public DateTime? FechaFin { get; private set; }
    public decimal? CaloriasObjetivo { get; private set; }

    public void DatosValidos()
    {
        if (string.IsNullOrWhiteSpace(Nombre)) throw new DomainException("Nombre de plan requerido.");
        if (FechaFin is not null && FechaFin < FechaInicio) throw new DomainException("FechaFin no puede ser anterior a FechaInicio.");
        if (CaloriasObjetivo is not null && CaloriasObjetivo <= 0) throw new DomainException("CaloriasObjetivo debe ser positiva.");
    }
}

public class DiaPlanAlimenticio : AuditableEntity
{
    public int IdPlanAlimenticio { get; private set; }
    public int NumeroDia { get; private set; }
    public DateTime? FechaPlan { get; private set; }
    public string? Descripcion { get; private set; }

    public void DatosValidos()
    {
        if (NumeroDia <= 0) throw new DomainException("NumeroDia debe ser positivo.");
    }
}

public class ComidaPlan : AuditableEntity
{
    public int IdDiaPlanAlimenticio { get; private set; }
    public int IdTipoComida { get; private set; }
    public string? Nombre { get; private set; }
    public DateTime? HoraSugerida { get; private set; }
    public string? Observaciones { get; private set; }
}

public class AlimentoComidaPlan : AuditableEntity
{
    public int IdComidaPlan { get; private set; }
    public int IdAlimento { get; private set; }
    public int IdUnidadMedida { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal? CantidadGramos { get; private set; }
    public int Orden { get; private set; }
    public string? Observacion { get; private set; }

    public void DatosValidos()
    {
        if (Cantidad <= 0) throw new DomainException("Cantidad planificada debe ser positiva.");
    }
}
