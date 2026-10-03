namespace Nutriologo.Domain.Tests;

using System.Text;
using Nutriologo.Domain.Catalog;
using Xunit;
using Xunit.Abstractions;

/// <summary>G7: baseline del matching determinista (Fase D catalog seed).</summary>
public class FoodResolverEvaluationTests
{
    private readonly ITestOutputHelper _out;
    public FoodResolverEvaluationTests(ITestOutputHelper output) => _out = output;

    private static IFoodCatalogSource CatalogFromSeed() => new SeedCatalog();

    private sealed class SeedCatalog : IFoodCatalogSource
    {
        public IReadOnlyList<FoodCatalogItem> Todos() => new[]
        {
            new FoodCatalogItem(1, "Huevo"), new FoodCatalogItem(2, "Arroz"), new FoodCatalogItem(3, "Pollo"),
            new FoodCatalogItem(4, "Tortilla"), new FoodCatalogItem(5, "Frijoles"), new FoodCatalogItem(6, "Aguacate"),
            new FoodCatalogItem(7, "Aceite"), new FoodCatalogItem(8, "Salsa"), new FoodCatalogItem(9, "Agua"),
            new FoodCatalogItem(10, "Refresco"),
        };
    }

    [Fact]
    public void EvaluacionBaseline_Determinista()
    {
        var casos = new (string Query, int? EsperadoId)[]
        {
            ("huevo", 1), ("huevos", 1), ("HUEVO", 1), ("huevo ", 1),
            ("arroz", 2), ("ARROZ", 2), ("pollo", 3), ("pollo asado", null), // ambiguo por catálogo sin "Pollo asado"? verificamos
            ("tortilla", 4), ("frijoles", 5), ("frijol", null), ("aguacate", 6),
            ("aceite", 7), ("salsa", 8), ("agua", 9), ("refresco", 10),
            ("pizza", null), ("pizza de pollo", null),
        };

        int correctas = 0, aclaracion = 0, incorrecto = 0;
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var (q, esperado) in casos)
        {
            var r = DeterministicFoodResolver.Resolver(q, CatalogFromSeed());
            var outcome = r.RequiereAclaracion ? "ACLARACION" : r.Candidatos[0].IdAlimento == esperado ? "OK" : "ERROR";
            if (outcome == "OK") correctas++;
            else if (r.RequiereAclaracion && esperado is null) correctas++;
            else if (r.RequiereAclaracion) aclaracion++;
            else incorrecto++;
            sb.AppendLine($"| `{q}` | esperado={(esperado?.ToString() ?? "aclaración")} | {outcome} | {r.Motivo} |");
        }
        sw.Stop();

        var precision = casos.Length == 0 ? 0 : (double)correctas / casos.Length;
        var report = $"# G7 — Evaluación baseline determinista\n\n- Casos: {casos.Length}\n- Correctos: {correctas}\n- Requiere aclaración (inesperada): {aclaracion}\n- Incorrectos: {incorrecto}\n- Precisión: {precision:P0}\n- Latencia total: {sw.ElapsedMilliseconds} ms ({casos.Length} consultas)\n\n| Query | Esperado | Resultado | Motivo |\n|---|---|---|---|\n{sb}\n\n## Decisión pgvector\n\nBaseline determinista suficiente para el catálogo actual (10+ alimentos). Comparación con pgvector/híbrido se realizará cuando existan embeddings disponibles (post-G8), con dataset ampliado y catálogo real.\n";
        File.WriteAllText(Path.Combine(FindRoot(), "docs", "eval-g7.md"), report);

        Assert.Equal(0, incorrecto); // sin falsos positivos; solo acierto o aclaración
        Assert.True(precision >= 0.85, $"Precisión {precision:P0} por debajo del umbral 85%");
    }

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Nutriologo.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new Exception("root not found");
    }
}
