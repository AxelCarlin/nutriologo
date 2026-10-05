namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Consumption;
using Xunit;

/// <summary>
/// Baja lógica: el camino documentado para corregir un registro erróneo
/// (p.ej. 100 PIEZA en vez de 100 g). El contenido nunca se sobrescribe.
/// </summary>
public class BajaLogicaTests
{
    [Fact]
    public void DarDeBaja_PoneEstatusCero_YAudita()
    {
        var c = new RegistroComida(1, new DateTime(2026, 10, 4, 12, 0, 0), "Desayuno");
        Assert.True(c.Activa);

        c.DarDeBaja(7);

        Assert.False(c.Activa);
        Assert.Equal(0, c.Estatus);
        Assert.Equal(7, c.IdUsuarioModifica);
    }

    [Fact]
    public void DarDeBaja_EsIdempotente()
    {
        var c = new RegistroComida(1, new DateTime(2026, 10, 4, 12, 0, 0), "Desayuno");
        c.DarDeBaja(7);
        var ex = Record.Exception(() => c.DarDeBaja(7));
        Assert.Null(ex);
        Assert.False(c.Activa);
    }

    [Fact]
    public void Item_DarDeBaja_EnCascadaConElPadre()
    {
        var item = new AlimentoRegistroComida(10, 4, 2, 100m, 100m, 155m);
        Assert.True(item.Activa);

        item.DarDeBaja(7);

        Assert.False(item.Activa);
        Assert.Equal(0, item.Estatus);
        // El contenido histórico no se toca: solo se excluye de lecturas.
        Assert.Equal(100m, item.Cantidad);
        Assert.Equal(155m, item.CaloriasEstimadas);
    }
}