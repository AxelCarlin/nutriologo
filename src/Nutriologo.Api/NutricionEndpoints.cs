using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nutriologo.Api.Auth;
using Nutriologo.Application.Meals;
using Nutriologo.Domain.Catalog;
using Nutriologo.Domain.Consumption;
using Nutriologo.Domain.Entities;
using Nutriologo.Domain.Exceptions;
using Nutriologo.Domain.Nutrition;
using Nutriologo.Infrastructure.Persistence;

namespace Nutriologo.Api;

public static class NutricionEndpoints
{
    public static void MapNutricion(this WebApplication app)
    {
        var g = app.MapGroup("/api").RequireRateLimiting("business").RequireAuthorization();

        g.MapPost("/perfil", async (PerfilRequest r, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var p = new PerfilUsuario(idUsuario, r.IdSexoBiologico, r.IdNivelActividadFisica,
                r.FechaNacimiento, r.Altura, r.AlturaUnidad ?? "cm", DateTime.UtcNow, idUsuario);
            db.PerfilesUsuario.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/perfil/{p.Id}", new { p.Id });
        });

        g.MapPost("/mediciones", async (MedicionRequest r, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var m = new MedicionCorporal(idUsuario, r.IdTipoMedicionCorporal, r.Valor, r.FechaMedicion, r.Observacion, idUsuario);
            db.MedicionesCorporales.Add(m);
            await db.SaveChangesAsync();
            return Results.Created($"/api/mediciones/{m.Id}", new { m.Id });
        });

        // GET /alimentos — catálogo con energía por 100 g y la porción declarada.
        // La UI necesita esto para mostrar kcal en vivo por fila ANTES de enviar;
        // sin el no se puede evitar que el usuario registre a ciegas.
        g.MapGet("/alimentos", async (AppDbContext db) =>
        {
            var filas = await db.Database.SqlQueryRaw<AlimentoCatalogoDto>(
                """
                SELECT a."Nombre"                                    AS "Nombre",
                       an."Cantidad"                                AS "KcalPorBase",
                       bn."CantidadBase"                            AS "Base",
                       COALESCE(ub."Codigo", 'G')                   AS "BaseUnidad",
                       p."Descripcion"                              AS "PorcionDescripcion",
                       p."Cantidad"                                 AS "PorcionCantidad",
                       p."CantidadGramos"                           AS "PorcionGramos",
                       COALESCE(up."Codigo", 'G')                   AS "PorcionUnidad"
                FROM "Alimento" a
                LEFT JOIN "AlimentoFuente" af ON af."IdAlimento" = a."Id"
                LEFT JOIN "BaseNutricionalAlimento" bn ON bn."IdAlimentoFuente" = af."Id"
                LEFT JOIN "UnidadMedida" ub ON ub."Id" = bn."IdUnidadMedida"
                LEFT JOIN "AlimentoNutriente" an ON an."IdAlimentoFuente" = af."Id"
                     AND an."IdNutriente" = (SELECT "Id" FROM "Nutriente" WHERE "Codigo" = 'ENERGIA')
                LEFT JOIN "PorcionAlimento" p ON p."IdAlimento" = a."Id"
                LEFT JOIN "UnidadMedida" up ON up."Id" = p."IdUnidadMedida"
                GROUP BY a."Id", a."Nombre", an."Cantidad", bn."CantidadBase", ub."Codigo",
                         p."Descripcion", p."Cantidad", p."CantidadGramos", up."Codigo"
                ORDER BY a."Nombre"
                """).ToListAsync();

            // Varias filas por alimento si tiene más de una porción: se conserva
            // la primera (porción principal) para el preview del cliente.
            return filas
                .GroupBy(f => f.Nombre)
                .Select(g => new
                {
                    nombre = g.Key,
                    kcalPorBase = g.First().KcalPorBase,
                    baseGramos = g.First().Base,
                    baseUnidad = g.First().BaseUnidad,
                    porcion = g.First().PorcionDescripcion is null ? null : new
                    {
                        descripcion = g.First().PorcionDescripcion,
                        cantidad = g.First().PorcionCantidad,
                        gramos = g.First().PorcionGramos,
                        unidad = g.First().PorcionUnidad,
                    },
                })
                .ToList();
        });

        // Unidades disponibles, para el selector (no texto libre).
        g.MapGet("/unidades", async (AppDbContext db) =>
            await db.UnidadesMedida.AsNoTracking()
                .OrderBy(u => u.IdTipoUnidadMedida).ThenBy(u => u.Codigo)
                .Select(u => new { codigo = u.Codigo, simbolo = u.Simbolo })
                .ToListAsync());

        // GET /catalogos — todo lo que la UI necesita para no exponer IDs.
        // Tres pantallas (Perfil, Objetivo, Mediciones) lo consumen; el factor
        // 1.55 estaba duplicado en Objetivo y hardcodeado.
        g.MapGet("/catalogos", async (AppDbContext db) =>
        {
            var sexos = await db.Database.SqlQueryRaw<IdNombreDto>(
                """
                SELECT "Id", "Descripcion" AS "Nombre"
                FROM "SexoBiologico"
                WHERE "Estatus" = 1
                ORDER BY "Id"
                """).ToListAsync();

            // ORDER BY "Orden", no "Id": el Id es arbitrario y el orden natural
            // va de sedentar a muy activo (SEDENTARIO=1, MUY_ACTIVO=5).
            var actividad = await db.Database.SqlQueryRaw<NivelActividadFullDto>(
                """
                SELECT "Id", "Codigo", "Descripcion", "FactorActividad"
                FROM "NivelActividadFisica"
                WHERE "Estatus" = 1
                ORDER BY "Orden"
                """).ToListAsync();

            var tiposMedicion = await db.Database.SqlQueryRaw<TipoMedicionDto>(
                """
                SELECT t."Id", t."Codigo", t."Descripcion", u."Simbolo"
                FROM "TipoMedicionCorporal" t
                LEFT JOIN "UnidadMedida" u ON u."Id" = t."IdUnidadMedida"
                WHERE t."Estatus" = 1
                ORDER BY t."Id"
                """).ToListAsync();

            return Results.Ok(new
            {
                sexos = sexos.Select(s => new { s.Id, s.Nombre }),
                actividad = actividad.Select(a => new
                {
                    a.Id,
                    a.Codigo,
                    nombre = a.Descripcion,
                    factor = a.FactorActividad,
                    // Descripción en lenguaje natural: reduce la fricción de un
                    // dato de salud sin explicar la matemática del factor.
                    descripcion = DescribirActividad(a.Codigo, a.FactorActividad),
                }),
                tiposMedicion = tiposMedicion.Select(m =>
                {
                    var (min, max) = RangoPorCodigo(m.Codigo);
                    return new { m.Id, m.Codigo, nombre = m.Descripcion, unidad = m.Simbolo ?? "", min, max };
                }),
            });
        });

        // GET /perfil — lectura del perfil propio. Objetivo lo necesita para
        // precargar los datos en vez de volver a pedirlos.
        g.MapGet("/perfil", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var p = await db.PerfilesUsuario.AsNoTracking()
                .Where(x => x.IdUsuario == idUsuario)
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.IdSexoBiologico,
                    x.IdNivelActividadFisica,
                    x.FechaNacimiento,
                    x.Altura,
                    x.AlturaUnidad,
                })
                .FirstOrDefaultAsync();
            return Results.Ok(p);
        });

        g.MapPost("/comidas", async (ComidaRequest r, ClaimsPrincipal user, AppDbContext db, IFoodCatalogSource catalog) =>
        {
            var result = await MealRegistrar.RegisterAsync(db, catalog, JwtIssuer.UserId(user), r.FechaConsumo, r.Nombre, r.Items);
            return result.Ok ? Results.Created($"/api/comidas/{result.Id}", new { id = result.Id })
                : Results.BadRequest(new { result.RequiereAclaracion, result.Mensaje });
        });

        g.MapGet("/comidas", async (DateTime? fecha, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var q = db.RegistrosComida.Where(x => x.IdUsuario == idUsuario && x.Estatus == 1);
            if (fecha is not null) q = q.Where(x => x.FechaConsumo.Date == fecha.Value.Date);
            var items = await q.OrderByDescending(x => x.FechaConsumo)
                .Select(x => new { x.Id, x.FechaConsumo, x.Nombre, x.CaloriasEstimadas })
                .ToListAsync();
            return Results.Ok(items);
        });

        // DELETE /comidas/{id} — baja lógica (Estatus = 0), nunca borrado físico.
        // El contenido histórico no se toca: solo se excluye de las lecturas.
        // 404 tanto si no existe como si es de otro usuario o ya está dada de
        // baja (no se filtra información ajena). Idempotente por diseño.
        g.MapDelete("/comidas/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var comida = await db.RegistrosComida
                .FirstOrDefaultAsync(x => x.Id == id && x.IdUsuario == idUsuario);
            if (comida is null || comida.Estatus != 1) return Results.NotFound();

            comida.DarDeBaja(idUsuario);
            var items = await db.AlimentosRegistroComida
                .Where(x => x.IdRegistroComida == id && x.Estatus == 1)
                .ToListAsync();
            foreach (var it in items) it.DarDeBaja(idUsuario);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/balance", async (DateTime fecha, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var consumidas = await db.RegistrosComida
                .Where(x => x.IdUsuario == idUsuario && x.Estatus == 1 && x.FechaConsumo.Date == fecha.Date)
                .SumAsync(x => (decimal?)x.CaloriasEstimadas) ?? 0m;
            return Results.Ok(new { fecha = fecha.Date, caloriasConsumidas = consumidas });
        });

        // GET /panel — agregado de UNA llamada para la pantalla de Inicio.
        // Antes el frontend encadenaba /balance + /perfil + /objetivo/calcular
        // (3 round-trips, y /perfil no existia como GET: 404 garantizado).
        //
        // Nota de esquema: TipoComida, TipoMedicionCorporal y MetaCalorica NO
        // estan mapeadas como entidades EF (ver AppDbContext), asi que se leen
        // por SQL crudo. Agregarlas al DbContext es el fix de raiz, pero excede
        // el alcance de este cambio.
        g.MapGet("/panel", async (DateTime? fecha, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            // Npgsql rechaza DateTime Unspecified como parámetro ("Cannot write
            // DateTime with Kind=Unspecified"). El binding de ?fecha= lo deja
            // Unspecified; se normaliza a UTC. La comparación es por fecha,
            // así que el Kind no cambia la semántica.
            var dia = DateTime.SpecifyKind((fecha ?? DateTime.UtcNow).Date, DateTimeKind.Utc);

            var comidasHoy = await db.RegistrosComida
                .Where(x => x.IdUsuario == idUsuario && x.Estatus == 1 && x.FechaConsumo.Date == dia)
                .OrderBy(x => x.FechaConsumo)
                .Select(x => new { x.Id, x.FechaConsumo, x.Nombre, x.CaloriasEstimadas, x.IdTipoComida })
                .ToListAsync();

            var consumidas = comidasHoy.Sum(c => c.CaloriasEstimadas ?? 0m);

            // Tipos de comida por Id (tabla no mapeada).
            var tiposComida = await db.Database.SqlQueryRaw<IdNombreDto>(
                """SELECT "Id", "Codigo" AS "Nombre" FROM "TipoComida" """).ToListAsync();
            var tipoPorId = tiposComida.ToDictionary(t => t.Id, t => t.Nombre);

            // Macros del dia, escalados con la misma base que usa MealRegistrar
            // (CantidadBase). Sin esto el panel solo muestra kcal y desperdicia
            // los 40 registros de AlimentoNutriente que si existen.
            var filasMacros = await db.Database.SqlQueryRaw<MacroFilaDto>(
                """
                SELECT n."Codigo"                       AS "Codigo",
                       an."Cantidad"                   AS "PorBase",
                       -- CantidadGramos: MealRegistrar ya normaliza PIEZA/ML a gramos.
                       -- Usar arc."Cantidad" daba "2 PIEZA" = 2 g, macros ~50x bajos.
                       COALESCE(arc."CantidadGramos", arc."Cantidad") AS "Cantidad",
                       bn."CantidadBase"               AS "BaseGramos"
                FROM "AlimentoRegistroComida" arc
                JOIN "RegistroComida" rc        ON rc."Id" = arc."IdRegistroComida"
                JOIN "AlimentoFuente" af        ON af."IdAlimento" = arc."IdAlimento"
                JOIN "BaseNutricionalAlimento" bn ON bn."IdAlimentoFuente" = af."Id"
                JOIN "AlimentoNutriente" an      ON an."IdAlimentoFuente" = af."Id"
                JOIN "Nutriente" n               ON n."Id" = an."IdNutriente"
                WHERE rc."IdUsuario" = {0}
                  AND rc."FechaConsumo"::date = {1}::date
                  AND rc."Estatus" = 1 AND arc."Estatus" = 1
                  AND n."Codigo" IN ('PROTEINA', 'GRASA', 'CARBOHIDRATO')
                  AND bn."CantidadBase" > 0
                """, idUsuario, dia).ToListAsync();

            var macros = filasMacros
                .GroupBy(f => f.Codigo)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round(g.Sum(f => f.PorBase * f.Cantidad / f.BaseGramos), 1));

            // Ultima medicion de peso. TipoMedicionCorporal esta vacia hoy, asi que
            // este bloque devuelve null hasta que se siembre el catalogo.
            var peso = (await db.Database.SqlQueryRaw<DecimalFechadoDto>(
                """
                SELECT m."Valor", m."FechaMedicion"
                FROM "MedicionCorporal" m
                JOIN "TipoMedicionCorporal" t ON t."Id" = m."IdTipoMedicionCorporal"
                WHERE m."IdUsuario" = {0} AND (t."Codigo" = 'PESO' OR t."Codigo" = 'PESO_KG')
                ORDER BY m."FechaMedicion" DESC
                LIMIT 1
                """, idUsuario).ToListAsync()).FirstOrDefault();

            // Serie de 7 dias para la barra segmentada (incluye dias sin comida).
            var desde = dia.AddDays(-6);
            var porDia = await db.RegistrosComida
                .Where(x => x.IdUsuario == idUsuario && x.Estatus == 1 && x.FechaConsumo.Date >= desde && x.FechaConsumo.Date <= dia)
                .GroupBy(x => x.FechaConsumo.Date)
                .Select(g => new { dia = g.Key, kcal = g.Sum(x => x.CaloriasEstimadas) ?? 0m })
                .ToListAsync();
            var serie = Enumerable.Range(0, 7)
                .Select(i => new { dia = desde.AddDays(i), kcal = Math.Round(porDia.FirstOrDefault(p => p.dia == desde.AddDays(i))?.kcal ?? 0m, 0) })
                .ToList();

            // Meta vigente. MetaCalorica esta vacia: null => el frontend NO inventa
            // un denominador, muestra el dato sin barra de progreso.
            var meta = (await db.Database.SqlQueryRaw<DecimalFechadoDto>(
                """
                SELECT "CaloriasObjetivo" AS "Valor", "FechaInicio" AS "FechaMedicion"
                FROM "MetaCalorica"
                WHERE "IdUsuario" = {0} AND ("FechaFin" IS NULL OR "FechaFin" >= {1})
                ORDER BY "FechaInicio" DESC
                LIMIT 1
                """, idUsuario, dia).ToListAsync()).FirstOrDefault();

            var tienePerfil = await db.PerfilesUsuario.AnyAsync(p => p.IdUsuario == idUsuario);

            // Contenido de cada comida: qué alimentos la componen, con cantidad
            // y kcal. Sin esto la lista de comidas es opaca ("Desayuno 420 kcal"
            // sin decir qué se comió). Una sola query para todo el día.
            var filasItems = await db.Database.SqlQueryRaw<ItemComidaDto>(
                """
                SELECT arc."IdRegistroComida" AS "IdComida",
                       a."Nombre"             AS "Alimento",
                       arc."Cantidad"         AS "Cantidad",
                       u."Codigo"            AS "Unidad",
                       arc."CantidadGramos"   AS "Gramos",
                       arc."CaloriasEstimadas" AS "Kcal"
                FROM "AlimentoRegistroComida" arc
                JOIN "RegistroComida" rc ON rc."Id" = arc."IdRegistroComida"
                JOIN "Alimento" a        ON a."Id" = arc."IdAlimento"
                LEFT JOIN "UnidadMedida" u ON u."Id" = arc."IdUnidadMedida"
                WHERE rc."IdUsuario" = {0}
                  AND rc."FechaConsumo"::date = {1}::date
                  AND rc."Estatus" = 1 AND arc."Estatus" = 1
                ORDER BY arc."IdRegistroComida", arc."Id"
                """, idUsuario, dia).ToListAsync();

            var itemsPorComida = filasItems
                .GroupBy(f => f.IdComida)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<object>)g.Select(f => new
                    {
                        alimento = f.Alimento,
                        cantidad = f.Cantidad,
                        unidad = f.Unidad,
                        gramos = f.Gramos,
                        kcal = f.Kcal is null ? (decimal?)null : Math.Round(f.Kcal.Value, 0),
                    }).ToList());

            return Results.Ok(new
            {
                fecha = dia,
                caloriasConsumidas = Math.Round(consumidas, 0),
                metaCalorica = meta?.Valor,
                tieneObjetivo = meta is not null,
                tienePerfil,
                peso = peso?.Valor,
                fechaPeso = peso?.FechaMedicion,
                macros,
                comidas = comidasHoy.Select(c => new
                {
                    c.Id,
                    c.Nombre,
                    c.CaloriasEstimadas,
                    tipo = c.IdTipoComida != null && tipoPorId.TryGetValue(c.IdTipoComida.Value, out var tc) ? tc : null,
                    hora = c.FechaConsumo,
                    items = itemsPorComida.TryGetValue(c.Id, out var items) ? items : Array.Empty<object>(),
                }),
                serie,
            });
        });

        g.MapPost("/objetivo/calcular", async (ObjetivoRequest r, AppDbContext db) =>
        {
            NutritionSafetyGuard.EnsureAllowed(r.TipoObjetivo, r.EdadAnios, r.Estado);
            ICalorieCalculator calc = new MifflinStJeorCalorieCalculator();
            var est = calc.Estimate(new CalorieInput(r.PesoKg, r.AlturaCm, r.EdadAnios, r.Sexo, r.FactorActividad,
                r.TipoObjetivo, r.PesoObjetivoKg, r.SemanasObjetivo));

            // Reglas configurables vienen de DB (Infrastructure → Domain evaluators).
            var reglasRitmo = await db.ReglaRiesgoObjetivos
                .Select(x => new ReglaRiesgoObjetivoDef(x.Codigo, x.RitmoMinimo, x.RitmoMaximo, x.Orden, x.IdNivelRiesgo, x.Descripcion))
                .ToListAsync();
            var reglasMeta = await db.ReglaRiesgoMetasCaloricas
                .Select(x => new ReglaRiesgoMetaCaloricaDef(x.Codigo, x.CaloriasMinimas, x.DeficitMaximo, x.Orden, x.IdNivelRiesgo, x.Descripcion))
                .ToListAsync();

            object? evaluacionObjetivo = null, evaluacionMeta = null;
            if (est.RitmoSemanalKg is not null && reglasRitmo.Count > 0)
            {
                var e = RiesgoEvaluator.EvaluarRitmo(est.RitmoSemanalKg.Value, reglasRitmo);
                evaluacionObjetivo = new { e.ReglaAplicada.Codigo, e.ReglaAplicada.Descripcion, e.ReglaAplicada.IdNivelRiesgo };
            }
            if (est.MetaCaloricaEstimada is not null && reglasMeta.Count > 0)
            {
                var e = RiesgoEvaluator.EvaluarMetaProporcionada(est.MetaCaloricaEstimada.Value, est.GastoEnergeticoTotal, reglasMeta);
                evaluacionMeta = new { e.ReglaAplicada.Codigo, e.ReglaAplicada.Descripcion, e.ReglaAplicada.IdNivelRiesgo, e.DeficitCalculado, e.PorcentajeDeficit };
            }

            return Results.Ok(new
            {
                est.MetabolismoBasal,
                est.GastoEnergeticoTotal,
                est.DeficitDiarioRequerido,
                est.MetaCaloricaEstimada,
                est.RitmoSemanalKg,
                est.PorcentajeCambio,
                evaluacionObjetivo,
                evaluacionMetaCalorica = evaluacionMeta,
            });
        });

        // GET /mediciones?tipo=&desde= — historial filtrado.
        // Antes devolvía TODO sin filtro y el front no tenía forma de pedir
        // "solo peso de los últimos 90 días", que es lo que pide una gráfica.
        g.MapGet("/mediciones", async (int? tipo, DateTime? desde, ClaimsPrincipal user, AppDbContext db) =>
        {
            var idUsuario = JwtIssuer.UserId(user);
            var q = db.MedicionesCorporales.Where(x => x.IdUsuario == idUsuario);
            if (tipo is not null) q = q.Where(x => x.IdTipoMedicionCorporal == tipo);
            if (desde is not null) q = q.Where(x => x.FechaMedicion >= desde);

            var items = await q
                .OrderByDescending(x => x.FechaMedicion)
                .Select(x => new { x.Id, x.IdTipoMedicionCorporal, x.Valor, x.FechaMedicion, x.Observacion })
                .ToListAsync();
            return Results.Ok(items);
        });

    }

    public record ComidaRequest(DateTime FechaConsumo, string? Nombre, IReadOnlyList<MealItemInput> Items);
    public record PerfilRequest(int IdSexoBiologico, int IdNivelActividadFisica, DateTime FechaNacimiento, decimal Altura, string? AlturaUnidad);
    public record MedicionRequest(int IdTipoMedicionCorporal, decimal Valor, DateTime FechaMedicion, string? Observacion);
    public record ObjetivoRequest(decimal PesoKg, decimal AlturaCm, int EdadAnios, SexoBiologico Sexo, decimal FactorActividad, TipoObjetivo TipoObjetivo, decimal? PesoObjetivoKg, decimal? SemanasObjetivo, EstadoFisiologicoCodigo Estado = EstadoFisiologicoCodigo.Ninguno);

    // DTOs para tablas leidas por SQL crudo (sin entidad EF mapeada).
    public record IdNombreDto(int Id, string Nombre);
    public record DecimalFechadoDto(decimal? Valor, DateTime? FechaMedicion);
    public record MacroFilaDto(string Codigo, decimal PorBase, decimal Cantidad, decimal BaseGramos);
    public record ItemComidaDto(int IdComida, string Alimento, decimal Cantidad, string? Unidad,
        decimal? Gramos, decimal? Kcal);
    public record AlimentoCatalogoDto(
        string Nombre, decimal? KcalPorBase, decimal? Base, string? BaseUnidad,
        string? PorcionDescripcion, decimal? PorcionCantidad, decimal? PorcionGramos, string? PorcionUnidad);
    public record NivelActividadDto(string Descripcion, decimal FactorActividad);
    public record NivelActividadFullDto(int Id, string Codigo, string Descripcion, decimal FactorActividad);
    public record TipoMedicionDto(int Id, string Codigo, string Descripcion, string? Simbolo);

    /// <summary>
    /// Traduce el factor a algo accionable. "1.55" no le dice nada a nadie;
    /// "3–5 días de ejercicio por semana" sí.
    /// </summary>
    private static string DescribirActividad(string codigo, decimal factor) => codigo switch
    {
        "SEDENTARIO" => "Poco o nada de ejercicio",
        "LIGERO" => "1–2 días de ejercicio por semana",
        "MODERADO" => "3–5 días de ejercicio por semana",
        "ACTIVO" => "6–7 días de ejercicio por semana",
        "MUY_ACTIVO" => "Ejercicio intenso diario o trabajo físico",
        _ => $"Factor {factor:0.00}",
    };

    /// <summary>
    /// Rango plausible por tipo de medición. Política de la API: el schema
    /// TipoMedicionCorporal no tiene columnas min/max, así que no hay dónde
    /// persistirlos sin una migración.
    /// </summary>
    private static (decimal min, decimal max) RangoPorCodigo(string codigo) => codigo switch
    {
        "PESO" => (20m, 400m),
        "CINTURA" => (30m, 250m),
        "CADERA" => (40m, 250m),
        "CUAJAS" => (30m, 250m),
        "GRASA_PORCENTUAL" => (2m, 70m),
        _ => (0m, 100000m),
    };
}
