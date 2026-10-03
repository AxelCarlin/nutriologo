namespace Nutriologo.Domain.Recipes;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

public class RecetaAlimento : AuditableEntity
{
    public int IdAlimento { get; private set; }
    public decimal? PesoInicial { get; private set; }
    public decimal? PesoFinal { get; private set; }
    public int? IdUnidadMedidaPeso { get; private set; }
    public decimal? CantidadPorciones { get; private set; }
    public decimal? PesoPorPorcion { get; private set; }
}

public class IngredienteReceta : AuditableEntity
{
    public int IdRecetaAlimento { get; private set; }
    public int? IdAlimento { get; private set; }
    public int? IdRecetaComponente { get; private set; }
    public decimal Cantidad { get; private set; }
    public int IdUnidadMedida { get; private set; }
    public decimal? CantidadGramos { get; private set; }
    public int Orden { get; private set; }
    public bool EsOpcional { get; private set; }
    public string? Descripcion { get; private set; }

    private IngredienteReceta() { }

    public IngredienteReceta(int idRecetaAlimento, int? idAlimento, int? idRecetaComponente,
        decimal cantidad, int idUnidadMedida, int orden, decimal? cantidadGramos = null, bool esOpcional = false, string? descripcion = null)
    {
        IdRecetaAlimento = idRecetaAlimento;
        IdAlimento = idAlimento;
        IdRecetaComponente = idRecetaComponente;
        Cantidad = cantidad;
        IdUnidadMedida = idUnidadMedida;
        Orden = orden;
        CantidadGramos = cantidadGramos;
        EsOpcional = esOpcional;
        Descripcion = descripcion;
        ValidarXor();
    }

    public void ValidarXor()
    {
        bool alimento = IdAlimento is not null;
        bool receta = IdRecetaComponente is not null;
        if (alimento == receta)
            throw new DomainException("IngredienteReceta: exactamente uno de IdAlimento o IdRecetaComponente debe estar informado (XOR).");
        if (Cantidad <= 0) throw new DomainException("IngredienteReceta: cantidad debe ser positiva.");
    }
}

public class RecetaUsuario : AuditableEntity
{
    public int IdUsuario { get; private set; }
    public int IdRecetaAlimento { get; private set; }
    public string? NombrePersonalizado { get; private set; }
    public string? DescripcionPersonalizada { get; private set; }
}
