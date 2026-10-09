#!/usr/bin/env bash
# Builds dist/TurnipDash-<version>.zip: sheets -> preflight -> generated code + map -> dotnet build -> package.
set -euo pipefail
cd "$(dirname "$0")"
VERSION=$(sed -n 's/^  Version: //p' mod/everest.yaml | head -1)
EVEREST=stable-1.6580.0   # the Everest build Melty installs for Celeste (game_info)
if [ ! -f .lib/Celeste.dll ]; then
  mkdir -p .lib/dl && curl -sSLf -o .lib/dl/lib-stripped.zip "https://github.com/EverestAPI/Everest/releases/download/$EVEREST/lib-stripped.zip"
  unzip -o -q -j .lib/dl/lib-stripped.zip 'lib-stripped/Celeste.dll' 'lib-stripped/FNA.dll' 'lib-stripped/MMHOOK_Celeste.dll' 'lib-stripped/MonoMod.Utils.dll' 'lib-stripped/MonoMod.RuntimeDetour.dll' 'lib-stripped/YamlDotNet.dll' -d .lib
fi
if [ ! -f .lib/classdata.tpk ]; then
  mkdir -p .lib/dl && curl -sSLf -o .lib/dl/uabea.zip https://github.com/nesrak1/UABEA/releases/download/v8/uabea-windows.zip
  unzip -o -q -j .lib/dl/uabea.zip classdata.tpk -d .lib
fi
python3 tools/preflight.py
python3 tools/gen_cs.py
dotnet build src/TurnipDash -c Release -nologo -v q
OUT=build/TurnipDash; rm -rf build && mkdir -p "$OUT/TurnipDash" dist
cp -r mod/. "$OUT/"
python3 tools/gen_map.py "$OUT"
BIN=src/TurnipDash/bin/Release/net8.0
cp $BIN/*.dll "$OUT/"   # TurnipDash.dll + its bundled libraries (Celeste/Everest refs are not copied: Private=false)
cp .lib/classdata.tpk "$OUT/TurnipDash/classdata.tpk"
cp README.md CREDITS.md "$OUT/"
mkdir -p "$OUT/licenses" && cp licenses/* "$OUT/licenses/"
(cd "$OUT" && rm -f "../../dist/TurnipDash-$VERSION.zip" && TZ=UTC zip -X -q -r "../../dist/TurnipDash-$VERSION.zip" .)
echo "built dist/TurnipDash-$VERSION.zip"
