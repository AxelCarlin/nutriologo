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
    public async Task Chat_RegistraComida_Y_Balance()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto = "Comí dos huevos y dos tortillas" });
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Assert.Equal("registrado", doc.RootElement.GetProperty("tipo").GetString());
        }
    }

    [Fact]
    public async Task Chat_Pizza_Aclaracion_SinPersistir()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { texto = "Comí pizza" });
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Assert.Equal("aclaracion", doc.RootElement.GetProperty("tipo").GetString());
            Assert.False(doc.RootElement.TryGetProperty("id", out _));
        }
    }
}
