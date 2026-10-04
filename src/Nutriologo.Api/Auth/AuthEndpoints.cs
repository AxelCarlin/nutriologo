namespace Nutriologo.Api.Auth;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Entities;
using Nutriologo.Infrastructure.Persistence;

public static class AuthEndpoints
{
    public record RegistroRequest(string Nombre, string Correo, string Password);
    public record LoginRequest(string Correo, string Password);

    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth").AllowAnonymous().RequireRateLimiting("business");

        g.MapPost("/registro", async (RegistroRequest r, AppDbContext db, JwtIssuer jwt) =>
        {
            if (string.IsNullOrWhiteSpace(r.Nombre) || string.IsNullOrWhiteSpace(r.Correo) || !r.Correo.Contains('@'))
                return Results.BadRequest(new { mensaje = "Nombre y correo válido requeridos." });
            if (string.IsNullOrEmpty(r.Password) || r.Password.Length < 8)
                return Results.BadRequest(new { mensaje = "La contraseña debe tener al menos 8 caracteres." });
            if (await db.Usuarios.AnyAsync(u => u.Correo == r.Correo))
                return Results.Conflict(new { mensaje = "Ese correo ya está registrado." });

            // IdUsuarioCrea es FK a Usuario (DEFERRABLE): el primer usuario se autocrea
            // en transacción diferida; los demás quedan creados por el primer usuario (sistema).
            var creador = await db.Usuarios.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            if (creador == 0) await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED");
            var u = new Usuario(r.Nombre.Trim(), r.Correo.Trim(), PasswordHasher.Hash(r.Password), creador);
            db.Usuarios.Add(u);
            await db.SaveChangesAsync();
            if (creador == 0)
                await db.Database.ExecuteSqlRawAsync($"UPDATE \"Usuario\" SET \"IdUsuarioCrea\" = \"Id\" WHERE \"Id\" = {u.Id}");
            await tx.CommitAsync();
            return Results.Created($"/api/usuarios/{u.Id}", new { id = u.Id, token = jwt.Issue(u.Id, u.Correo) });
        });

        g.MapPost("/login", async (LoginRequest r, AppDbContext db, JwtIssuer jwt) =>
        {
            var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == r.Correo);
            if (u is null || !PasswordHasher.Verify(r.Password, u.ContrasenaHash))
                return Results.Unauthorized();
            return Results.Ok(new { id = u.Id, nombre = u.Nombre, token = jwt.Issue(u.Id, u.Correo) });
        });
    }
}
