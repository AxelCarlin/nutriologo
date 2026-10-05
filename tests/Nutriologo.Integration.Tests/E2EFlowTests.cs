namespace Nutriologo.Integration.Tests;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// E2E reales contra PostgreSQL 17+pgvector (y Ollama cuando el flujo lo usa).
/// Requieren: docker compose up -d, seed cargado, Ollama activo con el modelo de texto.
/// Todos los flujos de negocio exigen JWT; el idUsuario se deriva del token (anti-BOLA).
/// </summary>
public class E2EFlowTests
{
    static E2EFlowTests()
    {
        // Clave de firma solo para la suite (el Api real usa Auth__Key / Development).
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static HttpClient Client() =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();

    private static async Task<(HttpClient client, int id)> AuthedClientAsync()
    {
        var client = Client();
        var correo = $"e2e_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "E2E", correo, password = "Segura1234" });
        reg.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("token").GetString());
        return (client, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Registro_Y_Login_EmiteToken()
    {
        using var client = Client();
        var correo = $"login_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "T", correo, password = "Segura1234" });
        Assert.Equal(System.Net.HttpStatusCode.Created, reg.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { correo, password = "Segura1234" });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("token").GetString()));

        var bad = await client.PostAsJsonAsync("/api/auth/login",
            new { correo, password = "incorrecta" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task SinToken_401()
    {
        using var client = Client();
        var r = await client.GetAsync($"/api/balance?fecha={DateTime.UtcNow:yyyy-MM-dd}");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Objetivo_DevuelveEstimacionesYRiesgo()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/objetivo/calcular", new
            {
                pesoKg = 92m, alturaCm = 175m, edadAnios = 35, sexo = 0, factorActividad = 1.55m,
                tipoObjetivo = 0, pesoObjetivoKg = 82m, semanasObjetivo = 22m,
            });
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.GetProperty("gastoEnergeticoTotal").GetDecimal() > 0);
            Assert.True(doc.RootElement.GetProperty("metaCaloricaEstimada").GetDecimal() > 0);
            Assert.True(doc.RootElement.TryGetProperty("evaluacionObjetivo", out _));
        }
    }

    [Fact]
    public async Task Objetivo_MenorDeEdad_Rechaza()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/objetivo/calcular", new
            {
                pesoKg = 60m, alturaCm = 165m, edadAnios = 16, sexo = 1, factorActividad = 1.2m,
                tipoObjetivo = 0, pesoObjetivoKg = 55m, semanasObjetivo = 10m,
            });
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
        }
    }

    [Fact]
    public async Task RegistroFormulario_Y_Balance()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/comidas", new
            {
                fechaConsumo = DateTime.UtcNow, nombre = "E2E form",
                items = new[] { new { nombreAlimento = "arroz", cantidad = 200m, unidad = "G" } },
            });
            r.EnsureSuccessStatusCode();

            var fecha = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var bal = await client.GetFromJsonAsync<JsonElement>($"/api/balance?fecha={fecha}");
            Assert.True(bal.GetProperty("caloriasConsumidas").GetDecimal() >= 0);
        }
    }

    [Fact]
    public async Task Aislamiento_BOLA_UsuarioB_NoVeDatosDeA()
    {
        var (clientA, _) = await AuthedClientAsync();
        var (clientB, _) = await AuthedClientAsync();
        using (clientA)
        using (clientB)
        {
            var r = await clientA.PostAsJsonAsync("/api/comidas", new
            {
                fechaConsumo = DateTime.UtcNow, nombre = "Solo A",
                items = new[] { new { nombreAlimento = "arroz", cantidad = 200m, unidad = "G" } },
            });
            r.EnsureSuccessStatusCode();

            var fecha = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var balB = await clientB.GetFromJsonAsync<JsonElement>($"/api/balance?fecha={fecha}");
            Assert.Equal(0m, balB.GetProperty("caloriasConsumidas").GetDecimal());

            var comidasB = await clientB.GetFromJsonAsync<JsonElement>("/api/comidas");
            Assert.Equal(0, comidasB.GetArrayLength());
        }
    }

    [Fact]
    public async Task ComidaInvalida_AlimentoDesconocido_NoPersiste()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/comidas", new
            {
                fechaConsumo = DateTime.UtcNow, nombre = "X",
                items = new[] { new { nombreAlimento = "pizza inexistente", cantidad = 1m, unidad = "G" } },
            });
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
        }
    }

    [Fact]
    public async Task Chat_NoRegistra_SoloRedirigeConLosDatos()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto = "Comí dos huevos y dos tortillas" });
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var raiz = doc.RootElement;

            Assert.Equal("redirigir", raiz.GetProperty("tipo").GetString());

            // La redirección lleva los datos y la vista: nada queda guardado.
            var red = raiz.GetProperty("redireccion");
            Assert.Equal("registrar", red.GetProperty("vista").GetString());
            Assert.False(string.IsNullOrWhiteSpace(red.GetProperty("etiqueta").GetString()));
            Assert.True(red.GetProperty("items").GetArrayLength() >= 1);

            // El contrato viejo ("registrado" + id) no puede volver.
            Assert.False(raiz.TryGetProperty("id", out _));
        }
    }

    [Fact]
    public async Task Chat_NoEscribeComidas_AlTerminarElTurno()
    {
        // El punto del cambio: el chat arma el borrador y lo entrega, pero la
        // tabla de comidas no se toca. Antes escribía una fila por mensaje.
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var antes = await ContarComidasAsync(client);
            var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto = "Comí dos huevos y dos tortillas" });
            r.EnsureSuccessStatusCode();
            var despues = await ContarComidasAsync(client);

            Assert.Equal(antes, despues);
        }
    }

    /// <summary>Cantidad real de comidas del usuario: la lista, no el panel.</summary>
    private static async Task<int> ContarComidasAsync(HttpClient client)
    {
        using var r = await client.GetAsync("/api/comidas");
        r.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.GetArrayLength()
            : 0;
    }

    /// <summary>
    /// Pizza NO está en el catálogo, así que el bot no puede cerrar una comida:
    /// tiene que preguntar, no inventar.
    ///
    /// Este test afirmaba `tipo == "aclaracion"` a secas, que dependía de cómo
    /// clasificara el modelo ese turno: con el modelo ya cargado, "Comí pizza"
    /// llegaba como `crear_objetivo` y el bot mandaba al usuario a calcular la
    /// meta calórica. Falla o pasa según el estado de la caché de Ollama.
    ///
    /// Lo que importa no es qué tipo exacto devuelve, sino dos cosas que sí
    /// tienen que ser verdad siempre:
    ///   1. NO se redirige a `objetivo`: nadie pide la meta y una comida juntos.
    ///   2. NO se persiste nada: el contrato viejo `id` ya no existe.
    /// </summary>
    [Fact]
    public async Task Chat_Pizza_NoSeRedirigeAlObjetivoYNoPersiste()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto = "Comí pizza" });
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());

            var tipo = doc.RootElement.GetProperty("tipo").GetString();
            Assert.True(tipo is "aclaracion" or "redirigir" or "respuesta",
                $"tipo inesperado: {tipo}");

            if (tipo == "redirigir")
            {
                var vista = doc.RootElement.GetProperty("redireccion").GetProperty("vista").GetString();
                // Un mensaje sobre comida no puede aterrizar en la meta calórica.
                Assert.NotEqual("objetivo", vista);
                // Y si redirige, tiene que ser al formulario, con el item que
                // el modelo haya detectado.
                Assert.Equal("registrar", vista);
            }

            // El chat nunca escribe en la base.
            Assert.False(doc.RootElement.TryGetProperty("id", out _));
            Assert.Equal(0, await ContarComidasAsync(client));
        }
    }
}
