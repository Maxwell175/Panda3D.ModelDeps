#!/usr/bin/env bash
# Publish the panda-model-deps NativeAOT binary for one or more RIDs into build/tools/<rid>/,
# then pack the Panda3D.ModelDeps NuGet. CI passes every RID; locally it defaults to the host.
#
# Usage: ./pack.sh [-o OUTPUT_DIR] [RID ...]
set -euo pipefail
cd "$(dirname "$0")"

OUT="${OUT:-$PWD/artifacts}"
RIDS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o) OUT="$2"; shift 2 ;;
    *)  RIDS+=("$1"); shift ;;
  esac
done
if [[ ${#RIDS[@]} -eq 0 ]]; then
  RIDS=("$(dotnet --info | grep -oiE 'RID:\s*\S+' | head -1 | awk '{print $2}')")
fi

STAGE="$PWD/build/tools"
rm -rf "$STAGE"
for rid in "${RIDS[@]}"; do
  echo ">> publishing panda-model-deps for $rid"
  dotnet publish src/Panda3D.ModelDeps -c Release -r "$rid" -p:PublishAot=true -o "build/publish-$rid"
  mkdir -p "$STAGE/$rid"
  # ship only the native exe (not .dbg symbols / .xml docs)
  cp "build/publish-$rid/panda-model-deps"* "$STAGE/$rid/" 2>/dev/null || true
  rm -f "$STAGE/$rid/"*.dbg "$STAGE/$rid/"*.xml
done

echo ">> packing Panda3D.ModelDeps -> $OUT"
mkdir -p "$OUT"
dotnet pack src/Panda3D.ModelDeps.Package -o "$OUT" -p:ToolsDir="$STAGE"
