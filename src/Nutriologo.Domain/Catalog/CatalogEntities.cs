namespace Nutriologo.Domain.Catalog;

using Nutriologo.Domain.Common;

public class CatalogoBase : AuditableEntity
{
    public string Codigo { get; protected set; } = default!;
    public string Descripcion { get; protected set; } = default!;
}

public class FuenteAlimento : CatalogoBase
{
    public string? UrlFuente { get; private set; }
}

public class CategoriaAlimento : CatalogoBase
{
    public int? IdCategoriaPadre { get; private set; }
    public int Nivel { get; private set; }
}

public class CocinaAlimentaria : CatalogoBase { }

public class Marca : AuditableEntity
{
    public string Nombre { get; private set; } = default!;
    public string? Descripcion { get; private set; }
}

public class EstadoPreparacionAlimento : CatalogoBase { }

public class MetodoPreparacion : CatalogoBase { }

public class Alimento : AuditableEntity
{
    public int IdCategoriaAlimento { get; private set; }
    public int? IdMarca { get; private set; }
    public int? IdEstadoPreparacionAlimento { get; private set; }
    public int? IdMetodoPreparacion { get; private set; }
    public string Nombre { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public bool EsReceta { get; private set; }
}

public class AlimentoCocina : AuditableEntity
{
    public int IdAlimento { get; private set; }
    public int IdCocinaAlimentaria { get; private set; }
    public bool EsPrincipal { get; private set; }
}

public class AlimentoFuente : AuditableEntity
{
    public int IdAlimento { get; private set; }
    public int IdFuenteAlimento { get; private set; }
    public int? IdMarca { get; private set; }
    public string? CodigoExterno { get; private set; }
    public string? VersionFuente { get; private set; }
    public string? NombreFuente { get; private set; }
    public string? DescripcionFuente { get; private set; }
    public DateTime? FechaReferencia { get; private set; }
}

public class TipoNutriente : CatalogoBase { }

public class Nutriente : AuditableEntity
{
    public int IdTipoNutriente { get; private set; }
    public int IdUnidadMedida { get; private set; }
    public string Codigo { get; private set; } = default!;
    public string Descripcion { get; private set; } = default!;
}

public class AlimentoNutriente : AuditableEntity
{
    public int IdAlimentoFuente { get; private set; }
    public int IdNutriente { get; private set; }
    public decimal Cantidad { get; private set; }
}

public class TipoUnidadMedida : CatalogoBase { }

public class UnidadMedida : AuditableEntity
{
    public int IdTipoUnidadMedida { get; private set; }
    public string Codigo { get; private set; } = default!;
    public string Descripcion { get; private set; } = default!;
    public string? Simbolo { get; private set; }
}

public class BaseNutricionalAlimento : AuditableEntity
{
    public int IdAlimentoFuente { get; private set; }
    public decimal CantidadBase { get; private set; }
    public int IdUnidadMedida { get; private set; }
}

public class ConversionUnidad : AuditableEntity
{
    public int IdUnidadOrigen { get; private set; }
    public int IdUnidadDestino { get; private set; }
    public decimal FactorConversion { get; private set; }
    public string? Formula { get; private set; }
}

public class PorcionAlimento : AuditableEntity
{
    public int IdAlimento { get; private set; }
    public int IdUnidadMedida { get; private set; }
    public string Descripcion { get; private set; } = default!;
    public decimal Cantidad { get; private set; }
    public decimal? CantidadGramos { get; private set; }
}
