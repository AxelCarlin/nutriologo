using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Entities;
using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Nutrition;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class NutricionEndpoints
{
    public static void MapNutricion(this WebApplication app)
    {
        var g = app.MapGroup("/api");

        g.MapPost("/perfil", async (PerfilRequest r, AppDbContext db) =>
        {
            var p = new PerfilUsuario(r.IdUsuario, r.IdSexoBiologico, r.IdNivelActividadFisica,
                r.FechaNacimiento, r.Altura, r.AlturaUnidad ?? "cm", DateTime.UtcNow, r.IdUsuario);
            db.PerfilesUsuario.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/perfil/{p.Id}", new { p.Id });
        });

        g.MapPost("/mediciones", async (MedicionRequest r, AppDbContext db) =>
        {
            var m = new MedicionCorporal(r.IdUsuario, r.IdTipoMedicionCorporal, r.Valor, r.FechaMedicion, r.Observacion, r.IdUsuario);
            db.MedicionesCorporales.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/mediciones/{m.Id}", new { m.Id });
        });

        g.MapGet("/mediciones", async (int idUsuario, AppDbContext db) =>
        {
            var items = await db.MedicionesCorporales.Where(x => x.IdUsuario == idUsuario)
                .OrderByDescending(x => x.FechaMedicion)
                .Select(x => new { x.Id, x.IdTipoMedicionCorporal, x.Valor, x.FechaMedicion, x.Observacion })
                .ToListAsync();
            return Results.Ok(items);
        });

        g.MapPost("/objetivo/calcular", (ObjetivoRequest r, AppDbContext db) =>
        {
            NutritionSafetyGuard.EnsureAllowed(r.TipoObjetivo, r.EdadAnios, r.Estado);
            ICalorieCalculator calc = new MifflinStJeorCalorieCalculator();
            var est = calc.Estimate(new CalorieInput(r.PesoKg, r.AlturaCm, r.EdadAnios, r.Sexo, r.FactorActividad,
                r.TipoObjetivo, r.PesoObjetivoKg, r.SemanasObjetivo));
            return Results.Ok(est);
        });
    }

    public record PerfilRequest(int IdUsuario, int IdSexoBiologico, int IdNivelActividadFisica, DateTime FechaNacimiento, decimal Altura, string? AlturaUnidad);
    public record MedicionRequest(int IdUsuario, int IdTipoMedicionCorporal, decimal Valor, DateTime FechaMedicion, string? Observacion);
    public record ObjetivoRequest(decimal PesoKg, decimal AlturaCm, int EdadAnios, SexoBiologico Sexo, decimal FactorActividad, TipoObjetivo TipoObjetivo, decimal? PesoObjetivoKg, decimal? SemanasObjetivo, EstadoFisiologicoCodigo Estado = EstadoFisiologicoCodigo.Ninguno);
}
