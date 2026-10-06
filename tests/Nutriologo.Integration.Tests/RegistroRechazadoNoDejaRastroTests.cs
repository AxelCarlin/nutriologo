namespace Nutriologo.Integration.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// Un registro RECHAZADO no debe dejar rastro. MealRegistrar insertaba la
/// comida y guardaba antes de resolver las unidades: si una unidad no
/// existia devolvia 400 y dejaba la fila persistida. Esa fila contamiba
/// todos los conteos del dia — /panel, /comidas, Balance y el contador de
/// comidas del sidebar — con una comida de 0 kcal que nadie registro.
/// </summary>
public class RegistroRechazadoNoDejaRastroTests
{
    static RegistroRechazadoNoDejaRastroTests()
    {
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static async Task<HttpClient> AuthedClientAsync()
    {
        var client = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();
        var correo = $"rechazo_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Rechazo", correo, password = "Segura1234" });
        reg.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                doc.RootElement.GetProperty("token").GetString());
        return client;
    }

    private static async Task<(int comidas, int kcal)> PanelHoyAsync(HttpClient client)
    {
        var panel = JsonDocument.Parse(await client.GetStringAsync("/api/panel"));
        var comidas = panel.RootElement.GetProperty("comidas").GetArrayLength();
        var kcal = (int)panel.RootElement.GetProperty("caloriasConsumidas").GetDecimal();
        return (comidas, kcal);
    }

    [Fact]
    public async Task Unidad_Invalida_Rechaza_Sin_Crear_Comida()
    {
        using var client = await AuthedClientAsync();
        var (antes, kcalAntes) = await PanelHoyAsync(client);

        var r = await client.PostAsJsonAsync("/api/comidas", new
        {
            fechaConsumo = DateTime.UtcNow,
            nombre = "No debe existir",
            items = new[]
            {
                new { nombreAlimento = "huevo", cantidad = 2m, unidad = "PIEZA" },
                new { nombreAlimento = "arroz", cantidad = 1m, unidad = "UNIDAD_QUE_NO_EXISTE" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var (despues, kcalDespues) = await PanelHoyAsync(client);
        Assert.Equal(antes, despues);
        Assert.Equal(kcalAntes, kcalDespues);
    }

    [Fact]
    public async Task Unidad_Invalida_No_Aparece_En_Comidas()
    {
        using var client = await AuthedClientAsync();

        await client.PostAsJsonAsync("/api/comidas", new
        {
            fechaConsumo = DateTime.UtcNow,
            nombre = "Huerfana",
            items = new[] { new { nombreAlimento = "huevo", cantidad = 1m, unidad = "UNIDAD_FALSA" } },
        });

        var listado = JsonDocument.Parse(await client.GetStringAsync("/api/comidas"));
        Assert.DoesNotContain(listado.RootElement.EnumerateArray(),
            c => c.GetProperty("nombre").GetString() == "Huerfana");
    }

    [Fact]
    public async Task Comida_Valida_Si_Cuenta_Y_Las_Invalidas_No_Infian_El_Contador()
    {
        using var client = await AuthedClientAsync();

        // Dos registros rechazados intercalados con uno válido.
        for (var i = 0; i < 2; i++)
            await client.PostAsJsonAsync("/api/comidas", new
            {
                fechaConsumo = DateTime.UtcNow,
                nombre = $"Falsa{i}",
                items = new[] { new { nombreAlimento = "huevo", cantidad = 1m, unidad = "UNIDAD_FALSA" } },
            });

        var ok = await client.PostAsJsonAsync("/api/comidas", new
        {
            fechaConsumo = DateTime.UtcNow,
            nombre = "Real",
            items = new[] { new { nombreAlimento = "huevo", cantidad = 2m, unidad = "PIEZA" } },
        });
        ok.EnsureSuccessStatusCode();

        // El contador del sidebar sale de panel.comidas: con dos huérfanas
        // habría mostrado 3.
        var (comidas, kcal) = await PanelHoyAsync(client);
        Assert.Equal(1, comidas);
        Assert.True(kcal > 0, $"La comida válida debe aportar kcal, no {kcal}");
    }
}