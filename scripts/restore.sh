#!/usr/bin/env bash
# Restauración reproducible desde backup.
# Uso: scripts/restore.sh <archivo.dump>
set -euo pipefail
DUMP="${1:?uso: restore.sh <archivo.dump>}"
docker cp "$DUMP" nutriologo-db:/tmp/restore.dump
docker exec nutriologo-db psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS nutriologo_restore;"
docker exec nutriologo-db psql -U postgres -d postgres -c "CREATE DATABASE nutriologo_restore;"
docker exec nutriologo-db pg_restore -U postgres -d nutriologo_restore --no-owner --no-acl /tmp/restore.dump
docker exec nutriologo-db rm /tmp/restore.dump
echo "Restaurado en base nutriologo_restore"
