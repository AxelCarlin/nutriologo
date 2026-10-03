namespace Nutriologo.Domain.Catalog;

using Nutriologo.Domain.Exceptions;

public static class ConversionService
{
    /// <summary>
    /// Aplica el factor de conversión directo entre unidad origen y destino.
    /// No encadena conversiones en esta fase: si no existe la conversión directa, lanza.
    /// </summary>
    public static decimal Convert(decimal cantidad, int idUnidadOrigen, int idUnidadDestino,
        IEnumerable<ConversionUnidad> conversiones)
    {
        if (idUnidadOrigen == idUnidadDestino) return cantidad;
        var c = conversiones.FirstOrDefault(x => x.IdUnidadOrigen == idUnidadOrigen && x.IdUnidadDestino == idUnidadDestino);
        if (c is null) throw new DomainException($"No existe conversión de {idUnidadOrigen} a {idUnidadDestino}.");
        return Math.Round(cantidad * c.FactorConversion, 6);
    }
}

public static class NutricionScaling
{
    /// <summary>
    /// Escala la composición nutricional definida sobre una base (p.ej. 100 g)
    /// a la cantidad realmente consumida en la misma unidad base.
    /// </summary>
    public static decimal Escalar(decimal cantidadPorBase, decimal cantidadBase, decimal cantidadConsumida)
    {
        if (cantidadBase <= 0) throw new DomainException("Cantidad base debe ser positiva.");
        if (cantidadConsumida < 0) throw new DomainException("Cantidad consumida no puede ser negativa.");
        return Math.Round(cantidadPorBase * cantidadConsumida / cantidadBase, 6);
    }
}
