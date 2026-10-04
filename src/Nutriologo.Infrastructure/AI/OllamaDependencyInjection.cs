namespace Nutriologo.Infrastructure.AI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutriologo.Application.Abstractions.AI;

public static class OllamaDependencyInjection
{
    public static IServiceCollection AddOllama(this IServiceCollection services, IConfiguration config)
    {
        var baseUrl = config["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
        var model = config["Ollama:Model"] ?? "qwen2.5:7b-instruct-q4_K_M";

        services.AddHttpClient(typeof(OllamaChatInterpreter).FullName!, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IChatInterpreter>(sp =>
            new OllamaChatInterpreter(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OllamaChatInterpreter).FullName!), model));
        return services;
    }
}
