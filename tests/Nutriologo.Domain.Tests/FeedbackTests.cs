namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Feedback;
using Xunit;

public class FeedbackTests
{
    private static T Make<T>(params (string Name, object? Value)[] props) where T : new()
    {
        var obj = new T();
        foreach (var (n, v) in props) typeof(T).GetProperty(n)!.SetValue(obj, v);
        return obj;
    }

    [Fact]
    public void EvaluacionAnalisis_PuntuacionInvalida_Lanza() =>
        Assert.Throws<DomainException>(() => Make<EvaluacionAnalisisIA>(("Puntuacion", 11m)).DatosValidos());

    [Fact]
    public void Correccion_SinCambio_Lanza() =>
        Assert.Throws<DomainException>(() => Make<CorreccionPrediccion>(
            ("IdAlimentoAnterior", 1), ("IdAlimentoCorregido", 1), ("CantidadAnterior", 100m), ("CantidadCorregida", 100m)).DatosValidos());

    [Fact]
    public void Correccion_CambiaAlimento_Valida() =>
        Make<CorreccionPrediccion>(("IdAlimentoAnterior", 1), ("IdAlimentoCorregido", 3)).DatosValidos();

    [Fact]
    public void Correccion_CambiaCantidad_Valida() =>
        Make<CorreccionPrediccion>(("CantidadAnterior", 100m), ("CantidadCorregida", 150m)).DatosValidos();

    [Fact]
    public void EvaluacionMensaje_PuntuacionValida_Pasa() =>
        Make<EvaluacionMensaje>(("Puntuacion", 8.5m)).DatosValidos();
}
