namespace Nutriologo.Infrastructure.Tests;

using Microsoft.Extensions.Configuration;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Application.AI;
using Nutriologo.Infrastructure.AI;
using Xunit;

/// <summary>
/// Registro de español: tuteo, siempre.
///
/// En la misma pantalla convivían tres registros: la interfaz decía "escribe tu
/// mensaje" (tuteo), el estado vacío decía "contame" (voseo) y el asistente
/// contestaba "¿Te apetece...?" con el prompt instructing en voseo y el fallback
/// respondiendo "¿qué querés consultar?". Nadie puede saber a qué registro está
/// leyendo.
///
/// Estos tests leen el TEXTO QUE SALE, no las intenciones: el prompt (que es lo
/// que enseñaba voseo al modelo) y cada respuesta del fallback.
/// </summary>
public class RegistroEspanolTests
{
    /// <summary>
    /// Formas voseas inequívocas. Solo las acentuadas o sin equivalente en
    /// tuteo: "revisá" es voseo pero "revisa" es imperativo de tuteo perfectamente
    /// válido, y ahí no hay nada que corregir.
    /// </summary>
    private static readonly string[] Voseo =
    [
        "contame", "preguntame", "decime", "decís", "podés", "querés", "tenés",
        "revisá", "guardá", "indicá", "llevás", "hablás", "escribís", "pensás",
        "priorizá", "registralas", "calculala",
    ];

    private static void SinVoseo(string texto, string quien)
    {
        var minuscula = texto.ToLowerInvariant();
        var castigo = Voseo.FirstOrDefault(v => minuscula.Contains(v, StringComparison.Ordinal));
        Assert.True(castigo is null, $"{quien} usa voseo: \"{castigo}\" en → {texto}");
    }

    /// <summary>
    /// El prompt tiene que poder NOMBRAR las formas que prohíbe para listing
    /// dónde está la lista. Se descarta esa única línea y se mira el resto: lo
    /// que se busca es que nadie vuelva a enseñar voseo como instrucción.
    /// </summary>
    [Fact]
    public void ElPrompt_NoEnseñaVoseo()
    {
        var lineas = OllamaConversationalResponder.ConversationalSystemPrompt
            .Split('\n')
            .Where(l => !l.Contains("Formas prohibidas", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(lineas);
        SinVoseo(string.Join('\n', lineas), "el prompt");
    }

    [Fact]
    public void ElPrompt_DeclaraLaListaDeFormasProhibidas()
    {
        var p = OllamaConversationalResponder.ConversationalSystemPrompt;

        Assert.Contains("HABLA EN TUTEO", p, StringComparison.Ordinal);
        Assert.Contains("Formas prohibidas", p, StringComparison.Ordinal);
    }

    [Fact]
    public void ElPrompt_ProhibeElRellenoDeCierre()
    {
        var p = OllamaConversationalResponder.ConversationalSystemPrompt;

        // El relleno era real en las capturas: "para mantenerte en el camino
        // correcto", "¿te apetece compartir...?".
        Assert.Contains("sin relleno", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("para mantenerte en el camino correcto", p, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cortesía", p, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ElPrompt_MandaNombrarLaRutaParaQueHayaBoton()
    {
        // Sin la ruta el cliente no tiene de dónde sacar el botón.
        var p = OllamaConversationalResponder.ConversationalSystemPrompt;
        foreach (var ruta in new[] { "/registrar", "/mediciones", "/objetivo", "/perfil" })
            Assert.Contains(ruta, p, StringComparison.Ordinal);
    }

    /// <summary>Puerto donde no escucha nada: el modelo está caído, como en producción sin Ollama.</summary>
    private static OllamaConversationalResponder Sut() =>
        new(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1"), Timeout = TimeSpan.FromSeconds(2) },
            "qwen2.5:7b-instruct-q4_K_M",
            new ConfigurationBuilder().AddInMemoryCollection().Build());

    private static ContextoUsuario Contexto(decimal? peso = 113.7m) => new(
        Perfil: null,
        Objetivo: null,
        Hoy: new BalanceHoy(
            KcalConsumidas: 4035,
            MetaKcal: 2200,
            Macros: new MacroResumen(120, 80, 400),
            Comidas: Enumerable.Range(1, 3)
                .Select(i => new ComidaResumen($"Comida {i}", 1200, $"{i * 3:00}:00"))
                .ToArray()),
        Peso: peso is null ? null : new PesoResumen(peso.Value, "2026-10-01", -0.6m));

    [Theory]
    [InlineData("¿Cómo voy hoy?")]
    [InlineData("¿Cuánto pesas?")]
    [InlineData("hola")]
    [InlineData("gracias")]
    [InlineData("¿Qué comí?")]
    [InlineData("¿Qué me recomiendas para cenar con pollo?")]
    [InlineData("quiero adelgazar")]
    public async Task ElFallback_NoCaeEnVoseo(string pregunta)
    {
        var r = await Sut().ResponderAsync(new ConversationalRequest(pregunta, null, Contexto()));

        Assert.True(r.UsaFallback);
        SinVoseo(r.Texto, $"el fallback de «{pregunta}»");
    }

    [Fact]
    public async Task ElFallback_NombraLaRutaParaQueHayaBoton()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿cuánto pesas?", null, Contexto(peso: null)));

        // Sin peso no hay dato que inventar: la respuesta tiene que decir DÓNDE
        // se carga, con la ruta, que es lo que el cliente convierte en botón.
        Assert.Contains("/mediciones", r.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElFallback_NoOfreceLaFraseGenerica()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿qué me recomiendas para cenar con pollo?", null, Contexto()));

        Assert.DoesNotContain("¿Qué te gustaría registrar o consultar?", r.Texto);
        Assert.Contains("balance de hoy", r.Texto);
    }
}
