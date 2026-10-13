#!/usr/bin/env bash
# Starts the Spincio server for online play (ADR 0014).
# In a GitHub Codespace (.devcontainer runs it on opening) it also makes port 5080 public and prints the link to open on
# the phone; elsewhere it serves http://localhost:5080. While people play, the server logs a line every 5 minutes: in a
# codespace that counts as activity, so it stays on during a match and switches itself off when idle.
set -euo pipefail
cd "$(dirname "$0")/../.."
APP="https://mattiag98.github.io/SpincioMobile/"
PORT=5080

if [ -n "${CODESPACE_NAME:-}" ]; then
  SERVER="https://${CODESPACE_NAME}-${PORT}.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
else
  SERVER="http://localhost:${PORT}"
fi

banner() {
  echo
  echo "================================================================"
  echo " Server Spincio acceso."
  echo " Apri questo link sul telefono, crea la stanza e premi \"Invita gli amici\":"
  echo
  echo "   ${APP}online?server=${SERVER}"
  echo
  echo " Lascia aperta questa finestra finché giocate."
  echo "================================================================"
  echo
}

if curl -fs "http://localhost:${PORT}/health" >/dev/null 2>&1; then
  banner # already running (the codespace was opened again)
  exit 0
fi

echo "Preparo il server (la prima volta ci vuole un minuto)…"
dotnet build src/Spincio.Server -c Release --nologo -v quiet
dotnet run --project src/Spincio.Server -c Release --no-build --no-launch-profile --urls "http://0.0.0.0:${PORT}" &
PID=$!
trap 'kill $PID 2>/dev/null || true' INT TERM

for _ in $(seq 1 60); do
  curl -fs "http://localhost:${PORT}/health" >/dev/null 2>&1 && break
  sleep 1
done

if [ -n "${CODESPACE_NAME:-}" ]; then
  # Friends' phones must reach the server without a GitHub login.
  if ! gh codespace ports visibility "${PORT}:public" -c "${CODESPACE_NAME}" >/dev/null 2>&1; then
    echo "ATTENZIONE: rendi pubblica la porta ${PORT} a mano: scheda PORTE, tasto destro sulla porta, Visibilità, Pubblica."
  fi
fi

banner
wait $PID
