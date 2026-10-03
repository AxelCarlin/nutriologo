namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Plans;

internal static class PlansConsumptionMapping
{
    public static void Apply(ModelBuilder b)
    {
        Plan<PlanAlimenticio>(b, "PlanAlimenticio", e =>
        {
            e.Property(x => x.Nombre).HasColumnType("varchar(200)").IsRequired();
            e.Property(x => x.Descripcion).HasColumnType("text");
            e.Property(x => x.FechaInicio).HasColumnType("timestamp");
            e.Property(x => x.FechaFin).HasColumnType("timestamp");
            e.Property(x => x.CaloriasObjetivo).HasColumnType("decimal(10,2)");
        });
        Plan<DiaPlanAlimenticio>(b, "DiaPlanAlimenticio", e =>
        {
            e.Property(x => x.FechaPlan).HasColumnType("timestamp");
            e.Property(x => x.Descripcion).HasColumnType("varchar(255)");
        });
        Plan<ComidaPlan>(b, "ComidaPlan", e =>
        {
            e.Property(x => x.Nombre).HasColumnType("varchar(200)");
            e.Property(x => x.HoraSugerida).HasColumnType("timestamp");
            e.Property(x => x.Observaciones).HasColumnType("text");
        });
        Line(b.Entity<AlimentoComidaPlan>());
        Plan<RegistroComida>(b, "RegistroComida", e =>
        {
            e.Property(x => x.Nombre).HasColumnType("varchar(200)");
            e.Property(x => x.Descripcion).HasColumnType("text");
            e.Property(x => x.FechaConsumo).HasColumnType("timestamp");
            e.Property(x => x.CaloriasEstimadas).HasColumnType("decimal(10,2)");
            e.Property(x => x.Observacion).HasColumnType("text");
        });
        LineReg<AlimentoRegistroComida>(b, "AlimentoRegistroComida");
        Bal(b);
    }

    private static void Plan<T>(ModelBuilder b, string table, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>> extra) where T : Domain.Common.AuditableEntity
    {
        var e = b.Entity<T>();
        e.ToTable(table);
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
        extra(e);
    }

    private static void Line(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AlimentoComidaPlan> e)
    {
        e.ToTable("AlimentoComidaPlan");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.Cantidad).HasColumnType("decimal(14,6)");
        e.Property(x => x.CantidadGramos).HasColumnType("decimal(14,6)");
        e.Property(x => x.Observacion).HasColumnType("varchar(255)");
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
    }

    private static void LineReg<T>(ModelBuilder b, string table) where T : Domain.Common.AuditableEntity
    {
        var e = b.Entity<AlimentoRegistroComida>();
        e.ToTable(table);
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.Cantidad).HasColumnType("decimal(14,6)");
        e.Property(x => x.CantidadGramos).HasColumnType("decimal(14,6)");
        e.Property(x => x.CaloriasEstimadas).HasColumnType("decimal(10,2)");
        e.Property(x => x.Confianza).HasColumnType("decimal(5,4)");
        e.Property(x => x.Observacion).HasColumnType("text");
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
    }

    private static void Bal(ModelBuilder b)
    {
        var e = b.Entity<BalanceCalorico>();
        e.ToTable("BalanceCalorico");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.FechaBalance).HasColumnType("timestamp");
        e.Property(x => x.CaloriasConsumidas).HasColumnType("decimal(10,2)");
        e.Property(x => x.CaloriasObjetivo).HasColumnType("decimal(10,2)");
        e.Property(x => x.Balance).HasColumnType("decimal(10,2)");
        e.Property(x => x.Observacion).HasColumnType("text");
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
    }
}
