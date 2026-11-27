namespace Nutriologo.Integration.Tests;

using Nutriologo.Api;
using Xunit;

/// <summary>
/// El texto del asistente dice QUÉ tiene que hacer el usuario; el botón es lo
/// que le hace poder hacerlo.
///
/// Antes el respondedor contestaba "ve a tu pantalla de registro" y la ruta
/// conversacional devolvía <c>redireccion: null</c>: el texto prometía una
/// pantalla y no había ni un enlace. El estado vacío decía "te llevo al
/// formulario" y la promesa moría en la frase.
///
/// Estos tests fijan la traducción de la ruta que escribe el modelo. Son
/// deterministas: no dependen de que el modelo responda una cosa u otra, solo
/// de cómo se lee lo que respondió.
/// </summary>
public class RedireccionDesdeTextoTests
{
    [Theory]
    [InlineData("Ve a /registrar para cargar la comida.", "registrar")]
    [InlineData("Regístralas en /mediciones.", "mediciones")]
    [InlineData("Tu objetivo no está definido. Ve a /objetivo para calcularlo.", "objetivo")]
    [InlineData("Actualiza tu perfil en /perfil.", "perfil")]
    [InlineData("carga eso en tu PANTALLA DE REGISTRO", "registrar")]
    public void RutaNombrada_ArmaElBoton(string texto, string vistaEsperada)
    {
        var red = ChatEndpoints.RedireccionDesdeTexto(texto);

        Assert.NotNull(red);
        Assert.Equal(vistaEsperada, red!.Vista);
        Assert.False(string.IsNullOrWhiteSpace(red.Etiqueta));
        // Sin datos que llevar: esta redirección abre la vista, no precarga nada.
        Assert.Empty(red.Items);
    }

    [Theory]
    [InlineData("Tu objetivo es perder peso a 0,5 kg por semana.")]
    [InlineData("Llevas 1.200 kcal hoy de 2.200 de meta.")]
    [InlineData("No tengo tu peso actual.")]
    public void PalabraSuelta_NoArmaBoton(string texto)
    {
        // "objetivo", "meta" y "perfil" son palabras del tema, no rutas. Si
        // contaran, el botón aparecería en respuestas que no invitan a ir a
        // ninguna parte.
        Assert.Null(ChatEndpoints.RedireccionDesdeTexto(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinTexto_NoArmaBoton(string? texto)
    {
        Assert.Null(ChatEndpoints.RedireccionDesdeTexto(texto));
    }

    [Fact]
    public void GanaLaRutaQueElModeloNombroPrimero()
    {
        // Caso real de la respuesta "¿qué me recomiendas para cenar?": el modelo
        // escribió "/objetivo" y después "/registrar". El botón va a lo primero
        // que pidió, no a una vista de una lista fija.
        var red = ChatEndpoints.RedireccionDesdeTexto(
            "No tengo tu meta calórica actual. Ve a /objetivo para definirla y luego ve a /registrar para agregar tus comidas del día.");

        Assert.NotNull(red);
        Assert.Equal("objetivo", red!.Vista);
    }

    [Fact]
    public void NombraDosVistas_UnaSolaAccionLaPrimera()
    {
        // El cliente muestra un botón por respuesta: si el modelo nombra dos
        // pantallas, gana la que pidió primero.
        var red = ChatEndpoints.RedireccionDesdeTexto("Ve a /registrar y después revisa /mediciones.");

        Assert.NotNull(red);
        Assert.Equal("registrar", red!.Vista);
    }
}
