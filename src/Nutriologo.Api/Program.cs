using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nutriologo.Api;
using Nutriologo.Api.Auth;
using Nutriologo.Api.Middleware;
using Nutriologo.Infrastructure.AI;
using Nutriologo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOllama(builder.Configuration);
builder.Services.AddSingleton<JwtIssuer>();

// Auth: JWT HMAC-SHA256. Clave vía Auth__Key (ver .env / compose).
var authKey = builder.Configuration["Auth:Key"]
    ?? throw new InvalidOperationException("Falta Auth:Key (variable Auth__Key).");
var authIssuer = builder.Configuration["Auth:Issuer"] ?? "nutriologo";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authIssuer,
            ValidateAudience = true,
            ValidAudience = authIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuth();
app.MapNutricion();
app.MapChat();

app.Run();
public partial class Program { }
