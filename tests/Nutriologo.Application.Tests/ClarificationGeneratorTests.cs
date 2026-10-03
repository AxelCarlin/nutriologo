namespace Nutriologo.Application.Tests;

using Nutriologo.Application.AI;
using Nutriologo.Application.Abstractions.AI;
using Xunit;

public class ClarificationGeneratorTests
{
    private static readonly ClarificationGenerator Gen = new();

    private static ChatInterpretation Interp(params FoodCandidate[] c) =>
        new(AiIntent.RegistrarComida, c, c.SelectMany(x => x.Incertidumbres).ToList());

    [Fact]
    public void Suficiente_NoPregunta()
    {
        var d = Gen.Decide(Interp(new FoodCandidate("pollo", 100, "g", "cocido", 0.95m, Array.Empty<string>())));
        Assert.False(d.RequiereAclaracion);
    }

    [Fact]
    public void CantidadDesconocida_PreguntaCantidad()
    {
        var d = Gen.Decide(Interp(new FoodCandidate("arroz", null, null, "hervido", 0.4m, new[] { "cantidad?" })));
        Assert.True(d.RequiereAclaracion);
        Assert.Contains("cantidad", d.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreparacionDesconocida_PreguntaPreparacion()
    {
        var d = Gen.Decide(Interp(new FoodCandidate("huevos", 2, "piezas", null, 0.4m, new[] { "prep?" })));
        Assert.True(d.RequiereAclaracion);
        Assert.Contains("prepar", d.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IncertidumbreDeclarada_Prioriza()
    {
        var d = Gen.Decide(Interp(new FoodCandidate("taco", 2, "piezas", "asado", 0.4m, new[] { "?era guisado?" })));
        Assert.True(d.RequiereAclaracion);
        Assert.Contains("guisado", d.Pregunta);
    }

    [Fact]
    public void SinCandidatos_PreguntaGeneral() =>
        Assert.True(Gen.Decide(new ChatInterpretation(AiIntent.RegistrarComida, Array.Empty<FoodCandidate>(), Array.Empty<string>())).RequiereAclaracion);

    [Fact]
    public void IntencionDesconocida_Pregunta() =>
        Assert.True(Gen.Decide(new ChatInterpretation(AiIntent.Desconocida, Array.Empty<FoodCandidate>(), Array.Empty<string>())).Pregunta!.Length > 0);
}
