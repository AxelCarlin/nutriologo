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
        int idUsuario, DateTime fechaConsumo, string? nombre, IReadOnlyList<MealItemInput> items)
    {
        var v = RegisterMealValidator.Validate(new MealInput(idUsuario, fechaConsumo, items), catalog);
        if (!v.Exito) return new(false, v.RequiereAclaracion, v.Mensaje, null);

        var comida = new RegistroComida(idUsuario, fechaConsumo, nombre);
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
            if (alimentoFuente is not null)
            {
                var baseNutr = await db.BasesNutricionales.FirstOrDefaultAsync(b => b.IdAlimentoFuente == alimentoFuente.Id);
                var energia = await db.AlimentosNutriente
                    .Join(db.Nutrientes, an => an.IdNutriente, n => n.Id, (an, n) => new { an, n })
                    .Where(x => x.an.IdAlimentoFuente == alimentoFuente.Id && x.n.Codigo == "ENERGIA")
                    .Select(x => x.an.Cantidad)
                    .FirstOrDefaultAsync();
                if (baseNutr is not null && energia > 0)
                    kcal = NutricionScaling.Escalar(energia, baseNutr.CantidadBase, it.Cantidad);
            }

            db.AlimentosRegistroComida.Add(new AlimentoRegistroComida(comida.Id, v.Alimentos[i].IdAlimento, unidad.Id, it.Cantidad, kcal));
            if (kcal is not null) totalKcal = (totalKcal ?? 0m) + kcal;
        }

        if (totalKcal is not null)
            typeof(RegistroComida).GetProperty("CaloriasEstimadas")!.SetValue(comida, totalKcal);
        await db.SaveChangesAsync();
        return new(true, false, "Comida registrada", comida.Id);
    }
}
