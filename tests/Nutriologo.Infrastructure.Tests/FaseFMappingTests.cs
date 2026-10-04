namespace Nutriologo.Infrastructure.Tests;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Plans;
using Nutriologo.Infrastructure.Persistence;
using Xunit;

public class FaseFMappingTests
{
    private static AppDbContext? TryCreate()
    {
        try
        {
            var cs = System.Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=nutriologo;Username=postgres;Password=SET_VIA_ENV";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(cs)
                .Options;
            var ctx = new AppDbContext(options);
            return ctx.Database.CanConnect() ? ctx : null;
        }
        catch { return null; }
    }

    private static T Make<T>(params (string Name, object? Value)[] props) where T : new()
    {
        var obj = new T();
        foreach (var (name, value) in props) typeof(T).GetProperty(name)!.SetValue(obj, value);
        return obj;
    }

    [Fact]
    public void EfCore_InsertaYLee_PlanYRegistroConRollback()
    {
        using var ctx = TryCreate();
        if (ctx is null) return;

        using var tx = ctx.Database.BeginTransaction();
        var plan = Make<PlanAlimenticio>(("IdUsuario", 1), ("IdTipoPlanAlimenticio", 1), ("IdOrigenPlanAlimenticio", 1),
            ("Nombre", "Plan test"), ("FechaInicio", DateTime.UtcNow), ("CaloriasObjetivo", 2000m),
            ("FechaCreacion", DateTime.UtcNow), ("FechaModificacion", DateTime.UtcNow), ("Estatus", (short)1), ("IdUsuarioCrea", 1));
        ctx.PlanesAlimenticios.Add(plan);
        var reg = Make<RegistroComida>(("IdUsuario", 1), ("FechaConsumo", DateTime.UtcNow), ("CaloriasEstimadas", 300m),
            ("FechaCreacion", DateTime.UtcNow), ("FechaModificacion", DateTime.UtcNow), ("Estatus", (short)1), ("IdUsuarioCrea", 1));
        ctx.RegistrosComida.Add(reg);
        ctx.SaveChanges();

        Assert.True(plan.Id > 0);
        Assert.True(reg.Id > 0);
        Assert.Equal(1, ctx.PlanesAlimenticios.Count(p => p.Nombre == "Plan test"));
        tx.Rollback();
    }
}
