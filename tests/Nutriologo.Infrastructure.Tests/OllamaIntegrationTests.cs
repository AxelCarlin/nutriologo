namespace Nutriologo.Infrastructure.Tests;

using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Infrastructure.AI;
using Xunit;

public class OllamaIntegrationTests
{
    [Fact]
    public async Task Ollama_RespondeEstructuraValida_CuandoDisponible()
    {
        HttpClient? http = null;
        try
        {
            http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11434"), Timeout = TimeSpan.FromSeconds(60) };
            using var ping = await http.GetAsync("/api/version");
            if (!ping.IsSuccessStatusCode) return; // Ollama no disponible en este entorno
        }
        catch { return; }

        var sut = new OllamaChatInterpreter(http, "qwen2.5:7b-instruct-q4_K_M");
        var r = await sut.InterpretAsync(new ChatRequest("Comí dos huevos y dos tortillas."));

        Assert.Equal("qwen2.5:7b-instruct-q4_K_M", r.Modelo);
        Assert.NotNull(r.Value);
        Assert.Equal(AiIntent.RegistrarComida, r.Value.Intent);
        Assert.NotEmpty(r.Value.Candidatos);
        Assert.NotNull(r.RawJson);
    }
}
