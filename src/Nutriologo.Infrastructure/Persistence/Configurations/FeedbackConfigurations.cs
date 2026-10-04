namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Feedback;

internal static class FeedbackMapping
{
    public static void Apply(ModelBuilder b)
    {
        var e = b.Entity<EvaluacionAnalisisIA>();
        e.ToTable("EvaluacionAnalisisIA"); Ep(e);
        e.Property(x => x.Puntuacion).HasColumnType("decimal(8,4)");
        e.Property(x => x.Comentario).HasColumnType("text");
        e.Property(x => x.FechaEvaluacion).HasColumnType("timestamp");

        var c = b.Entity<CorreccionPrediccion>();
        c.ToTable("CorreccionPrediccion"); Ep(c);
        c.Property(x => x.CantidadAnterior).HasColumnType("decimal(14,6)");
        c.Property(x => x.CantidadCorregida).HasColumnType("decimal(14,6)");
        c.Property(x => x.Motivo).HasColumnType("varchar(255)");
        c.Property(x => x.Comentario).HasColumnType("text");
        c.Property(x => x.FechaCorreccion).HasColumnType("timestamp");

        var m = b.Entity<EvaluacionMensaje>();
        m.ToTable("EvaluacionMensaje"); Ep(m);
        m.Property(x => x.Puntuacion).HasColumnType("decimal(8,4)");
        m.Property(x => x.Comentario).HasColumnType("text");
        m.Property(x => x.FechaEvaluacion).HasColumnType("timestamp");
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
