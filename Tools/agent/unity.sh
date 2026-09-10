#!/usr/bin/env bash
# CLI-мост к живому редактору Unity. Требует включённого Warlord/Agent Bridge.
#   Tools/agent/unity.sh status
#   Tools/agent/unity.sh screenshot target=game name=lobby.png
#   Tools/agent/unity.sh hierarchy depth=3
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
AGENT="$ROOT/.agent"
TIMEOUT="${UNITY_AGENT_TIMEOUT:-30}"

cmd="${1:-status}"
shift || true

mkdir -p "$AGENT/inbox" "$AGENT/outbox" "$AGENT/shots"
id="$(date +%s%N)-$$"

{
  echo "$cmd"
  for kv in "$@"; do echo "$kv"; done
} > "$AGENT/inbox/$id.tmp"
mv "$AGENT/inbox/$id.tmp" "$AGENT/inbox/$id.req"

res="$AGENT/outbox/$id.res"
deadline=$(( $(date +%s) + TIMEOUT ))
while [ ! -f "$res" ]; do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    rm -f "$AGENT/inbox/$id.req"
    echo "{\"ok\":false,\"error\":\"timeout ${TIMEOUT}s: редактор не ответил (закрыт / компилирует / мост выключен)\"}"
    exit 2
  fi
  sleep 0.2
done

cat "$res"
echo
rm -f "$res"
