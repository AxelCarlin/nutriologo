namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nutriologo.Domain.Catalog;

internal static class CatalogMapping
{
    public static void ApplyCatalog(ModelBuilder b)
    {
        Catalog<FuenteAlimento>(b, "FuenteAlimento", e => e.Property(x => x.UrlFuente).HasColumnType("varchar(500)"));
        Catalog<CategoriaAlimento>(b, "CategoriaAlimento", e => { e.Property(x => x.Nivel).IsRequired(); e.Property(x => x.IdCategoriaPadre); });
        Catalog<CocinaAlimentaria>(b, "CocinaAlimentaria");
        MarcaCfg(b);
        Catalog<EstadoPreparacionAlimento>(b, "EstadoPreparacionAlimento", e => e.Property(x => x.Descripcion).HasColumnType("varchar(100)"));
        Catalog<MetodoPreparacion>(b, "MetodoPreparacion", e => e.Property(x => x.Descripcion).HasColumnType("varchar(100)"));
        AlimentoCfg(b);
        AlimentoCocinaCfg(b);
        AlimentoFuenteCfg(b);
        Catalog<TipoNutriente>(b, "TipoNutriente", e => e.Property(x => x.Descripcion).HasColumnType("varchar(100)"));
        NutrienteCfg(b);
        AlimentoNutrienteCfg(b);
        Catalog<TipoUnidadMedida>(b, "TipoUnidadMedida", e => e.Property(x => x.Descripcion).HasColumnType("varchar(100)"));
        UnidadMedidaCfg(b);
        BaseNutricionalCfg(b);
        ConversionCfg(b);
        PorcionCfg(b);
    }

    private static void Audit<T>(EntityTypeBuilder<T> b) where T : Domain.Common.AuditableEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.FechaCreacion).HasColumnType("timestamp");
        b.Property(x => x.FechaModificacion).HasColumnType("timestamp");
        b.Property(x => x.Estatus);
        b.Property(x => x.IdUsuarioCrea);
        b.Property(x => x.IdUsuarioModifica);
    }

    private static void Catalog<T>(ModelBuilder b, string table, Action<EntityTypeBuilder<T>>? extra = null) where T : CatalogoBase
    {
        var e = b.Entity<T>();
        e.ToTable(table);
        Audit(e);
        e.Property(x => x.Codigo).HasColumnType("varchar(50)").IsRequired();
        e.HasIndex(x => x.Codigo).IsUnique();
        e.Property(x => x.Descripcion).HasColumnType("varchar(150)").IsRequired();
        extra?.Invoke(e);
    }

    private static void MarcaCfg(ModelBuilder b)
    {
        var e = b.Entity<Marca>();
        e.ToTable("Marca"); Audit(e);
        e.Property(x => x.Nombre).HasColumnType("varchar(150)").IsRequired();
        e.Property(x => x.Descripcion).HasColumnType("varchar(255)");
    }

    private static void AlimentoCfg(ModelBuilder b)
    {
        var e = b.Entity<Alimento>();
        e.ToTable("Alimento"); Audit(e);
        e.Property(x => x.Nombre).HasColumnType("varchar(200)").IsRequired();
        e.Property(x => x.Descripcion).HasColumnType("text");
        e.Property(x => x.EsReceta).HasDefaultValue(false);
        e.Property(x => x.IdCategoriaAlimento).IsRequired();
    }

    private static void AlimentoCocinaCfg(ModelBuilder b)
    {
        var e = b.Entity<AlimentoCocina>();
        e.ToTable("AlimentoCocina"); Audit(e);
        e.Property(x => x.EsPrincipal).HasDefaultValue(false);
    }

    private static void AlimentoFuenteCfg(ModelBuilder b)
    {
        var e = b.Entity<AlimentoFuente>();
        e.ToTable("AlimentoFuente"); Audit(e);
        e.Property(x => x.CodigoExterno).HasColumnType("varchar(150)");
        e.Property(x => x.VersionFuente).HasColumnType("varchar(100)");
        e.Property(x => x.NombreFuente).HasColumnType("varchar(255)");
        e.Property(x => x.DescripcionFuente).HasColumnType("text");
        e.Property(x => x.FechaReferencia).HasColumnType("timestamp");
    }

    private static void NutrienteCfg(ModelBuilder b)
    {
        var e = b.Entity<Nutriente>();
        e.ToTable("Nutriente"); Audit(e);
        e.Property(x => x.Codigo).HasColumnType("varchar(50)").IsRequired();
        e.HasIndex(x => x.Codigo).IsUnique();
        e.Property(x => x.Descripcion).HasColumnType("varchar(150)").IsRequired();
    }

    private static void AlimentoNutrienteCfg(ModelBuilder b)
    {
        var e = b.Entity<AlimentoNutriente>();
        e.ToTable("AlimentoNutriente"); Audit(e);
        e.Property(x => x.Cantidad).HasColumnType("decimal(14,6)");
    }

    private static void UnidadMedidaCfg(ModelBuilder b)
    {
        var e = b.Entity<UnidadMedida>();
        e.ToTable("UnidadMedida"); Audit(e);
        e.Property(x => x.Codigo).HasColumnType("varchar(30)").IsRequired();
        e.HasIndex(x => x.Codigo).IsUnique();
        e.Property(x => x.Descripcion).HasColumnType("varchar(100)").IsRequired();
        e.Property(x => x.Simbolo).HasColumnType("varchar(20)");
    }

    private static void BaseNutricionalCfg(ModelBuilder b)
    {
        var e = b.Entity<BaseNutricionalAlimento>();
        e.ToTable("BaseNutricionalAlimento"); Audit(e);
        e.Property(x => x.CantidadBase).HasColumnType("decimal(14,6)");
    }

    private static void ConversionCfg(ModelBuilder b)
    {
        var e = b.Entity<ConversionUnidad>();
        e.ToTable("ConversionUnidad"); Audit(e);
        e.Property(x => x.FactorConversion).HasColumnType("decimal(18,8)");
        e.Property(x => x.Formula).HasColumnType("varchar(255)");
    }

    private static void PorcionCfg(ModelBuilder b)
    {
        var e = b.Entity<PorcionAlimento>();
        e.ToTable("PorcionAlimento"); Audit(e);
        e.Property(x => x.Descripcion).HasColumnType("varchar(150)").IsRequired();
        e.Property(x => x.Cantidad).HasColumnType("decimal(14,6)");
        e.Property(x => x.CantidadGramos).HasColumnType("decimal(14,6)");
    }
}
