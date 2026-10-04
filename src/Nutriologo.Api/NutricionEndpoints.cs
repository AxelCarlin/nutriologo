using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nutriologo.Api.Auth;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Entities;
using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Nutrition;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class NutricionEndpoints
{
    public static void MapNutricion(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireRateLimiting("business").RequireAuthorization();

        g.MapPost("/perfil", async (PerfilRequest r, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var p = new PerfilUsuario(idUsuario, r.IdSexoBiologico, r.IdNivelActividadFisica,
                r.FechaNacimiento, r.Altura, r.AlturaUnidad ?? "cm", DateTime.UtcNow, idUsuario);
            db.PerfilesUsuario.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/perfil/{p.Id}", new { p.Id });
        });

        g.MapPost("/mediciones", async (MedicionRequest r, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var m = new MedicionCorporal(idUsuario, r.IdTipoMedicionCorporal, r.Valor, r.FechaMedicion, r.Observacion, idUsuario);
            db.MedicionesCorporales.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/mediciones/{m.Id}", new { m.Id });
        });

        g.MapGet("/alimentos", async (AppDbContext db) =>
            await db.Alimentos.OrderBy(a => a.Nombre).Select(a => new { a.Id, a.Nombre }).ToListAsync());

        g.MapPost("/comidas", async (ComidaRequest r, ClaimsPrincipal user, AppDbContext db, IFoodCatalogSource catalog) =>
        {
            var result = await MealRegistrar.RegisterAsync(db, catalog, JwtIssuer.UserId(user), r.FechaConsumo, r.Nombre, r.Items);
            return result.Ok ? Results.Created($"/api/comidas/{result.Id}", new { id = result.Id })
                : Results.BadRequest(new { result.RequiereAclaracion, result.Mensaje });
        });

        g.MapGet("/comidas", async (DateTime? fecha, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var q = db.RegistrosComida.Where(x => x.IdUsuario == idUsuario);
            if (fecha is not null) q = q.Where(x => x.FechaConsumo.Date == fecha.Value.Date);
            var items = await q.OrderByDescending(x => x.FechaConsumo)
                .Select(x => new { x.Id, x.FechaConsumo, x.Nombre, x.CaloriasEstimadas })
                .ToListAsync();
            return Results.Ok(items);
        });

        g.MapGet("/balance", async (DateTime fecha, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var consumidas = await db.RegistrosComida
                .Where(x => x.IdUsuario == idUsuario && x.FechaConsumo.Date == fecha.Date)
                .SumAsync(x => (decimal?)x.CaloriasEstimadas) ?? 0m;
            return Results.Ok(new { fecha = fecha.Date, caloriasConsumidas = consumidas });
        });

        g.MapGet("/mediciones", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var items = await db.MedicionesCorporales.Where(x => x.IdUsuario == idUsuario)
                .OrderByDescending(x => x.FechaMedicion)
                .Select(x => new { x.Id, x.IdTipoMedicionCorporal, x.Valor, x.FechaMedicion, x.Observacion })
                .ToListAsync();
            return Results.Ok(items);
        });

        g.MapPost("/objetivo/calcular", async (ObjetivoRequest r, AppDbContext db) =>
        {
            NutritionSafetyGuard.EnsureAllowed(r.TipoObjetivo, r.EdadAnios, r.Estado);
            ICalorieCalculator calc = new MifflinStJeorCalorieCalculator();
            var est = calc.Estimate(new CalorieInput(r.PesoKg, r.AlturaCm, r.EdadAnios, r.Sexo, r.FactorActividad,
                r.TipoObjetivo, r.PesoObjetivoKg, r.SemanasObjetivo));

            // Reglas configurables vienen de DB (Infrastructure → Domain evaluators).
            var reglasRitmo = await db.ReglaRiesgoObjetivos
                .Select(x => new ReglaRiesgoObjetivoDef(x.Codigo, x.RitmoMinimo, x.RitmoMaximo, x.Orden, x.IdNivelRiesgo, x.Descripcion))
                .ToListAsync();
            var reglasMeta = await db.ReglaRiesgoMetasCaloricas
                .Select(x => new ReglaRiesgoMetaCaloricaDef(x.Codigo, x.CaloriasMinimas, x.DeficitMaximo, x.Orden, x.IdNivelRiesgo, x.Descripcion))
                .ToListAsync();

            object? evaluacionObjetivo = null, evaluacionMeta = null;
            if (est.RitmoSemanalKg is not null && reglasRitmo.Count > 0)
            {
                var e = RiesgoEvaluator.EvaluarRitmo(est.RitmoSemanalKg.Value, reglasRitmo);
                evaluacionObjetivo = new { e.ReglaAplicada.Codigo, e.ReglaAplicada.Descripcion, e.ReglaAplicada.IdNivelRiesgo };
            }
            if (est.MetaCaloricaEstimada is not null && reglasMeta.Count > 0)
            {
                var e = RiesgoEvaluator.EvaluarMetaProporcionada(est.MetaCaloricaEstimada.Value, est.GastoEnergeticoTotal, reglasMeta);
                evaluacionMeta = new { e.ReglaAplicada.Codigo, e.ReglaAplicada.Descripcion, e.ReglaAplicada.IdNivelRiesgo, e.DeficitCalculado, e.PorcentajeDeficit };
            }

            return Results.Ok(new
            {
                est.MetabolismoBasal,
                est.GastoEnergeticoTotal,
                est.DeficitDiarioRequerido,
                est.MetaCaloricaEstimada,
                est.RitmoSemanalKg,
                est.PorcentajeCambio,
                evaluacionObjetivo,
                evaluacionMetaCalorica = evaluacionMeta,
            });
        });
    }

    public record ComidaRequest(DateTime FechaConsumo, string? Nombre, IReadOnlyList<MealItemInput> Items);
    public record PerfilRequest(int IdSexoBiologico, int IdNivelActividadFisica, DateTime FechaNacimiento, decimal Altura, string? AlturaUnidad);
    public record MedicionRequest(int IdTipoMedicionCorporal, decimal Valor, DateTime FechaMedicion, string? Observacion);
    public record ObjetivoRequest(decimal PesoKg, decimal AlturaCm, int EdadAnios, SexoBiologico Sexo, decimal FactorActividad, TipoObjetivo TipoObjetivo, decimal? PesoObjetivoKg, decimal? SemanasObjetivo, EstadoFisiologicoCodigo Estado = EstadoFisiologicoCodigo.Ninguno);
}
