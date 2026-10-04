using Nutriologo.Api;
using Nutriologo.Api.Middleware;
using Nutriologo.Infrastructure.AI;
using Nutriologo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOllama(builder.Configuration);

// J5: rate limiting básico en endpoints de negocio/IA
builder.Services.AddRateLimiter(options =>
{
        options.AddPolicy("business", _ =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("business", _ =>
                new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapNutricion();
app.MapChat();

app.Run();
public partial class Program { }
