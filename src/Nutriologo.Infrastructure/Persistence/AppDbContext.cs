namespace Nutriologo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Conversations;
using Nutriologo.Domain.Entities;
using Nutriologo.Domain.Import;
using Nutriologo.Infrastructure.Persistence.Configurations;
using Nutriologo.Domain.Feedback;
using Nutriologo.Infrastructure.Persistence.Configurations;
using Nutriologo.Domain.Plans;
using Nutriologo.Domain.Recipes;
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
    public DbSet<RecetaAlimento> RecetasAlimento => Set<RecetaAlimento>();
    public DbSet<IngredienteReceta> IngredientesReceta => Set<IngredienteReceta>();
    public DbSet<RecetaUsuario> RecetasUsuario => Set<RecetaUsuario>();
    public DbSet<PlanAlimenticio> PlanesAlimenticios => Set<PlanAlimenticio>();
    public DbSet<DiaPlanAlimenticio> DiasPlan => Set<DiaPlanAlimenticio>();
    public DbSet<ComidaPlan> ComidasPlan => Set<ComidaPlan>();
    public DbSet<AlimentoComidaPlan> AlimentosComidaPlan => Set<AlimentoComidaPlan>();
    public DbSet<RegistroComida> RegistrosComida => Set<RegistroComida>();
    public DbSet<AlimentoRegistroComida> AlimentosRegistroComida => Set<AlimentoRegistroComida>();
    public DbSet<BalanceCalorico> BalancesCaloricos => Set<BalanceCalorico>();
    public DbSet<EvaluacionAnalisisIA> EvaluacionesAnalisisIA => Set<EvaluacionAnalisisIA>();
    public DbSet<CorreccionPrediccion> CorreccionesPrediccion => Set<CorreccionPrediccion>();
    public DbSet<EvaluacionMensaje> EvaluacionesMensaje => Set<EvaluacionMensaje>();
    public DbSet<TipoEntradaPlan> TiposEntradaPlan => Set<TipoEntradaPlan>();
    public DbSet<ImportacionPlan> ImportacionesPlan => Set<ImportacionPlan>();
    public DbSet<ImportacionPlanImagen> ImportacionesPlanImagen => Set<ImportacionPlanImagen>();
    public DbSet<PerfilUsuario> PerfilesUsuario => Set<PerfilUsuario>();
    public DbSet<MedicionCorporal> MedicionesCorporales => Set<MedicionCorporal>();
    public DbSet<ReglaRiesgoObjetivoRec> ReglaRiesgoObjetivos => Set<ReglaRiesgoObjetivoRec>();
    public DbSet<ReglaRiesgoMetaCaloricaRec> ReglaRiesgoMetasCaloricas => Set<ReglaRiesgoMetaCaloricaRec>();
    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        CatalogMapping.ApplyCatalog(modelBuilder);
        RecipesMapping.ApplyRecipes(modelBuilder);
        PlansConsumptionMapping.Apply(modelBuilder);
        FeedbackMapping.Apply(modelBuilder);
        ImportMapping.Apply(modelBuilder);
        ProfileMapping.Apply(modelBuilder);
        RiskRulesMapping.Apply(modelBuilder);
        ConversationMapping.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
        // Schema usa timestamp (sin zona): normaliza DateTime UTC a Kind=Unspecified.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var prop in entityType.GetProperties())
        {
            if (prop.ClrType == typeof(DateTime))
                prop.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v, v => v));
            else if (prop.ClrType == typeof(DateTime?))
                prop.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                    v => v.HasValue && v.Value.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v, v => v));
        }
    }
}
