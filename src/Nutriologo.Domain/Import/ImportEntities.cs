namespace Nutriologo.Domain.Import;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

public class TipoEntradaPlan : Domain.Catalog.CatalogoBase { }

/// <summary>Proceso de importación: entrada original + interpretación IA candidata.
/// NO es el plan final; solo tras validación se crea PlanAlimenticio.</summary>
public class ImportacionPlan : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int IdTipoEntradaPlan { get; private set; }
    public int? IdModeloIA { get; private set; }
    public string EntradaOriginal { get; private set; } = default!;
    public string? InterpretacionJson { get; private set; }
    public string? Resultado { get; private set; }
    public decimal? Confianza { get; private set; }
    public DateTime FechaImportacion { get; private set; }

    public void DatosValidos()
    {
        if (string.IsNullOrWhiteSpace(EntradaOriginal)) throw new DomainException("EntradaOriginal requerida.");
        if (Confianza is not null && (Confianza < 0 || Confianza > 1)) throw new DomainException("Confianza fuera de [0,1].");
    }
}

public class ImportacionPlanImagen : AuditableEntity
{
    public int IdImportacionPlan { get; private set; }
    public int IdImagen { get; private set; }
    public int Orden { get; private set; }
}
