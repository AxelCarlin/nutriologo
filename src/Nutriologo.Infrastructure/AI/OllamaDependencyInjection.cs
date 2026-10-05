namespace Nutriologo.Infrastructure.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutriologo.Application.Abstractions.AI;
using Nutriologo.Infrastructure.Conversations;

public static class OllamaDependencyInjection
{
    public static IServiceCollection AddOllama(this IServiceCollection services, IConfiguration config)
    {
        var baseUrl = config["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
        var model = config["Ollama:Model"] ?? "qwen2.5:7b-instruct-q4_K_M";

        // Cliente compartido para intérprete (timeout largo: slot filling puede tardar)
        services.AddHttpClient("Ollama.Interpreter", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // Cliente dedicado para respondedor conversacional (timeout corto: 30s)
        services.AddHttpClient("Ollama.Conversational", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        var visionModel = config["Ollama:VisionModel"] ?? "qwen2.5vl:7b-q4_K_M";
        services.AddScoped<IVisionAnalyzer>(sp =>
            new OllamaVisionAnalyzer(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama.Interpreter"), visionModel));
        services.AddScoped<IChatInterpreter>(sp =>
            new OllamaChatInterpreter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama.Interpreter"), model,
                sp.GetService<ILogger<OllamaChatInterpreter>>()));
        services.AddScoped<IConversationalResponder>(sp =>
            new OllamaConversationalResponder(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama.Conversational"), model, config,
                sp.GetService<ILogger<OllamaConversationalResponder>>()));

        // Slot filling determinista: resuelve las respuestas cortas y persiste
        // el estado del form para que sobreviva a un reinicio.
        services.AddScoped<ChatTurnService>();
        return services;
    }
}
