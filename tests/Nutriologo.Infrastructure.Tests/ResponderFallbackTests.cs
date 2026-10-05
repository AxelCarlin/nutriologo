namespace Nutriologo.Infrastructure.Tests;

using Microsoft.Extensions.Configuration;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Infrastructure.AI;
using Xunit;

/// <summary>
/// Path de respaldo del respondedor: qué pasa cuando el modelo no está.
/// El HttpClient apunta a un puerto cerrado, así que el fallo de transporte es
/// REAL (no un mock): es exactamente lo que pasa si Ollama está caído.
/// </summary>
public class ResponderFallbackTests
{
    /// <summary>Puerto donde no escucha nada: cada llamada falla de verdad.</summary>
    private static HttpClient PuertoMuerto() => new()
    {
        BaseAddress = new Uri("http://127.0.0.1:1"),
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static OllamaConversationalResponder Sut() =>
        new(PuertoMuerto(), "qwen2.5:7b-instruct-q4_K_M",
            new ConfigurationBuilder().AddInMemoryCollection().Build());

    private static ContextoUsuario Contexto(
        int kcal = 4035, int? meta = 2200, int comidas = 3,
        decimal? pesoKg = null, int? metaObjetivo = null) => new(
        Perfil: null,
        Objetivo: metaObjetivo is { } m
            ? new ObjetivoResumen("Mantener", m, 0m, "Desconocido")
            : null,
        Hoy: new BalanceHoy(
            KcalConsumidas: kcal,
            MetaKcal: meta,
            Macros: new MacroResumen(120, 80, 400),
            Comidas: Enumerable.Range(1, comidas)
                .Select(i => new ComidaResumen($"Comida {i}", 1200, $"{i * 3:00}:00"))
                .ToArray()),
        Peso: pesoKg is { } p
            ? new PesoResumen(p, "2026-10-01", -0.6m)
            : null);

    [Fact]
    public async Task ModeloCaido_MarcaFallbackYRespondeConCifrasDelContexto()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿Cómo voy hoy?", null, Contexto()));

        Assert.True(r.UsaFallback, "Sin modelo la respuesta debe marcarse como fallback");
        Assert.Contains("4.035", r.Texto);   // punto de miles
        Assert.DoesNotContain("4,035", r.Texto);
        Assert.Contains("2.200", r.Texto);
    }

    [Fact]
    public async Task SinMeta_NoInventaMeta()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿cuántas calorías llevo?", null, Contexto(meta: null)));

        Assert.True(r.UsaFallback);
        Assert.Contains("4.035", r.Texto);
        Assert.Contains("meta", r.Texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreguntaDeComidas_NoVuelcaTodas()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿qué comí hoy?", null, Contexto(comidas: 17)));

        Assert.True(r.UsaFallback);
        Assert.Contains("17 comidas", r.Texto);
        // Solo las últimas 4, no las 17: un volcado no es una respuesta.
        Assert.Equal(4, r.Texto.Split("kcal)").Length - 1);
    }

    [Fact]
    public async Task PreguntaDePeso_UsaLaMedicionReal()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿cuánto peso tengo?", null, Contexto(pesoKg: 113.7m)));

        Assert.True(r.UsaFallback);
        Assert.Contains("113,7", r.Texto);          // decimal con coma
        Assert.Contains("-0,6", r.Texto);          // delta real, no inventado
    }

    [Fact]
    public async Task SinPeso_DiceDondeRegistrarlo()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿cuánto peso tengo?", null, Contexto()));

        Assert.True(r.UsaFallback);
        Assert.Contains("Mediciones", r.Texto);
        Assert.DoesNotContain("kg", r.Texto);
    }

    [Fact]
    public async Task Saludo_NoNecesitaDatos()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("hola", null, Contexto(kcal: 0, meta: null, comidas: 0)));

        Assert.True(r.UsaFallback);
        Assert.DoesNotContain("kcal", r.Texto);
    }

    [Fact]
    public async Task PreguntaNoRespondible_NoDevuelveLaFraseGenerica()
    {
        var r = await Sut().ResponderAsync(
            new ConversationalRequest("¿qué me recomiendas para cenar con pollo?", null, Contexto()));

        Assert.True(r.UsaFallback);
        // Antes toda pregunta no contemplada recibía exactamente esta frase.
        Assert.DoesNotContain("¿Qué te gustaría registrar o consultar?", r.Texto);
        Assert.Contains("balance de hoy", r.Texto);  // ofrece lo que sí puede responder
    }

    [Fact]
    public async Task ContextoNulo_NoRevienta()
    {
        var r = await Sut().ResponderAsync(new ConversationalRequest("¿cómo voy?", null, null));

        Assert.True(r.UsaFallback);
        Assert.NotEmpty(r.Texto);
    }

    [Fact]
    public async Task Stream_CaeAFallback_SinEmitirTokensInventados()
    {
        var eventos = new List<ChatStreamEvent>();

        await foreach (var ev in Sut().ResponderStreamAsync(
            new ConversationalRequest("¿cómo voy hoy?", null, Contexto())))
        {
            eventos.Add(ev);
        }

        // Con el modelo caído no puede haber tokens: solo el cierre con fallback.
        Assert.DoesNotContain(eventos, e => e.Type == "token");
        var done = Assert.Single(eventos, e => e.Type == "done");
        Assert.True(done.Fallback);
        Assert.Contains("4.035", done.FinalText!);
    }
}
