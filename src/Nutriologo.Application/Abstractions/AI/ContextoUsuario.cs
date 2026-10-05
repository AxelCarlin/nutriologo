namespace Nutriologo.Application.Abstractions.AI;

/// <summary>
/// Contexto nutricional del usuario para el respondedor conversacional.
/// Lo construye el backend desde servicios internos; el modelo SOLO redacta.
/// <para><c>Planes</c> es lo único que se agrega del dominio de planes: es de
/// SOLO LECTURA. El bot analiza planes, no los crea ni los modifica.</para>
/// </summary>
public record ContextoUsuario(
    PerfilResumen? Perfil,
    ObjetivoResumen? Objetivo,
    BalanceHoy? Hoy,
    PesoResumen? Peso,
    PlanResumen[]? Planes = null
);

/// <summary>
/// Un plan alimentario del usuario, en forma plana: lo que el modelo puede leer
/// y comparar. Sin identificadores ni datos de otras personas.
/// </summary>
public record PlanResumen(
    string Nombre,
    string Estado,               // "Activo" | "Borrador" | "Archivado"
    int Dias,                    // cantidad de días definidos
    ComidaPlanResumen[] Comidas  // comidas del plan, todas
);

public record ComidaPlanResumen(
    string Momento,              // "Desayuno" | "Comida" | "Cena" | "Colación"
    string Alimento,
    decimal Cantidad,
    string Unidad
);

public record PerfilResumen(
    int Edad,
    string Sexo,           // "M" | "F"
    int AlturaCm,
    string Actividad       // "SEDENTARIO" | "LIGERO" | "MODERADO" | "ACTIVO" | "MUY_ACTIVO"
);

public record ObjetivoResumen(
    string Tipo,           // "Perder" | "Mantener" | "Ganar"
    int MetaKcal,
    decimal RitmoKgSem,    // ej: -0.5, 0, +0.3
    string Riesgo          // "Bajo" | "Moderado" | "Alto" | "Desconocido"
);

public record BalanceHoy(
    int KcalConsumidas,
    int? MetaKcal,         // null si no hay objetivo
    MacroResumen Macros,
    ComidaResumen[] Comidas
);

public record MacroResumen(
    int ProteinaG,
    int GrasaG,
    int CarbohidratoG
);

public record ComidaResumen(
    string Nombre,         // "Desayuno" | "Comida" | "Cena" | "Colación"
    int Kcal,
    string Hora            // "HH:mm"
);

public record PesoResumen(
    decimal UltimoKg,
    string Fecha,          // "YYYY-MM-DD"
    decimal? DeltaKg       // vs medición anterior, null si no hay
);