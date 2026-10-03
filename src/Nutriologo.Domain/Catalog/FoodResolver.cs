namespace Nutriologo.Domain.Catalog;

using System.Globalization;
using System.Text;

public static class FoodTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lower = text.Trim().ToLowerInvariant();
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var cleaned = sb.ToString().Normalize(NormalizationForm.FormC);
        while (cleaned.Contains("  ")) cleaned = cleaned.Replace("  ", " ");
        return cleaned.Trim();
    }

    /// <summary>Pluralización simple: quita 'es' si termina en consonante+es y longitud>4, si no quita 's'.</summary>
    public static string Singularize(string word)
    {
        if (word.Length > 4 && word.EndsWith("es")) return word[..^2];
        if (word.Length > 2 && word.EndsWith('s') && !word.EndsWith("ss")) return word[..^1];
        return word;
    }
}

public record FoodCatalogItem(
    int IdAlimento,
    string Nombre,
    string? Cocina = null,
    string? MetodoPreparacion = null,
    IReadOnlyList<string>? Sinonimos = null);

public record ResolvedFood(int IdAlimento, string Nombre, decimal Score, string Motivo);

public record FoodResolutionOutcome(IReadOnlyList<ResolvedFood> Candidatos, bool RequiereAclaracion, string Motivo);

public interface IFoodCatalogSource
{
    IReadOnlyList<FoodCatalogItem> Todos();
}

public static class DeterministicFoodResolver
{
    public const decimal ScoreExacto = 1.0m;
    public const decimal ScoreSinonimo = 0.9m;
    public const decimal ScoreNormalizado = 0.95m;
    public const decimal ScoreParcial = 0.65m;
    public const decimal UmbralResuelto = 0.85m;

    public static FoodResolutionOutcome Resolver(string texto, IFoodCatalogSource source,
        string? contextoPreparacion = null, string? contextoCocina = null)
    {
        if (source.Todos().Count == 0)
            return new(Array.Empty<ResolvedFood>(), true, "catálogo vacío");

        var norm = FoodTextNormalizer.Normalize(texto);
        if (norm.Length == 0) return new(Array.Empty<ResolvedFood>(), true, "texto vacío");
        var normSingular = string.Join(' ', norm.Split(' ').Select(FoodTextNormalizer.Singularize));

        var scored = new List<ResolvedFood>();
        foreach (var item in source.Todos())
        {
            var nombreNorm = FoodTextNormalizer.Normalize(item.Nombre);
            decimal? score = null;
            string motivo = "";

            if (string.Equals(nombreNorm, norm, StringComparison.Ordinal)) { score = ScoreExacto; motivo = "coincidencia exacta"; }
            else if (string.Equals(nombreNorm, normSingular, StringComparison.Ordinal)) { score = ScoreNormalizado; motivo = "normalización/singular"; }
            else if (item.Sinonimos is not null && item.Sinonimos.Any(s =>
                         string.Equals(FoodTextNormalizer.Normalize(s), norm, StringComparison.Ordinal) ||
                         string.Equals(FoodTextNormalizer.Normalize(s), normSingular, StringComparison.Ordinal)))
            { score = ScoreSinonimo; motivo = "sinónimo"; }
            else if (nombreNorm.Contains(norm, StringComparison.Ordinal) || norm.Contains(nombreNorm, StringComparison.Ordinal))
            { score = ScoreParcial; motivo = "coincidencia parcial"; }

            if (score is null) continue;

            // Penalización contextual: preparación/cocina explícita que no coincide.
            if (contextoPreparacion is not null && item.MetodoPreparacion is not null)
            {
                var ctx = FoodTextNormalizer.Normalize(contextoPreparacion);
                var prep = FoodTextNormalizer.Normalize(item.MetodoPreparacion);
                if (ctx.Contains(prep) || prep.Contains(ctx)) score += 0.15m;
                else score -= 0.25m;
            }
            if (contextoCocina is not null && item.Cocina is not null &&
                !FoodTextNormalizer.Normalize(item.Cocina).Contains(FoodTextNormalizer.Normalize(contextoCocina)))
                score -= 0.15m;

            scored.Add(new ResolvedFood(item.IdAlimento, item.Nombre, Math.Round(score.Value, 4), motivo));
        }

        var ordenados = scored.OrderByDescending(s => s.Score).ToList();
        if (ordenados.Count == 0) return new(Array.Empty<ResolvedFood>(), true, "alimento inexistente en catálogo");

        var top = ordenados[0];
        if (top.Score >= UmbralResuelto && (ordenados.Count == 1 || ordenados[1].Score < top.Score - 0.2m))
            return new(new[] { top }, false, "resuelto con alta confianza");

        return new(ordenados.Take(5).ToList(), true, ordenados.Count > 1 ? "candidato ambiguo: requiere aclaración" : "confianza insuficiente");
    }
}
