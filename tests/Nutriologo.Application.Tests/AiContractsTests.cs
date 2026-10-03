namespace Nutriologo.Application.Tests;

using Nutriologo.Application.Abstractions.AI;
using Xunit;

public class AiContractsTests
{
    [Theory]
    [InlineData(0.95, ConfidenceLevel.Alta)]
    [InlineData(0.8, ConfidenceLevel.Alta)]
    [InlineData(0.6, ConfidenceLevel.Media)]
    [InlineData(0.5, ConfidenceLevel.Media)]
    [InlineData(0.3, ConfidenceLevel.Baja)]
    public void Confidence_ClasificaCorrectamente(decimal c, ConfidenceLevel esperado) =>
        Assert.Equal(esperado, ConfidenceClassifier.Classify(c, Array.Empty<string>()));

    [Fact]
    public void ConIncertidumbres_SiempreRequiereAclaracion() =>
        Assert.Equal(ConfidenceLevel.RequiereAclaracion,
            ConfidenceClassifier.Classify(0.99m, new[] { "uso de aceite?" }));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ConfianzaFueraDeRango_Lanza(decimal c) =>
        Assert.Throws<Domain.Exceptions.DomainException>(() => ConfidenceClassifier.Classify(c, Array.Empty<string>()));

    [Fact]
    public void SuficienteInformacion_NoPregunta()
    {
        var interp = new ChatInterpretation(AiIntent.RegistrarComida,
            new[] { new FoodCandidate("pollo", 100, "g", "a la plancha", 0.95m, Array.Empty<string>()) },
            Array.Empty<string>());
        Assert.False(ClarificationPolicy.Decide(interp).RequiereAclaracion);
    }

    [Fact]
    public void IntencionDesconocida_Pregunta() =>
        Assert.True(ClarificationPolicy.Decide(new ChatInterpretation(AiIntent.Desconocida, Array.Empty<FoodCandidate>(), Array.Empty<string>())).RequiereAclaracion);

    [Fact]
    public void BajaConfianza_Pregunta()
    {
        var interp = new ChatInterpretation(AiIntent.RegistrarComida,
            new[] { new FoodCandidate("taco", null, null, null, 0.3m, new[] { "cantidad?" }) },
            new[] { "cantidad?" });
        var d = ClarificationPolicy.Decide(interp);
        Assert.True(d.RequiereAclaracion);
        Assert.NotNull(d.Pregunta);
    }
}
