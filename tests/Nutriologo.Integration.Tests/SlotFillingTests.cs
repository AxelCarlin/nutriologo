namespace Nutriologo.Integration.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// Slot filling contra el backend real.
///
/// Estos tests fijan el comportamiento que un LLM de 7B no puede dar: resolver
/// "sí, la grande" sin volver a preguntárselo al modelo. Si alguno falla, la
/// coreferencia volvió a depender del modelo y el chat se rompe otra vez.
/// </summary>
public class SlotFillingTests
{
    static SlotFillingTests()
    {
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static HttpClient Client() =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();

    private static async Task<HttpClient> AuthedAsync()
    {
        var client = Client();
        var correo = $"slot_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Slot", correo, password = "Segura1234" });
        reg.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                doc.RootElement.GetProperty("token").GetString());
        return client;
    }

    private static async Task<JsonElement> Enviar(HttpClient client, string texto)
    {
        var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto });
        r.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string Tipo(JsonElement e) => e.GetProperty("tipo").GetString() ?? "";

    [Fact]
    public async Task SinAlimentoReconocido_NoEsErrorDeConexion()
    {
        // Regresión: el frontend comparaba contra "pregunta" y el backend
        // emite "aclaracion", así que todo saludo caía en el else.
        using var client = await AuthedAsync();
        var r = await Enviar(client, "hola");
        Assert.Equal("aclaracion", Tipo(r));
    }

    [Fact]
    public async Task ComidaCompleta_SeRegistraEnUnSoloTurno()
    {
        using var client = await AuthedAsync();
        var r = await Enviar(client, "Comí dos huevos y dos tortillas");
        Assert.Equal("registrado", Tipo(r));
    }

    [Fact]
    public async Task PatchDeTamano_ResuelveElFormSinLlamarAlModelo()
    {
        // "comí tortillas" no trae cantidad: abre el form y pregunta el slot.
        // "sí, la grande" lo resuelve la tabla determinista en C#.
        using var client = await AuthedAsync();

        var t1 = await Enviar(client, "comí tortillas");
        Assert.Equal("aclaracion", Tipo(t1));
        Assert.Contains("cuántas", t1.GetProperty("pregunta").GetString()!.ToLowerInvariant());

        // El mensaje que fallaba: la grande resuelve la cantidad.
        var t2 = await Enviar(client, "sí, la grande");
        Assert.Equal("registrado", Tipo(t2));
        Assert.True(t2.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task CantidadConUnidad_EnUnSoloMensaje()
    {
        using var client = await AuthedAsync();

        var t1 = await Enviar(client, "comí tortillas");
        Assert.Equal("aclaracion", Tipo(t1));

        // "50ml" trae cantidad y unidad: cierra el form y registra.
        var t2 = await Enviar(client, "50ml");
        Assert.Equal("registrado", Tipo(t2));
    }

    [Fact]
    public async Task Cancelacion_CierraElFormSinRegistrar()
    {
        using var client = await AuthedAsync();

        await Enviar(client, "comí tortillas");
        var t = await Enviar(client, "olvídalo");

        Assert.Equal("respuesta", Tipo(t));
        Assert.Contains("dejamos", t.GetProperty("texto").GetString()!.ToLowerInvariant());

        // Tras cancelar, una frase nueva debe abrir su propio form.
        var nuevo = await Enviar(client, "comí arroz");
        Assert.Equal("aclaracion", Tipo(nuevo));
    }

    [Fact]
    public async Task ConsultaBalance_NoAbreFormNiInventaRegistro()
    {
        using var client = await AuthedAsync();
        var r = await Enviar(client, "¿cuántas calorías llevo?");
        Assert.Equal("respuesta", Tipo(r));
    }
}