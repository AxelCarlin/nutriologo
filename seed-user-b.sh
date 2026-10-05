#!/bin/bash
# seed-user-b.sh
# Siembra datos distintivos en usuario B para pruebas de privacidad
# Uso: ./seed-user-b.sh <TOKEN_B> [BASE_URL]

set -e

TOKEN_B=${1:-""}
BASE_URL=${2:-"http://localhost:5000"}

if [[ -z "$TOKEN_B" ]]; then
    echo "Uso: $0 <TOKEN_B> [BASE_URL]"
    exit 1
fi

API="$BASE_URL/api"
HDR=(-H "Authorization: Bearer $TOKEN_B" -H "Content-Type: application/json")

echo "=== Sembrando datos distintivos en Usuario B ==="

# 1. Peso distintivo: 113.7 kg
echo "1. Registrando peso: 113.7 kg"
curl -s -X POST "$API/mediciones" "${HDR[@]}" \
    -d '{"IdTipoMedicionCorporal":1,"Valor":113.7,"FechaMedicion":"'"$(date -u +%Y-%m-%dT%H:%M:%S)"'"}' | jq .

# 2. Meta calórica distintiva: 2847 kcal
# Primero necesitamos perfil y objetivo. Asumimos que ya existen.
# Si no, crear objetivo via /objetivo/calcular + POST a MetaCalorica (si existe endpoint)
# Por ahora, asumimos que el usuario B ya tiene objetivo calculado con meta 2847.
# Si no, se puede crear manualmente en BD o via endpoint si existe.

# 3. Comida distintiva hoy: "Pizza test B" ~800 kcal
echo "3. Registrando comida distintiva: Pizza test B"
HOY=$(date -u +%Y-%m-%dT%H:%M:%S)
curl -s -X POST "$API/comidas" "${HDR[@]}" \
    -d "{\"FechaConsumo\":\"$HOY\",\"Nombre\":\"Pizza test B\",\"Items\":[{\"nombreAlimento\":\"Pizza\",\"cantidad\":200,\"unidad\":\"G\"}]}" | jq .

echo ""
echo "=== Datos sembrados en Usuario B ==="
echo "  Peso: 113.7 kg"
echo "  Meta calórica: 2847 kcal (si existe objetivo)"
echo "  Comida: Pizza test B"
echo ""
echo "Ahora ejecuta: ./test-chat-suite.sh <TOKEN_A> $TOKEN_B"