#!/usr/bin/env bash
# Backup reproducible de PostgreSQL (Nutriólogo).
# Uso: scripts/backup.sh [directorio_destino]
set -euo pipefail
DEST="${1:-./backups}"
mkdir -p "$DEST"
STAMP="$(date +%Y%m%d_%H%M%S)"
docker exec nutriologo-db pg_dump -U postgres -d nutriologo -F c -f "/tmp/nutriologo_${STAMP}.dump"
docker cp "nutriologo-db:/tmp/nutriologo_${STAMP}.dump" "${DEST}/nutriologo_${STAMP}.dump"
docker exec nutriologo-db rm "/tmp/nutriologo_${STAMP}.dump"
echo "Backup escrito en ${DEST}/nutriologo_${STAMP}.dump"
