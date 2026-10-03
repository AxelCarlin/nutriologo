namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Recipes;

internal static class RecipesMapping
{
    public static void ApplyRecipes(Microsoft.EntityFrameworkCore.ModelBuilder b)
    {
        var r = b.Entity<RecetaAlimento>();
        r.ToTable("RecetaAlimento");
        r.HasKey(x => x.Id);
        r.Property(x => x.Id).ValueGeneratedOnAdd();
        r.Property(x => x.PesoInicial).HasColumnType("decimal(14,6)");
        r.Property(x => x.PesoFinal).HasColumnType("decimal(14,6)");
        r.Property(x => x.CantidadPorciones).HasColumnType("decimal(10,3)");
        r.Property(x => x.PesoPorPorcion).HasColumnType("decimal(14,6)");
        r.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        r.Property(x => x.FechaModificacion).HasColumnType("timestamp");

        var i = b.Entity<IngredienteReceta>();
        i.ToTable("IngredienteReceta");
        i.HasKey(x => x.Id);
        i.Property(x => x.Id).ValueGeneratedOnAdd();
        i.Property(x => x.Cantidad).HasColumnType("decimal(14,6)");
        i.Property(x => x.CantidadGramos).HasColumnType("decimal(14,6)");
        i.Property(x => x.Descripcion).HasColumnType("varchar(255)");
        i.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        i.Property(x => x.FechaModificacion).HasColumnType("timestamp");

        var u = b.Entity<RecetaUsuario>();
        u.ToTable("RecetaUsuario");
        u.HasKey(x => x.Id);
        u.Property(x => x.Id).ValueGeneratedOnAdd();
        u.Property(x => x.NombrePersonalizado).HasColumnType("varchar(200)");
        u.Property(x => x.DescripcionPersonalizada).HasColumnType("text");
        u.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        u.Property(x => x.FechaModificacion).HasColumnType("timestamp");
    }
}
