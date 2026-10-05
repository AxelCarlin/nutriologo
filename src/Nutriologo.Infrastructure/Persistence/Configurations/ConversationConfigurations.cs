namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nutriologo.Domain.Conversations;

internal static class ConversationMapping
{
    public static void Apply(ModelBuilder b)
    {
        var c = b.Entity<Conversacion>();
        c.ToTable("Conversacion"); Audit(c);
        c.Property(x => x.Titulo).HasColumnType("varchar(255)");
        c.Property(x => x.FechaInicio).HasColumnType("timestamp");
        c.Property(x => x.FechaFin).HasColumnType("timestamp");
        c.HasIndex(x => new { x.IdUsuario, x.FechaFin });

        var m = b.Entity<Mensaje>();
        m.ToTable("Mensaje"); Audit(m);
        m.Property(x => x.Contenido).HasColumnType("text");
        m.Property(x => x.FechaMensaje).HasColumnType("timestamp");
        m.HasIndex(x => new { x.IdConversacion, x.FechaMensaje });

        // El estado del form "comida en curso": una columna JSON en Conversacion.
        // Se prefiere a una tabla SlotEnCurso porque el shape es fijo (alimento,
        // cantidad, unidad) y hay a lo sumo un draft activo por conversación.
        c.Property<string?>("MealDraft")
            .HasColumnType("jsonb")
            .HasColumnName("MealDraft");
    }

    private static EntityTypeBuilder<T> Audit<T>(EntityTypeBuilder<T> e) where T : Domain.Common.AuditableEntity
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();
        e.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        e.Property(x => x.FechaModificacion).HasColumnType("timestamp");
        return e;
    }
}