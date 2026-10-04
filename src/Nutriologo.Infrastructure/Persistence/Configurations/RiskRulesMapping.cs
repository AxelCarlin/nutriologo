namespace Nutriologo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;

/// <summary>Read models ligeros para reglas de riesgo (solo lectura en Fase I3).</summary>
public class ReglaRiesgoObjetivoRec
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal? RitmoMinimo { get; set; }
    public decimal? RitmoMaximo { get; set; }
    public int IdNivelRiesgo { get; set; }
    public int Orden { get; set; }
}

public class ReglaRiesgoMetaCaloricaRec
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal? CaloriasMinimas { get; set; }
    public decimal? DeficitMaximo { get; set; }
    public int IdNivelRiesgo { get; set; }
    public int Orden { get; set; }
}

internal static class RiskRulesMapping
{
    public static void Apply(ModelBuilder b)
    {
        var r = b.Entity<ReglaRiesgoObjetivoRec>();
        r.ToTable("ReglaRiesgoObjetivo");
        r.HasNoKey();
        r.Property(x => x.Codigo).HasColumnType("varchar(50)");
        r.Property(x => x.Descripcion).HasColumnType("varchar(255)");
        r.Property(x => x.RitmoMinimo).HasColumnType("decimal(8,4)");
        r.Property(x => x.RitmoMaximo).HasColumnType("decimal(8,4)");

        var m = b.Entity<ReglaRiesgoMetaCaloricaRec>();
        m.ToTable("ReglaRiesgoMetaCalorica");
        m.HasNoKey();
        m.Property(x => x.Codigo).HasColumnType("varchar(50)");
        m.Property(x => x.Descripcion).HasColumnType("varchar(255)");
        m.Property(x => x.CaloriasMinimas).HasColumnType("decimal(10,2)");
        m.Property(x => x.DeficitMaximo).HasColumnType("decimal(10,2)");
    }
}
