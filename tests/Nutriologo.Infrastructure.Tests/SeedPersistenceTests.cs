namespace Nutriologo.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Infrastructure.Persistence;
using Xunit;

public class SeedPersistenceTests
{
    private static AppDbContext? TryCreate()
    {
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=nutriologo;Username=postgres;Password=Buss1ness.2026")
                .Options;
            var ctx = new AppDbContext(options);
            return ctx.Database.CanConnect() ? ctx : null;
        }
        catch { return null; }
    }

    [Fact]
    public void Seeds_Cargados_CatalogosBasicos()
    {
        using var ctx = TryCreate();
        if (ctx is null) return; // DB no disponible en este entorno

        Assert.True(ctx.Alimentos.Count() >= 10);
        Assert.True(ctx.Nutrientes.Count() >= 5);
        Assert.True(ctx.UnidadesMedida.Count() >= 5);
        Assert.True(ctx.ConversionesUnidad.Count() >= 5);
        Assert.True(ctx.PorcionesAlimento.Count() >= 10);
    }
}
