using Microsoft.EntityFrameworkCore;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class MealRegistrar
{
    public record Result(bool Ok, bool RequiereAclaracion, string Mensaje, int? Id);

    public static async Task<Result> RegisterAsync(AppDbContext db, IFoodCatalogSource catalog,
        int idUsuario, DateTime fechaConsumo, string? nombre, IReadOnlyList<MealItemInput> items,
        int? idTipoComida = null)
    {
        var v = RegisterMealValidator.Validate(new MealInput(idUsuario, fechaConsumo, items), catalog);
        if (!v.Exito) return new(false, v.RequiereAclaracion, v.Mensaje, null);

        // El tipo de comida se persiste solo si existe en el catálogo. Con un Id
        // desconocido se guarda null (se muestra "Sin tipo") en vez de inventar
        // una fila que rompa la FK.
        // `SqlQuery` (no `SqlQueryRaw`): el Id viaja como parámetro.
        if (idTipoComida is not null)
        {
            var tipoExiste = await db.Database
                .SqlQuery<int>($"""SELECT "Id" FROM "TipoComida" WHERE "Id" = {idTipoComida.Value} LIMIT 1""")
                .AnyAsync();
            if (!tipoExiste) idTipoComida = null;
        }

        var comida = new RegistroComida(idUsuario, fechaConsumo, nombre, idTipoComida);
        db.RegistrosComida.Add(comida);
        await db.SaveChangesAsync();

        decimal? totalKcal = null;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var unidad = await db.UnidadesMedida.FirstOrDefaultAsync(u => u.Codigo == it.Unidad.ToUpperInvariant());
            if (unidad is null) return new(false, false, $"Unidad desconocida: {it.Unidad}", null);

            var alimentoFuente = await db.AlimentosFuente.FirstOrDefaultAsync(af => af.IdAlimento == v.Alimentos[i].IdAlimento);
            decimal? kcal = null;

            // La composicion nutricional esta declarada sobre una base (habitualmente
            // 100 g). Antes se escalaba por it.Cantidad SIN convertir de la unidad del
            // usuario a esa base: "2 PIEZA" se trataba como 2 g. Convierte primero.
            var gramos = await AGramosAsync(db, v.Alimentos[i].IdAlimento, unidad.Id, it.Cantidad);

            if (alimentoFuente is not null && gramos is not null)
            {
                var baseNutr = await db.BasesNutricionales.FirstOrDefaultAsync(b => b.IdAlimentoFuente == alimentoFuente.Id);
                var unidadBase = baseNutr is null ? null
                    : await db.UnidadesMedida.FirstOrDefaultAsync(u => u.Id == baseNutr.IdUnidadMedida);
                var energia = await db.AlimentosNutriente
                    .Join(db.Nutrientes, an => an.IdNutriente, n => n.Id, (an, n) => new { an, n })
                    .Where(x => x.an.IdAlimentoFuente == alimentoFuente.Id && x.n.Codigo == "ENERGIA")
                    .Select(x => x.an.Cantidad)
                    .FirstOrDefaultAsync();

                if (baseNutr is not null && energia > 0)
                {
                    // Expresa la cantidad del usuario en la unidad de la base.
                    var enBase = await ABaseAsync(db, gramos.Value, v.Alimentos[i].IdAlimento, unidadBase?.Id);
                    kcal = NutricionScaling.Escalar(energia, baseNutr.CantidadBase, enBase ?? it.Cantidad);
                }
            }

            db.AlimentosRegistroComida.Add(new AlimentoRegistroComida(comida.Id, v.Alimentos[i].IdAlimento, unidad.Id, it.Cantidad, gramos, kcal));
            if (kcal is not null) totalKcal = (totalKcal ?? 0m) + kcal;
        }

        if (totalKcal is not null)
            typeof(RegistroComida).GetProperty("CaloriasEstimadas")!.SetValue(comida, totalKcal);
        await db.SaveChangesAsync();
        return new(true, false, "Comida registrada", comida.Id);
    }

    /// <summary>
    /// Convierte (cantidad, unidad del usuario) a gramos.
    /// Orden de resolucion:
    ///   1. La unidad ya es masa (G/KG) -> conversion directa.
    ///   2. Existe una PorcionAlimento para ese alimento+unidad (p.ej. 1 PIEZA de
    ///      huevo = 50 g) -> escala por esa porcion.
    ///   3. No se puede resolver -> null (se omite el aporte, no se inventa).
    /// </summary>
    private static async Task<decimal?> AGramosAsync(AppDbContext db, int idAlimento, int idUnidad, decimal cantidad)
    {
        var unidad = await db.UnidadesMedida.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUnidad);
        if (unidad is null) return null;

        var tipo = await db.TiposUnidadMedida.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == unidad.IdTipoUnidadMedida);
        var esMasa = tipo?.Codigo == "MASA";

        if (esMasa)
        {
            if (unidad.Codigo == "G") return cantidad;
            if (unidad.Codigo == "KG") return cantidad * 1000m;
            var factor = await db.ConversionesUnidad.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdUnidadOrigen == idUnidad && c.IdUnidadDestino == 1);
            return factor is null ? null : cantidad * factor.FactorConversion;
        }

        // Volumen o pieza: usar la porción declarada para ese alimento.
        var porcion = await db.PorcionesAlimento.AsNoTracking()
            .Where(p => p.IdAlimento == idAlimento && p.IdUnidadMedida == idUnidad)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync();
        if (porcion is null || porcion.Cantidad <= 0) return null;

        var gramosPorPorcion = porcion.CantidadGramos ?? porcion.Cantidad;
        return Math.Round(cantidad * gramosPorPorcion / porcion.Cantidad, 6);
    }

    /// <summary>
    /// Expresa una cantidad en gramos dentro de la unidad en la que está
    /// declarada la base nutricional del alimento.
    /// </summary>
    private static async Task<decimal?> ABaseAsync(AppDbContext db, decimal gramos, int idAlimento, int? idUnidadBase)
    {
        if (idUnidadBase is null) return gramos;

        var baseAlimento = await db.AlimentosFuente.AsNoTracking()
            .FirstOrDefaultAsync(af => af.IdAlimento == idAlimento);
        if (baseAlimento is null) return gramos;

        var baseNutr = await db.BasesNutricionales.AsNoTracking()
            .FirstOrDefaultAsync(b => b.IdAlimentoFuente == baseAlimento.Id);
        if (baseNutr is null) return gramos;

        // El id de la unidad base se resuelve por tipo; si la base es masa y ya
        // temos gramos, no hace falta convertir.
        var idTipoBase = await db.UnidadesMedida.AsNoTracking()
            .Where(u => u.Id == baseNutr.IdUnidadMedida)
            .Select(u => u.IdTipoUnidadMedida)
            .FirstOrDefaultAsync();
        var tipoBase = await db.TiposUnidadMedida.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == idTipoBase);
        if (tipoBase?.Codigo == "MASA") return gramos;

        var porcionBase = await db.PorcionesAlimento.AsNoTracking()
            .Where(p => p.IdAlimento == idAlimento && p.IdUnidadMedida == idUnidadBase)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync();
        if (porcionBase is null || porcionBase.Cantidad <= 0) return gramos;

        var gramosPorBase = porcionBase.CantidadGramos ?? porcionBase.Cantidad;
        return Math.Round(gramos * porcionBase.Cantidad / gramosPorBase, 6);
    }
}
