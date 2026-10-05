namespace Nutriologo.Application.Abstractions.AI;

/// <summary>
/// Contexto nutricional del usuario para el respondedor conversacional.
/// Lo construye el backend desde servicios internos; el modelo SOLO redacta.
/// </summary>
public record ContextoUsuario(
    PerfilResumen? Perfil,
    ObjetivoResumen? Objetivo,
    BalanceHoy? Hoy,
    PesoResumen? Peso
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