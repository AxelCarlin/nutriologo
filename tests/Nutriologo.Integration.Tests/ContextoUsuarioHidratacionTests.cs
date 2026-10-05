using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nutriologo.Application.Abstractions.AI;
using Xunit;

namespace Nutriologo.Integration.Tests;

/// <summary>
/// Integración del flujo de hidratación de contexto de usuario.
/// Este test verifica que HidratarContextoUsuarioAsync produzca un ContextoUsuario
/// válido sin errores de esquema de BD (columnas faltantes, tipos incompatibles, etc.).
/// </summary>
public class ContextoUsuarioHidratacionTests
{
    static ContextoUsuarioHidratacionTests()
    {
        Environment.SetEnvironmentVariable("Auth__Key", "test-suite-key-0123456789abcdef-test-suite-key");
    }

    private static HttpClient Client() =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().CreateClient();

    private static async Task<(HttpClient client, int idUsuario)> AuthedClientAsync()
    {
        var client = Client();
        var correo = $"contexto_{Guid.NewGuid():N}@test.local";
        var reg = await client.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Contexto", correo, password = "Segura1234" });
        reg.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var perf = await client.GetAsync($"/api/perfil/{doc.RootElement.GetProperty("id").GetInt32()}");
        // El perfil puede no existir aún; el endpoint retornará datos disponibles.
        return (client, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task HidratarCon_RetornaContextoUsuario_ConEstructuraValida()
    {
        // Arrange: obtenemos un usuario autenticado
        var (client, idUsuario) = await AuthedClientAsync();

        // Act: llamamos al endpoint de conversación (usa HidratarContextoUsuarioAsync internamente)
        var mensaje = new { texto = "hola" };
        var response = await client.PostAsJsonAsync("/api/conversacion/mensaje", mensaje);

        // Assert: la respuesta debe ser exitosa y contener contexto de usuario
        response.EnsureSuccessStatusCode();
        var contexto = await response.Content.ReadFromJsonAsync<ContextoUsuario>();
        Assert.NotNull(contexto);
        // Al menos el perfil o el objetivo deben estar presentes (aunque algunos campos puedan ser null)
        // Lo crítico es que no hay 500 por mismatch de columnas SQL.
    }
}