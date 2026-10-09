#!/bin/bash
# Regenerates code from the sheets, builds, and zips the release into dist/.
set -euo pipefail
cd "$(dirname "$0")/.."
python3 tools/sheets.py gen
dotnet build stub/Assembly-CSharp -c Release -v q -nologo >/dev/null
dotnet build src/TurnipDash -c Release -v q -nologo >/dev/null
VER=$(grep -oP '(?<=<Version>)[^<]+' src/TurnipDash/TurnipDash.csproj)
OUT=dist/TurnipDash-$VER.zip
STAGE=$(mktemp -d)
B=src/TurnipDash/bin/Release/net472
cp $B/TurnipDash.dll $B/NLayer.dll $B/AssetsTools.NET.dll README.md THIRD-PARTY-NOTICES.txt "$STAGE"/
mkdir -p dist && rm -f "$OUT"
(cd "$STAGE" && touch -d 2026-01-01 * && zip -qX -D "$OLDPWD/$OUT" *)
rm -rf "$STAGE"
echo "$OUT $(stat -c %s "$OUT") $(sha256sum "$OUT" | cut -d' ' -f1)"
unzip -l "$OUT"
