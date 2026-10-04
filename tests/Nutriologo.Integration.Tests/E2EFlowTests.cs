namespace Nutriologo.Integration.Tests;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// E2E reales contra PostgreSQL 17+pgvector (y Ollama cuando el flujo lo usa).
/// Requieren: docker compose up -d, seed cargado, Ollama activo con el modelo de texto.
/// </summary>
public class E2EFlowTests
{
    private static HttpClient Client()
    {
        // Levanta el Api real con WebApplicationFactory contra la DB local.
        var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        return factory.CreateClient();
    }

    [Fact]
    public async Task Objetivo_DevuelveEstimacionesYRiesgo()
    {
        using var client = Client();
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

    [Fact]
    public async Task Objetivo_MenorDeEdad_Rechaza()
    {
        using var client = Client();
        var r = await client.PostAsJsonAsync("/api/objetivo/calcular", new
        {
            pesoKg = 60m, alturaCm = 165m, edadAnios = 16, sexo = 1, factorActividad = 1.2m,
            tipoObjetivo = 0, pesoObjetivoKg = 55m, semanasObjetivo = 10m,
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task RegistroFormulario_Y_Balance()
    {
        using var client = Client();
        var r = await client.PostAsJsonAsync("/api/comidas", new
        {
            idUsuario = 1, fechaConsumo = DateTime.UtcNow, nombre = "E2E form",
            items = new[] { new { nombreAlimento = "arroz", cantidad = 200m, unidad = "G" } },
        });
        r.EnsureSuccessStatusCode();

        var fecha = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var bal = await client.GetFromJsonAsync<JsonElement>($"/api/balance?idUsuario=1&fecha={fecha}");
        Assert.True(bal.GetProperty("caloriasConsumidas").GetDecimal() >= 0);

        // Aislamiento por usuario: otro usuario no ve el balance de usuario 1
        var bal2 = await client.GetFromJsonAsync<JsonElement>($"/api/balance?idUsuario=999&fecha={fecha}");
        Assert.Equal(0m, bal2.GetProperty("caloriasConsumidas").GetDecimal());
    }

    [Fact]
    public async Task ComidaInvalida_AlimentoDesconocido_NoPersiste()
    {
        using var client = Client();
        var r = await client.PostAsJsonAsync("/api/comidas", new
        {
            idUsuario = 1, fechaConsumo = DateTime.UtcNow, nombre = "X",
            items = new[] { new { nombreAlimento = "pizza inexistente", cantidad = 1m, unidad = "G" } },
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Chat_RegistraComida_Y_Balance()
    {
        using var client = Client();
        var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { idUsuario = 1, texto = "Comí dos huevos y dos tortillas" });
        r.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal("registrado", doc.RootElement.GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task Chat_Pizza_Aclaracion_SinPersistir()
    {
        using var client = Client();
        var r = await client.PostAsJsonAsync("/api/conversacion/mensaje", new { idUsuario = 1, texto = "Comí pizza" });
        r.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal("aclaracion", doc.RootElement.GetProperty("tipo").GetString());
        Assert.False(doc.RootElement.TryGetProperty("id", out _));
    }
}
