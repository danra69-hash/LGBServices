#!/usr/bin/env bash
# Keeps LGB dev backend + frontend alive. Run: ./scripts/dev-watch.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT/scripts/dev-path.sh"
LOG="$ROOT/.dev-watch.log"
BACKEND_PID=""
FRONTEND_PID=""

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG"; }

port_up() { lsof -i:"$1" >/dev/null 2>&1; }

start_backend() {
  if port_up 5003; then return; fi
  log "Starting backend on :5003"
  cd "$ROOT/LGBApp.Backend"
  dotnet run >>"$LOG" 2>&1 &
  BACKEND_PID=$!
}

start_frontend() {
  if port_up 5173; then return; fi
  log "Starting frontend on :5173"
  cd "$ROOT/LGBApp.Frontend"
  npm run dev -- --host 0.0.0.0 >>"$LOG" 2>&1 &
  FRONTEND_PID=$!
}

log "dev-watch started (check every 30s)"
while true; do
  if ! port_up 5003; then
    log "WARN backend down — restarting"
    start_backend
    sleep 8
  fi
  if ! port_up 5173; then
    log "WARN frontend down — restarting"
    start_frontend
    sleep 3
  fi
  if port_up 5003 && port_up 5173; then
    :
  fi
  sleep 30
done
