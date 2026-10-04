namespace Nutriologo.Domain.Feedback;

using Nutriologo.Domain.Common;
using Nutriologo.Domain.Exceptions;

/// <summary>Feedback de IA: dataset de evaluación histórico. NO entrena al modelo
/// automáticamente y NO modifica predicciones anteriores.</summary>
public class EvaluacionAnalisisIA : AuditableEntity
{
    public int IdAnalisisImagen { get; private set; }
    public int IdUsuario { get; private set; }
    public int IdTipoEvaluacionIA { get; private set; }
    public decimal? Puntuacion { get; private set; }
    public bool? EsCorrecto { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime FechaEvaluacion { get; private set; }

    public void DatosValidos()
    {
        if (Puntuacion is not null && (Puntuacion < 0 || Puntuacion > 10))
            throw new DomainException("Puntuacion fuera de rango [0,10].");
    }
}

public class CorreccionPrediccion : AuditableEntity
{
    public int IdPrediccionAlimento { get; private set; }
    public int IdUsuario { get; private set; }
    public int? IdAlimentoAnterior { get; private set; }
    public int? IdAlimentoCorregido { get; private set; }
    public decimal? CantidadAnterior { get; private set; }
    public decimal? CantidadCorregida { get; private set; }
    public string? Motivo { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime FechaCorreccion { get; private set; }

    public void DatosValidos()
    {
        bool cambiaAlimento = IdAlimentoAnterior != IdAlimentoCorregido && IdAlimentoCorregido is not null;
        bool cambiaCantidad = CantidadAnterior != CantidadCorregida && CantidadCorregida is not null;
        if (!cambiaAlimento && !cambiaCantidad)
            throw new DomainException("La corrección debe cambiar el alimento y/o la cantidad respecto a la predicción.");
        if (CantidadCorregida is not null && CantidadCorregida < 0)
            throw new DomainException("CantidadCorregida no puede ser negativa.");
    }
}

public class EvaluacionMensaje : AuditableEntity
{
    public int IdMensaje { get; private set; }
    public int IdUsuario { get; private set; }
    public decimal? Puntuacion { get; private set; }
    public bool? EsUtil { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime FechaEvaluacion { get; private set; }

    public void DatosValidos()
    {
        if (Puntuacion is not null && (Puntuacion < 0 || Puntuacion > 10))
            throw new DomainException("Puntuacion fuera de rango [0,10].");
    }
}
