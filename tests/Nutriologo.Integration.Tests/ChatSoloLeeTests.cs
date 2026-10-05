namespace Nutriologo.Integration.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// El chat deja de ESCRIBIR: solo conversa, analiza y redirige.
///
/// Estos tests fijan el límite. Si alguno falla, el bot volvió a tocar la base:
/// una comida registrada desde el chat es un dato que el usuario nunca confirmó
/// guardar, y el sistema no puede deshacerlo desde la interfaz.
/// </summary>
public class ChatSoloLeeTests
{
    static ChatSoloLeeTests()
    {
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static HttpClient Client() =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();

    private static async Task<HttpClient> AuthedAsync(string prefijo = "sololectura")
    {
        var client = Client();
        var correo = $"{prefijo}_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "SoloLectura", correo, password = "Segura1234" });
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

    private static async Task<int> ContarComidasAsync(HttpClient client)
    {
        using var r = await client.GetAsync("/api/comidas");
        r.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.GetArrayLength()
            : 0;
    }

    // ═══════════════════ CERO ESCRITURAS DE DOMINIO ═══════════════════

    [Fact]
    public async Task ComidaCompleta_NoEscribeEnComidas()
    {
        using var client = await AuthedAsync("noescribe");
        var antes = await ContarComidasAsync(client);

        var r = await Enviar(client, "Comí dos huevos y dos tortillas");

        Assert.Equal("redirigir", Tipo(r));
        Assert.Equal(antes, await ContarComidasAsync(client));
    }

    [Fact]
    public async Task VariosTurnosDeRegistro_NoEscribenNiUnaComida()
    {
        using var client = await AuthedAsync("noescribe2");
        var antes = await ContarComidasAsync(client);

        // El recorrido completo: item con cantidad, item sin cantidad, cierre.
        await Enviar(client, "comí tortillas");
        await Enviar(client, "sí, la grande");
        await Enviar(client, "y dos huevos");

        Assert.Equal(antes, await ContarComidasAsync(client));
    }

    [Fact]
    public async Task Medicion_RedirigeYNoMide()
    {
        using var client = await AuthedAsync("medicion");
        var r = await Enviar(client, "registrá mi peso de 80 kilos");

        // Cualquiera de los dos desenlaces es válido según lo que entienda el
        // modelo, pero NUNCA puede afirmar que lo midió.
        Assert.Contains(Tipo(r), new[] { "redirigir", "aclaracion", "respuesta" });

        if (Tipo(r) == "redirigir")
        {
            var vista = r.GetProperty("redireccion").GetProperty("vista").GetString();
            Assert.Equal("mediciones", vista);
        }

        var texto = r.TryGetProperty("texto", out var t) ? t.GetString() : null;
        Assert.DoesNotContain("registré tu peso", texto ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("peso registrado", texto ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Objetivo_RedirigeYNoCalculaPorSuCuenta()
    {
        using var client = await AuthedAsync("objetivo");
        var r = await Enviar(client, "calculame la meta calórica");

        if (Tipo(r) == "redirigir")
        {
            Assert.Equal("objetivo", r.GetProperty("redireccion").GetProperty("vista").GetString());
        }
        else
        {
            // Si no redirigió, al menos no puede afirmar que la dejó creada.
            var texto = r.GetProperty("texto").GetString() ?? "";
            Assert.DoesNotContain("meta creada", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("objetivo calculado", texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ElContratoRegistrado_NoVuelve()
    {
        using var client = await AuthedAsync("contrato");
        var r = await Enviar(client, "Comí dos huevos y dos tortillas");

        // "registrado" + id era el contrato de cuando el bot escribía.
        Assert.NotEqual("registrado", Tipo(r));
        Assert.False(r.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task PreguntaDeAnalisis_NoRedirige()
    {
        using var client = await AuthedAsync("analisis");
        var r = await Enviar(client, "¿qué puedo cenar hoy con lo que me sobra?");

        // Una pregunta de análisis es una respuesta, no un formulario.
        Assert.Equal("respuesta", Tipo(r));
    }

    [Fact]
    public async Task Redireccion_LlevaItemsConNombreCantidadYUnidad()
    {
        using var client = await AuthedAsync("items");
        var r = await Enviar(client, "Comí 150 gramos de arroz");

        if (Tipo(r) == "redirigir")
        {
            var items = r.GetProperty("redireccion").GetProperty("items");
            Assert.True(items.GetArrayLength() >= 1);

            var primero = items[0];
            Assert.False(string.IsNullOrWhiteSpace(primero.GetProperty("nombre").GetString()));
            Assert.True(primero.GetProperty("cantidad").GetDecimal() > 0);
            Assert.False(string.IsNullOrWhiteSpace(primero.GetProperty("unidad").GetString()));
        }
    }

    [Fact]
    public async Task UnaSesionDeOtroUsuario_NoSeMezcla()
    {
        // Dos usuarios distintos: lo que el primero taperleja, el segundo no lo ve.
        using var a = await AuthedAsync("userA");
        using var b = await AuthedAsync("userB");

        await Enviar(a, "comí dos huevos y dos tortillas");

        // El balance de B no puede contener lo que comió A.
        using var panelB = await b.GetAsync("/api/balance?fecha=" + DateTime.UtcNow.ToString("yyyy-MM-dd"));
        panelB.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await panelB.Content.ReadAsStringAsync());
        // El balance serializa decimales (0.0), no enteros: GetInt32 revienta.
        var kcal = doc.RootElement.TryGetProperty("caloriasConsumidas", out var k)
            ? k.GetDecimal() : 0m;
        Assert.Equal(0m, kcal);
    }
}