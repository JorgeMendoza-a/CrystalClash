#!/usr/bin/env bash
# Reconstruye el módulo de Cloud Code (CloudCode/Build/CrystalClashRanked.ccm). Requiere .NET SDK 8.
set -euo pipefail
cd "$(dirname "$0")"
out="Build/publish"
rm -rf "$out"
dotnet publish CrystalClashRanked/Project/CrystalClashRanked.csproj -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o "$out"
rm -f Build/CrystalClashRanked.ccm
(cd "$out" && zip -q -r ../CrystalClashRanked.ccm .)
rm -rf "$out"
echo "Listo: Build/CrystalClashRanked.ccm"
