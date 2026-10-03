namespace Nutriologo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Entities;
using Nutriologo.Infrastructure.Persistence.Configurations;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<FuenteAlimento> FuentesAlimento => Set<FuenteAlimento>();
    public DbSet<CategoriaAlimento> CategoriasAlimento => Set<CategoriaAlimento>();
    public DbSet<CocinaAlimentaria> CocinasAlimentarias => Set<CocinaAlimentaria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<EstadoPreparacionAlimento> EstadosPreparacion => Set<EstadoPreparacionAlimento>();
    public DbSet<MetodoPreparacion> MetodosPreparacion => Set<MetodoPreparacion>();
    public DbSet<Alimento> Alimentos => Set<Alimento>();
    public DbSet<AlimentoCocina> AlimentosCocina => Set<AlimentoCocina>();
    public DbSet<AlimentoFuente> AlimentosFuente => Set<AlimentoFuente>();
    public DbSet<TipoNutriente> TiposNutriente => Set<TipoNutriente>();
    public DbSet<Nutriente> Nutrientes => Set<Nutriente>();
    public DbSet<AlimentoNutriente> AlimentosNutriente => Set<AlimentoNutriente>();
    public DbSet<TipoUnidadMedida> TiposUnidadMedida => Set<TipoUnidadMedida>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<BaseNutricionalAlimento> BasesNutricionales => Set<BaseNutricionalAlimento>();
    public DbSet<ConversionUnidad> ConversionesUnidad => Set<ConversionUnidad>();
    public DbSet<PorcionAlimento> PorcionesAlimento => Set<PorcionAlimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        CatalogMapping.ApplyCatalog(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
