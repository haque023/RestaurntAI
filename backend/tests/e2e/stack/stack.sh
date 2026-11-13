#!/usr/bin/env bash
# Isolated E2E stack: its own database, RabbitMQ vhost, Redis DB index and ports, so test runs can
# never touch the development data, the development audit log or the development services.
#
#   stack.sh up      create the isolated environment (idempotent) and start all 11 services
#   stack.sh down    stop only the processes this script started
#   stack.sh status  show health of the E2E services
#
# Requires: the backend built (dotnet build backend/RestaurantAI.sln), the local Docker dependencies
# running (backend/docker-compose.yml), and a .NET 10 `dotnet` on PATH (or DOTNET=/path/to/dotnet).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
E2E="$(cd "$HERE/.." && pwd)"
BACKEND="$(cd "$E2E/../.." && pwd)"
RUN_DIR="$E2E/.run"
LOG_DIR="$RUN_DIR/logs"
PID_FILE="$RUN_DIR/stack.pids"
ENV_FILE="$RUN_DIR/stack.env"

DOTNET="${DOTNET:-dotnet}"
PG_CONTAINER="${E2E_PG_CONTAINER:-restaurant-ai-postgres}"
RABBIT_CONTAINER="${E2E_RABBIT_CONTAINER:-restaurant-ai-dev-rabbitmq-1}"
DB_NAME="${E2E_DB_NAME:-restaurant_ai_e2e}"
VHOST="${E2E_RABBIT_VHOST:-restaurant_ai_e2e}"
REDIS_DB="${E2E_REDIS_DB:-9}"
MARKER="restaurant-ai-e2e-isolated"

# name|project dir|assembly|port
SERVICES=(
  "identity|src/Services/Identity.Api|Identity.Api|6201"
  "consumer|src/Services/Consumer.Api|Consumer.Api|6202"
  "restaurant|src/Services/Restaurant.Api|Restaurant.Api|6203"
  "menu|src/Services/Menu.Api|Menu.Api|6204"
  "order|src/Services/Order.Api|Order.Api|6205"
  "reservation|src/Services/Reservation.Api|Reservation.Api|6206"
  "loyalty|src/Services/Loyalty.Api|Loyalty.Api|6207"
  "admin|src/Services/Admin.Api|Admin.Api|6208"
  "ai|src/Services/Ai.Api|Ai.Api|6300"
  "gateway|src/Gateways/Gateway.Api|Gateway.Api|6080"
  "mcp|src/Gateways/Mcp.Gateway|Mcp.Gateway|6100"
)

die() { echo "stack: $*" >&2; exit 1; }
psql_admin() { docker exec -i "$PG_CONTAINER" psql -U postgres -v ON_ERROR_STOP=1 -At "$@"; }

guard_db_name() {
  [[ "$DB_NAME" == *_e2e ]] || die "refusing: E2E database name must end with _e2e (got '$DB_NAME')"
}

ensure_database() {
  guard_db_name
  if [[ -z "$(psql_admin -d postgres -c "select 1 from pg_database where datname = '$DB_NAME'")" ]]; then
    echo "creating isolated database $DB_NAME"
    psql_admin -d postgres -c "CREATE DATABASE \"$DB_NAME\" OWNER restaurant_app"
  fi
  psql_admin -d "$DB_NAME" >/dev/null <<SQL
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE SCHEMA IF NOT EXISTS e2e_meta AUTHORIZATION postgres;
REVOKE ALL ON SCHEMA e2e_meta FROM PUBLIC;
CREATE TABLE IF NOT EXISTS e2e_meta.environment (marker text PRIMARY KEY, created_at timestamptz NOT NULL DEFAULT now());
INSERT INTO e2e_meta.environment (marker) VALUES ('$MARKER') ON CONFLICT DO NOTHING;
SQL
}

ensure_vhost() {
  docker exec "$RABBIT_CONTAINER" rabbitmqctl -q list_vhosts | grep -qx "$VHOST" || {
    echo "creating isolated RabbitMQ vhost $VHOST"
    docker exec "$RABBIT_CONTAINER" rabbitmqctl -q add_vhost "$VHOST"
  }
  docker exec "$RABBIT_CONTAINER" rabbitmqctl -q set_permissions -p "$VHOST" guest ".*" ".*" ".*" >/dev/null
}

# Reuses the development connection string (role and password) and only swaps port and database.
connection_string() {
  local base port
  base="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1],encoding='utf-8-sig'))['ConnectionStrings']['Platform'])" "$BACKEND/src/Services/Order.Api/appsettings.json")"
  port="$(docker port "$PG_CONTAINER" 5432/tcp | head -1 | sed 's/.*://')"
  python3 -c "
import re,sys
s,port,db=sys.argv[1:4]
s=re.sub(r'Port=\d+','Port='+port,s)
s=re.sub(r'Database=[^;]+','Database='+db,s)
print(s)" "$base" "$port" "$DB_NAME"
}

ensure_env_file() {
  mkdir -p "$RUN_DIR" "$LOG_DIR"
  if [[ ! -f "$ENV_FILE" ]]; then
    # A random administrator password per environment; never committed (see .gitignore).
    local pw
    pw="E2e#$(python3 -c 'import secrets;print(secrets.token_urlsafe(18))')"
    umask 077
    cat >"$ENV_FILE" <<ENV
E2E_GATEWAY_URL=http://localhost:6080
E2E_MCP_URL=http://localhost:6100/mcp
E2E_ADMIN_EMAIL=admin@e2e.test
E2E_ADMIN_PASSWORD=$pw
E2E_DB_NAME=$DB_NAME
E2E_PG_CONTAINER=$PG_CONTAINER
ENV
  fi
}

up() {
  "$DOTNET" --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.' || die "a .NET 10 runtime is required (set DOTNET or PATH)"
  for s in "${SERVICES[@]}"; do
    IFS='|' read -r name dir asm port <<<"$s"
    [[ -f "$BACKEND/$dir/bin/Debug/net10.0/$asm.dll" ]] || die "$asm is not built; run: dotnet build backend/RestaurantAI.sln"
    if lsof -nP -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then die "port $port is already in use (is the stack already up?)"; fi
  done

  ensure_database
  ensure_vhost
  ensure_env_file
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  local conn rabbit redis
  conn="$(connection_string)"
  rabbit="amqp://guest:guest@localhost:5672/$VHOST"
  redis="localhost:6379,defaultDatabase=$REDIS_DB"
  : >"$PID_FILE"

  for s in "${SERVICES[@]}"; do
    IFS='|' read -r name dir asm port <<<"$s"
    (
      cd "$BACKEND/$dir"
      export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:$port"
      export ConnectionStrings__Platform="$conn" ConnectionStrings__Redis="$redis" RabbitMQ__ConnectionString="$rabbit"
      export Services__Gateway="http://localhost:6080" Services__Menu="http://localhost:6204"
      export AI__Claude__McpEndpoint="http://localhost:6100/mcp"
      export Identity__BootstrapAdmin__Email="$E2E_ADMIN_EMAIL" Identity__BootstrapAdmin__Password="$E2E_ADMIN_PASSWORD" Identity__BootstrapAdmin__DisplayName="E2E Admin"
      export RateLimiting__AnonymousPermitsPerMinute=2000 RateLimiting__TenantPermitsPerMinute=5000
      for c in identity:6201 consumer:6202 restaurant:6203 menu:6204 order:6205 reservation:6206 loyalty:6207 admin:6208 ai:6300; do
        export "ReverseProxy__Clusters__${c%%:*}__Destinations__primary__Address=http://localhost:${c##*:}/"
      done
      # An explicitly provided key wins; otherwise Ai.Api reads its own user-secrets in Development.
      if [[ -n "${ANTHROPIC_API_KEY:-}" ]]; then export AI__Claude__ApiKey="$ANTHROPIC_API_KEY"; fi
      nohup "$DOTNET" "bin/Debug/net10.0/$asm.dll" >"$LOG_DIR/$name.log" 2>&1 &
      echo "$! $name $port" >>"$PID_FILE"
    )
  done

  echo "waiting for services..."
  for _ in $(seq 1 90); do
    if status >/dev/null 2>&1; then status; echo "E2E stack is up (gateway http://localhost:6080)"; return 0; fi
    sleep 1
  done
  status || true
  die "services did not become healthy; see $LOG_DIR"
}

status() {
  local bad=0
  for s in "${SERVICES[@]}"; do
    IFS='|' read -r name _ _ port <<<"$s"
    if curl -fs -o /dev/null --max-time 2 "http://localhost:$port/health/live"; then echo "up    $name :$port"; else echo "DOWN  $name :$port"; bad=1; fi
  done
  return $bad
}

down() {
  [[ -f "$PID_FILE" ]] || { echo "no E2E stack recorded"; return 0; }
  while read -r pid name port; do
    # Only stop the process this script started, and only if it still listens on its E2E port.
    if kill -0 "$pid" 2>/dev/null && lsof -nP -a -p "$pid" -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
      kill "$pid" && echo "stopped $name (pid $pid)"
    fi
  done <"$PID_FILE"
  rm -f "$PID_FILE"
}

case "${1:-}" in
  up) up ;;
  down) down ;;
  status) status ;;
  *) echo "usage: $0 up|down|status" >&2; exit 2 ;;
esac
