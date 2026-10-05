namespace Nutriologo.Domain.Tests;

using Nutriologo.Domain.Consumption;
using Xunit;

/// <summary>
/// Estos tests fijan el comportamiento que el LLM no puede dar: resolver
/// "sí, la grande" sin volver a preguntárselo al modelo. Si alguno falla,
/// la coreferencia del chat volvió a depender del modelo.
/// </summary>
public class MealSlotTrackerTests
{
    private static ComidaEnCurso FormCon(params (string nombre, decimal? cantidad, string? unidad)[] items) =>
        MealSlotTracker.Abrir(items);

    // ---------- Apertura ----------

    [Fact]
    public void Abrir_ConTodoCompleto_QuedaCompleto()
    {
        var e = FormCon(("Huevo", 100m, "G"));
        Assert.Equal(EstadoSlot.Completo, e.Estado);
        Assert.Single(MealSlotTracker.Listos(e));
    }

    [Fact]
    public void Abrir_SinCantidad_QuedaEsperandoCantidad()
    {
        var e = FormCon(("Tortilla", null, null));
        Assert.Equal(EstadoSlot.EsperandoCantidad, e.Estado);
        Assert.Equal(SlotAlimento.Cantidad, e.Pendiente!.SlotPendiente);
        Assert.Empty(MealSlotTracker.Listos(e));
    }

    [Fact]
    public void PreguntaPara_NombraElSlotExacto_NoUnErrorGenerico()
    {
        var e = FormCon(("Tortilla", null, null));
        var p = MealSlotTracker.PreguntaPara(e);
        Assert.Equal("¿Cuántas tortilla?", p);
        Assert.DoesNotContain("No pude", p, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Abrir_VariosItems_IncluyeLosPendientes()
    {
        var e = FormCon(("Huevo", 2m, "PIEZA"), ("Tortilla", null, null));
        Assert.Equal(2, e.Items.Count);
        Assert.Equal("Tortilla", e.Pendiente!.Nombre);
    }

    // ---------- El bug reportado: coreferencia ----------

    [Fact]
    public void PatchDeTamano_ResuelveLaGrande_SinLLM()
    {
        // El caso exacto que fallaba: "sí, la grande" con tortilla pendiente.
        var e = FormCon(("Tortilla", null, null));
        var r = MealSlotTracker.Avanzar(e, "sí, la grande");

        Assert.True(r.Reconocido);
        Assert.Equal(1.0m, e.Pendiente!.Cantidad);
        Assert.True(e.Pendiente.ConfirmadoPorTracker);
    }

    [Theory]
    [InlineData("la grande", 1.0)]
    [InlineData("chica", 0.5)]
    [InlineData("mediana", 0.75)]
    [InlineData("doble", 2.0)]
    [InlineData("mitad", 0.5)]
    public void PatchDeTamano_CubreVariantes(string texto, decimal esperado)
    {
        Assert.Equal(esperado, Cuantificador.PatchDeTamano(texto));
    }

    [Fact]
    public void PatchDeTamano_NoInventaCuandoNoHayPatches()
    {
        Assert.Null(Cuantificador.PatchDeTamano("quiero una pizza"));
    }

    // ---------- Cantidad con unidad ----------

    [Theory]
    [InlineData("50ml", 50, "ML")]
    [InlineData("2 piezas", 2, "PIEZA")]
    [InlineData("3 tazas", 3, "TAZA")]
    [InlineData("100 g", 100, "G")]
    [InlineData("1,5 kg", 1.5, "KG")]
    public void ExtraerCantidadUnid_ResuelveElPar(string texto, double cantidad, string unidad)
    {
        var r = Cuantificador.ExtraerCantidadUnidad(texto);
        Assert.NotNull(r);
        Assert.Equal((decimal)cantidad, r!.Value.Cantidad);
        Assert.Equal(unidad, r.Value.Unidad);
    }

    [Fact]
    public void NumeroSolo_ResuelveCantidadSinUnidad()
    {
        var r = Cuantificador.ExtraerCantidadUnidad("dos");
        Assert.Null(r); // "dos" en palabras no se parsea: requiere LLM o tracker
    }

    [Fact]
    public void Avanzar_CantidadYUnidad_EnUnSoloMensaje()
    {
        var e = FormCon(("Tortilla", null, null));
        MealSlotTracker.Avanzar(e, "50 ml");
        // Pendiente() es null porque el form ya está Completo: se mira el item.
        Assert.Equal(50m, e.Items[0].Cantidad);
        Assert.Equal("ML", e.Items[0].Unidad);
        Assert.Equal(EstadoSlot.Completo, e.Estado);
    }

    [Fact]
    public void UnidadNoSeAdivina_SePregunta()
    {
        // "3" puede ser 3 piezas o 3 gramos. Asumir gramos daría calorías
        // erradas en silencio, que es peor que preguntar.
        var e = FormCon(("Tortilla", null, null));
        MealSlotTracker.Avanzar(e, "3");
        Assert.Equal(EstadoSlot.EsperandoUnidad, e.Estado);
        Assert.Equal("¿En qué unidad, tortilla?", MealSlotTracker.PreguntaPara(e));
    }

    [Fact]
    public void Avanzar_SoloCantidad_DejaPendienteLaUnidad()
    {
        var e = FormCon(("Tortilla", null, null));
        MealSlotTracker.Avanzar(e, "3");
        Assert.Equal(3m, e.Pendiente!.Cantidad);
        Assert.Equal(SlotAlimento.Unidad, e.Pendiente.SlotPendiente);
    }

    // ---------- Confirmación ----------

    [Theory]
    [InlineData("sí")]
    [InlineData("dale")]
    [InlineData("ok")]
    [InlineData("correcto")]
    [InlineData("esa misma")]
    public void Confirmacion_NoInventaDatos_PeroEsReconocida(string texto)
    {
        var e = FormCon(("Tortilla", null, null));
        var r = MealSlotTracker.Avanzar(e, texto);
        Assert.True(r.Reconocido);
        // No debe rellenar la cantidad con una suposición.
        Assert.Null(e.Pendiente!.Cantidad);
    }

    // ---------- Cancelación ----------

    [Theory]
    [InlineData("cancela")]
    [InlineData("olvídalo")]
    [InlineData("olvidalo")]
    public void Cancelacion_LimpiaElForm(string texto)
    {
        var e = FormCon(("Tortilla", 3m, "PIEZA"));
        MealSlotTracker.Avanzar(e, texto);
        Assert.Empty(e.Items);
        Assert.False(e.Activa);
    }

    // ---------- Mensajes no relacionados ----------

    [Fact]
    public void MensajeLargo_NoAbreForm_NotReconocido()
    {
        var e = FormCon(("Tortilla", null, null));
        var r = MealSlotTracker.Avanzar(e, "quiero saber las calorías de mañana");
        Assert.False(r.Reconocido);
    }

    [Fact]
    public void TextoNoRelacionado_NoEnvenenaElForm()
    {
        var e = FormCon(("Tortilla", null, null));
        MealSlotTracker.Avanzar(e, "xyzabc");
        Assert.Equal("Tortilla", e.Items[0].Nombre);
        Assert.Null(e.Items[0].Cantidad);
    }

    [Fact]
    public void SinFormActivo_TodoEsNoReconocido()
    {
        var e = new ComidaEnCurso();
        Assert.False(MealSlotTracker.Avanzar(e, "sí").Reconocido);
    }

    // ---------- Clasificación de mensajes ----------

    [Theory]
    [InlineData("sí")]
    [InlineData("dale")]
    [InlineData("50ml")]
    [InlineData("cancela")]
    public void RespuestaCorta_EsRespuestaCorta(string t) =>
        Assert.True(Cuantificador.EsRespuestaCorta(t));

    [Theory]
    [InlineData("Comí dos huevos y dos tortillas")]
    [InlineData("¿cuántas calorías llevo hoy?")]
    public void FraseCompleta_NoEsRespuestaCorta(string t) =>
        Assert.False(Cuantificador.EsRespuestaCorta(t));

    // ---------- Persistencia del estado ----------

    [Fact]
    public void MultiTurno_AcumulaVariosItemsAntesDePersistir()
    {
        // Turno 1: el modelo propone dos alimentos. Tortilla viene SIN cantidad
        // pero CON unidad: la porción natural del catálogo (1 PIEZA).
        var e = FormCon(("Huevo", 2m, "PIEZA"), ("Tortilla", null, "PIEZA"));
        Assert.Equal(EstadoSlot.EsperandoCantidad, e.Estado);

        // Turno 2: el usuario responde con un patch de tamaño.
        MealSlotTracker.Avanzar(e, "la grande");

        Assert.Equal(EstadoSlot.Completo, e.Estado);
        var listos = MealSlotTracker.Listos(e);
        Assert.Equal(2, listos.Count);
        Assert.Contains(listos, i => i.Nombre == "Huevo" && i.Cantidad == 2m);
        Assert.Contains(listos, i => i.Nombre == "Tortilla" && i.Cantidad == 1.0m && i.Unidad == "PIEZA");
    }

    [Fact]
    public void FormCompleto_NoSePersisteAntesDeTiempo()
    {
        var e = FormCon(("Huevo", 2m, "PIEZA"), ("Tortilla", null, null));
        Assert.Empty(MealSlotTracker.Listos(e)); // todavía falta tortilla
    }
}