namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Entities;

internal static class ProfileMapping
{
    public static void Apply(ModelBuilder b)
    {
        var p = b.Entity<PerfilUsuario>();
        p.ToTable("PerfilUsuario"); Ep(p);
        p.Property(x => x.FechaNacimiento).HasColumnType("timestamp");
        p.Property(x => x.Altura).HasColumnType("decimal(6,2)");
        p.Property(x => x.AlturaUnidad).HasColumnType("varchar(20)");
        p.Property(x => x.FechaInicioPerfil).HasColumnType("timestamp");

        var m = b.Entity<MedicionCorporal>();
        m.ToTable("MedicionCorporal"); Ep(m);
        m.Property(x => x.Valor).HasColumnType("decimal(10,3)");
        m.Property(x => x.FechaMedicion).HasColumnType("timestamp");
        m.Property(x => x.Observacion).HasColumnType("text");
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
