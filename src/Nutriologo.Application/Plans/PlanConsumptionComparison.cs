namespace Nutriologo.Application.Plans;

/// <summary>Ítem planificado vs ítem consumido, ya resueltos por IdAlimento.</summary>
public record PlanItemDto(DateTime Fecha, int IdTipoComida, int IdAlimento, decimal Cantidad, int IdUnidadMedida, decimal? Calorias);
public record ConsumptionItemDto(DateTime Fecha, int? IdTipoComida, int IdAlimento, decimal Cantidad, int IdUnidadMedida, decimal? Calorias);

public enum ComparacionEstado { Coincidencia, CantidadDiferente, PlanificadoSinConsumo, ConsumidoSinPlan }

public record ComparacionPlanConsumo(
    DateTime Fecha,
    int? IdTipoComida,
    int? IdAlimento,
    decimal? CantidadPlanificada,
    decimal? CantidadConsumida,
    decimal? DiferenciaCantidad,
    decimal? CaloriasPlanificadas,
    decimal? CaloriasConsumidas,
    decimal? DiferenciaCalorias,
    ComparacionEstado Estado);

public static class PlanConsumptionComparisonService
{
    /// <summary>Compara sin fusionar: produce diferencias; nunca modifica plan ni registros.</summary>
    public static IReadOnlyList<ComparacionPlanConsumo> Compare(
        IEnumerable<PlanItemDto> plan, IEnumerable<ConsumptionItemDto> real)
    {
        var planItems = plan.ToList();
        var realItems = real.ToList();
        var resultado = new List<ComparacionPlanConsumo>();
        var emparejadosReal = new HashSet<ConsumptionItemDto>();

        foreach (var p in planItems)
        {
            var match = realItems.FirstOrDefault(r => r.Fecha.Date == p.Fecha.Date &&
                r.IdTipoComida == p.IdTipoComida && r.IdAlimento == p.IdAlimento &&
                !emparejadosReal.Contains(r));

            if (match is null)
            {
                resultado.Add(new(p.Fecha, p.IdTipoComida, p.IdAlimento, p.Cantidad, null, null, p.Calorias, null, null, ComparacionEstado.PlanificadoSinConsumo));
                continue;
            }

            emparejadosReal.Add(match);
            bool mismo = match.Cantidad == p.Cantidad && match.IdUnidadMedida == p.IdUnidadMedida;
            resultado.Add(new(p.Fecha, p.IdTipoComida, p.IdAlimento, p.Cantidad, match.Cantidad,
                match.Cantidad - p.Cantidad, p.Calorias, match.Calorias,
                match.Calorias is not null && p.Calorias is not null ? match.Calorias - p.Calorias : null,
                mismo ? ComparacionEstado.Coincidencia : ComparacionEstado.CantidadDiferente));
        }

        foreach (var r in realItems.Where(r => !emparejadosReal.Contains(r)))
            resultado.Add(new(r.Fecha, r.IdTipoComida, r.IdAlimento, null, r.Cantidad, null, null, r.Calorias, null, ComparacionEstado.ConsumidoSinPlan));

        return resultado.OrderBy(x => x.Fecha).ThenBy(x => x.IdTipoComida).ToList();
    }
}
