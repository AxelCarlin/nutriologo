namespace Nutriologo.Integration.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

/// <summary>
/// Soft delete de comidas: una comida errónea (p.ej. 100 PIEZA de huevo =
/// 7750 kcal) se da de baja sin borrar historia, y deja de contar en todas
/// las lecturas. Si algún test falla, hay una lectura que sigue contando
/// registros dados de baja.
/// </summary>
public class ComidaDeleteTests
{
    static ComidaDeleteTests()
    {
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static HttpClient Client() =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();

    private static async Task<(HttpClient client, int id)> AuthedClientAsync()
    {
        var client = Client();
        var correo = $"del_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Del", correo, password = "Segura1234" });
        reg.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                doc.RootElement.GetProperty("token").GetString());
        return (client, doc.RootElement.GetProperty("id").GetInt32());
    }

    private static async Task<int> RegistrarAsync(HttpClient client)
    {
        var r = await client.PostAsJsonAsync("/api/comidas", new
        {
            fechaConsumo = DateTime.UtcNow,
            nombre = "Para borrar",
            items = new[] { new { nombreAlimento = "huevo", cantidad = 100m, unidad = "G" } },
        });
        r.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Eliminar_Propia_204_Y_ExcluyeDeTodasLasLecturas()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var id = await RegistrarAsync(client);
            var fecha = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var antes = await client.GetFromJsonAsync<JsonElement>($"/api/balance?fecha={fecha}");
            Assert.True(antes.GetProperty("caloriasConsumidas").GetDecimal() > 0);

            var del = await client.DeleteAsync($"/api/comidas/{id}");
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

            // Ya no cuenta en ninguna lectura.
            var bal = await client.GetFromJsonAsync<JsonElement>($"/api/balance?fecha={fecha}");
            Assert.Equal(0m, bal.GetProperty("caloriasConsumidas").GetDecimal());

            var comidas = await client.GetFromJsonAsync<JsonElement>("/api/comidas");
            Assert.Equal(0, comidas.GetArrayLength());

            var panel = await client.GetFromJsonAsync<JsonElement>("/api/panel");
            Assert.Equal(0, panel.GetProperty("comidas").GetArrayLength());
            Assert.Equal(0, panel.GetProperty("caloriasConsumidas").GetDecimal());
        }
    }

    [Fact]
    public async Task Eliminar_DosVeces_404()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            var id = await RegistrarAsync(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/comidas/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/comidas/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Eliminar_DeOtroUsuario_404_Y_NoLaToca()
    {
        var (clientA, _) = await AuthedClientAsync();
        var (clientB, _) = await AuthedClientAsync();
        using (clientA)
        using (clientB)
        {
            var id = await RegistrarAsync(clientA);

            // B no puede borrar lo de A (y no distingue de "no existe").
            Assert.Equal(HttpStatusCode.NotFound, (await clientB.DeleteAsync($"/api/comidas/{id}")).StatusCode);

            // La comida de A sigue intacta.
            var comidasA = await clientA.GetFromJsonAsync<JsonElement>("/api/comidas");
            Assert.Equal(1, comidasA.GetArrayLength());
        }
    }

    [Fact]
    public async Task Eliminar_Inexistente_404()
    {
        var (client, _) = await AuthedClientAsync();
        using (client)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/comidas/987654321")).StatusCode);
        }
    }

    [Fact]
    public async Task Eliminar_SinToken_401()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/comidas/1")).StatusCode);
    }
}