#!/usr/bin/env bash
# check-sim-purity.sh
#
# Greps src/Simulation/ for constructs that break determinism. Exits non-zero on any hit.
# Wired into CI so a forbidden construct can never reach main unnoticed.
#
# The full rationale lives in docs/DETERMINISM.md. Short version: floats, wall clocks,
# unseeded RNGs, and platform-dependent math all desync multiplayer or break replays.
#
# Run locally before commits:
#   bash scripts/check-sim-purity.sh

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SIM="$ROOT/src/Simulation"

if [[ ! -d "$SIM" ]]; then
  echo "check-sim-purity: $SIM does not exist" >&2
  exit 2
fi

# Each pattern is an ERE that scans .cs files for a forbidden construct.
# We use word-boundary-like context so 'floatation' or 'Math64' don't false-positive.
# Description follows in a parallel array (Bash doesn't have nice maps without v4+).
patterns=(
  '\busing[[:space:]]+Godot[[:space:]]*;'
  '\bfloat[[:space:]]+[A-Za-z_]'
  '\bdouble[[:space:]]+[A-Za-z_]'
  '\bSystem\.Random\b'
  '\bDateTime\b'
  '\bStopwatch\b'
  '\bEnvironment\.TickCount\b'
  '\bMath\.(Sin|Cos|Tan|Sqrt|Atan|Atan2|Asin|Acos|Pow|Exp|Log|Floor|Ceiling|Round|Abs|Min|Max|PI|E)\b'
  '\bGuid\.NewGuid\b'
  '\basync[[:space:]]'
  '\bawait[[:space:]]'
  '\bTask\.Run\b'
  '\bParallel\.'
)
labels=(
  "using Godot;        -- simulation must not reference Godot"
  "float local/field   -- use Fixed64"
  "double local/field  -- use Fixed64"
  "System.Random       -- use DeterministicRng"
  "DateTime            -- use currentTick"
  "Stopwatch           -- use currentTick"
  "Environment.TickCount -- use currentTick"
  "Math.*              -- use FixedMath"
  "Guid.NewGuid        -- use sequential EntityId"
  "async               -- simulation is synchronous"
  "await               -- simulation is synchronous"
  "Task.Run            -- simulation is synchronous"
  "Parallel.*          -- nondeterministic scheduling"
)

# Specific narrow exceptions, by file:line content allowed:
# - Fixed64.cs intentionally provides ToFloatForRender() — this is a RENDER-ONLY conversion
#   and is explicitly documented as such. It's the only sanctioned mention of 'float' in sim.
# - Fixed64.cs has 'float ' as a return type on ToFloatForRender() AND a (float) cast inside.

# Use grep -rEn so we get filename:line:content for any hit.
# Strip comment-only lines (// or ///) before the pattern check so doc-comments
# discussing the forbidden constructs don't trip the script.
fail=0
for i in "${!patterns[@]}"; do
  pat="${patterns[$i]}"
  label="${labels[$i]}"
  hits="$(grep -rEn --include='*.cs' "$pat" "$SIM" 2>/dev/null \
    | grep -vE '^[^:]+:[0-9]+:[[:space:]]*//' \
    | grep -vE 'Fixed64\.cs.*ToFloatForRender' \
    | grep -vE 'Fixed64\.cs.*\(float\)One' || true)"
  if [[ -n "$hits" ]]; then
    if [[ $fail -eq 0 ]]; then
      echo "check-sim-purity: FORBIDDEN constructs found in src/Simulation/" >&2
      echo >&2
    fi
    fail=1
    echo "  [$label]" >&2
    while IFS= read -r line; do
      echo "    $line" >&2
    done <<< "$hits"
    echo >&2
  fi
done

if [[ $fail -ne 0 ]]; then
  echo "check-sim-purity: see docs/DETERMINISM.md for what to use instead" >&2
  exit 1
fi

echo "check-sim-purity: clean"
