namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Import;

internal static class ImportMapping
{
    public static void Apply(ModelBuilder b)
    {
        var c = b.Entity<TipoEntradaPlan>();
        c.ToTable("TipoEntradaPlan"); Ep(c);
        c.Property(x => x.Codigo).HasColumnType("varchar(50)").IsRequired();
        c.HasIndex(x => x.Codigo).IsUnique();
        c.Property(x => x.Descripcion).HasColumnType("varchar(100)").IsRequired();

        var i = b.Entity<ImportacionPlan>();
        i.ToTable("ImportacionPlan"); Ep(i);
        i.Property(x => x.EntradaOriginal).HasColumnType("text").IsRequired();
        i.Property(x => x.InterpretacionJson).HasColumnType("jsonb");
        i.Property(x => x.Resultado).HasColumnType("text");
        i.Property(x => x.Confianza).HasColumnType("decimal(5,4)");
        i.Property(x => x.FechaImportacion).HasColumnType("timestamp");

        var im = b.Entity<ImportacionPlanImagen>();
        im.ToTable("ImportacionPlanImagen"); Ep(im);
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> Ep<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e) where T : Domain.Common.AuditableEntity
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
        return e;
    }
}
