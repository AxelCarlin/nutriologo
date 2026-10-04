namespace Nutriologo.Application.Tests;

using Nutriologo.Application.AI;
using Nutriologo.Application.Abstractions.AI;
using Xunit;

public class StructuredOutputValidatorTests
{
    [Fact]
    public void ParseaSalidaValida()
    {
        var raw = """{"intencion":"registrar_comida","requiere_aclaracion":false,"alimentos":[{"nombre":"huevo","cantidad":2,"unidad":"pieza","preparacion":null,"confianza":0.9,"incertidumbres":[]}]}""";
        Assert.True(StructuredOutputValidator.TryParse(raw, out var r, out _));
        Assert.Equal(AiIntent.RegistrarComida, r!.Intent);
        Assert.Single(r.Candidatos);
        Assert.Equal(2m, r.Candidatos[0].Cantidad);
    }

    [Fact]
    public void JsonMalformado_Falla() =>
        Assert.False(StructuredOutputValidator.TryParse("{no es json", out _, out var e) && e.Length > 0);

    [Theory]
    [InlineData("""{"intencion":"registrar_comida"}""")]                          // falta alimentos
    [InlineData("""{"alimentos":[]}""")]                                            // falta intencion
    [InlineData("""{"intencion":"x","requiere_aclaracion":"si","alimentos":[]}""")] // flag no bool
    [InlineData("""{"intencion":"x","requiere_aclaracion":false,"alimentos":[{"cantidad":2}]}""")] // sin nombre
    public void EsquemaIncompleto_Falla(string raw) =>
        Assert.False(StructuredOutputValidator.TryParse(raw, out _, out _));

    [Fact]
    public void CantidadInvalida_Falla() =>
        Assert.False(StructuredOutputValidator.TryParse(
            """{"intencion":"registrar_comida","requiere_aclaracion":false,"alimentos":[{"nombre":"a","cantidad":-5}]}""",
            out _, out _));

    [Fact]
    public void SinCandidatosPeroIntencionRegistrar_FuerzaAclaracion()
    {
        StructuredOutputValidator.TryParse(
            """{"intencion":"registrar_comida","requiere_aclaracion":false,"alimentos":[]}""",
            out var r, out _);
        Assert.Contains(r!.Candidatos, c => c.Incertidumbres.Count > 0);
    }

    [Fact]
    public void AlimentosNull_SeNormalizaAVacio()
    {
        var raw = """{"intencion":"desconocida","requiere_aclaracion":true,"alimentos":null}""";
        Assert.True(StructuredOutputValidator.TryParse(raw, out var r, out _));
        Assert.NotNull(r);
        Assert.Empty(r!.Candidatos);
    }

    [Fact]
    public void ConfianzaFueraDeRango_Falla() =>
        Assert.False(StructuredOutputValidator.TryParse(
            """{"intencion":"registrar_comida","requiere_aclaracion":false,"alimentos":[{"nombre":"a","confianza":1.5}]}""",
            out _, out _));
}
