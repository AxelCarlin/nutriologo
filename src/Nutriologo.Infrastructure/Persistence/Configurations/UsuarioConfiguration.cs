namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nutriologo.Domain.Entities;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasColumnName("Nombre").HasColumnType("varchar(150)").IsRequired();
        b.Property(x => x.Correo).HasColumnName("Correo").HasColumnType("varchar(255)").IsRequired();
        b.HasIndex(x => x.Correo).IsUnique();
        b.Property(x => x.ContrasenaHash).HasColumnName("ContrasenaHash").HasColumnType("text").IsRequired();
        b.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion").HasColumnType("timestamp").IsRequired();
        b.Property(x => x.FechaModificacion).HasColumnName("FechaModificacion").HasColumnType("timestamp").IsRequired();
        b.Property(x => x.Estatus).HasColumnName("Estatus").IsRequired();
        b.Property(x => x.IdUsuarioCrea).HasColumnName("IdUsuarioCrea").IsRequired();
        b.Property(x => x.IdUsuarioModifica).HasColumnName("IdUsuarioModifica");
    }
}
