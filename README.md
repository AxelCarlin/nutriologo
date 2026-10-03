# Nutriólogo

Asistente conversacional de nutrición para adultos.

## Estructura

```
src/
  Nutriologo.Domain/
  Nutriologo.Application/
  Nutriologo.Infrastructure/
  Nutriologo.Api/
tests/
  Nutriologo.Domain.Tests/
  Nutriologo.Application.Tests/
  Nutriologo.Infrastructure.Tests/
docs/
scripts/
NutriologoDB.sql
```

## Requisitos

- .NET SDK 10.0.x (`~/.dotnet`)
- Docker + Docker Compose (PostgreSQL 17, contenedor `nutriologo-db`)
- Node.js LTS (Fase I, pendiente)
- Ollama (Fase G, pendiente)

## Base de datos

`NutriologoDB.sql` es la fuente de verdad de la persistencia. Levantar la base con:

```bash
docker compose up -d
```

Conexión: `Host=localhost;Port=5432;Database=nutriologo;Username=postgres;Password=Buss1ness.2026`

## Build y tests

```bash
dotnet build
dotnet test
```
