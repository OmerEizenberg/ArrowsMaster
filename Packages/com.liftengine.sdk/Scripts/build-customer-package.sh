#!/usr/bin/env bash
# Builds a customer-facing LiftEngine SDK folder for Unity "Add package from disk".
# Usage: ./Packages/com.liftengine.sdk/Scripts/build-customer-package.sh [version]

set -euo pipefail

VERSION="${1:-1.1.7}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$PKG_DIR/../.." && pwd)"
DIST_DIR="$REPO_ROOT/dist/liftengine-sdk-build"
PKG_STAGE="$DIST_DIR/com.liftengine.sdk"

RUNTIME_DLL="$REPO_ROOT/dist/_dlls/LiftEngine.Runtime.dll"
EDITOR_DLL="$REPO_ROOT/dist/_dlls/LiftEngine.Editor.dll"
if [[ ! -f "$RUNTIME_DLL" ]]; then
  RUNTIME_DLL="$REPO_ROOT/Library/ScriptAssemblies/LiftEngine.Runtime.dll"
fi
if [[ ! -f "$EDITOR_DLL" ]]; then
  EDITOR_DLL="$REPO_ROOT/Library/ScriptAssemblies/LiftEngine.Editor.dll"
fi

if [[ ! -f "$RUNTIME_DLL" ]]; then
  echo "ERROR: $RUNTIME_DLL not found."
  echo "Open the Unity project and let scripts compile, then re-run."
  exit 1
fi

if [[ ! -f "$EDITOR_DLL" ]]; then
  echo "ERROR: $EDITOR_DLL not found."
  exit 1
fi

echo "Building com.liftengine.sdk v${VERSION}..."

rm -rf "$DIST_DIR"
mkdir -p "$PKG_STAGE/Plugins"
mkdir -p "$PKG_STAGE/Editor/Plugins"
mkdir -p "$PKG_STAGE/Runtime"

cp "$PKG_DIR/package.json" "$PKG_STAGE/"
cp "$PKG_DIR/README.md" "$PKG_STAGE/"
cp "$PKG_DIR/CHANGELOG.md" "$PKG_STAGE/"
cp "$PKG_DIR/LICENSE.md" "$PKG_STAGE/" 2>/dev/null || true
cp -R "$PKG_DIR/Documentation~" "$PKG_STAGE/" 2>/dev/null || true
rm -f "$PKG_STAGE/Documentation~/DISTRIBUTION.md" 2>/dev/null || true
cp -R "$PKG_DIR/Samples~" "$PKG_STAGE/" 2>/dev/null || true
cp "$PKG_DIR/Runtime/link.xml" "$PKG_STAGE/Runtime/" 2>/dev/null || true

# Runtime plugin at package root Plugins/ — not under any asmdef
cp "$RUNTIME_DLL" "$PKG_STAGE/Plugins/LiftEngine.Runtime.dll"
cp "$EDITOR_DLL" "$PKG_STAGE/Editor/Plugins/LiftEngine.Editor.dll"

# Do not ship LiftEngineSettings.cs (type is in the runtime DLL).
# Do not ship Runtime/LiftEngine.Runtime.asmdef (name collision + hides plugin from Assembly-CSharp).

cat > "$PKG_STAGE/Editor/LiftEngine.Editor.asmdef" << 'EOF'
{
  "name": "LiftEngine.Editor.Package",
  "rootNamespace": "LiftEngine.Editor",
  "references": [
    "Newtonsoft.Json"
  ],
  "precompiledReferences": [
    "LiftEngine.Editor.dll",
    "LiftEngine.Runtime.dll"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "autoReferenced": true,
  "overrideReferences": false
}
EOF

cat > "$PKG_STAGE/Plugins/LiftEngine.Runtime.dll.meta" << 'EOF'
fileFormatVersion: 2
guid: 297f8c283ca664eb5bd85e0860c51460
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 1
      settings: {}
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF

cat > "$PKG_STAGE/Editor/Plugins/LiftEngine.Editor.dll.meta" << 'EOF'
fileFormatVersion: 2
guid: a2502ecb50af34929aec221d9982e85a
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings: {}
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Android: Android
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  - first:
      iPhone: iOS
    second:
      enabled: 0
      settings:
        CompileFlags: 
        FrameworkDependencies: 
  - first:
      tvOS: tvOS
    second:
      enabled: 0
      settings:
        CompileFlags: 
        FrameworkDependencies: 
  - first:
      Standalone: Win
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: Win64
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Standalone: Linux64
    second:
      enabled: 0
      settings:
        CPU: None
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF

python3 -c "
import json
p = '$PKG_STAGE/package.json'
with open(p) as f: d = json.load(f)
d['version'] = '$VERSION'
with open(p, 'w') as f: json.dump(d, f, indent=2); f.write('\n')
"

cat > "$PKG_STAGE/INSTALL.md" << EOF
# LiftEngine SDK ${VERSION} — First official release

Use **Add package from disk**. Do not use Add package from tarball.

## Requirements
- Unity 2021.3 or newer
- AppLovin MAX 8.0+ (installed first)
- Newtonsoft Json (\`com.unity.nuget.newtonsoft-json\` 3.2.1+)

## Install
1. Unzip the LiftEngine SDK zip and keep the \`com.liftengine.sdk\` folder on disk.
2. In Unity: **Window → Package Manager → + → Add package from disk…**
3. Select \`com.liftengine.sdk/package.json\`.
4. Create settings: copy the example from **Package Manager → LiftEngine SDK → Samples → Example LiftEngineSettings** to \`Assets/Resources/LiftEngineSettings.asset\`, or use **Window → LiftEngine → Integration Manager**.
5. Replace placeholders with your LiftEngine API key, environment, and MAX ad unit IDs.
6. Initialize **after** MAX is initialized. See \`Documentation~/INTEGRATION.md\`.
7. Optional: paste \`Documentation~/CURSOR_INTEGRATION_PROMPT.md\` into Cursor after the package is imported.

After import, a script under \`Assets/\` with \`using LiftEngine;\` must compile with no \`csc.rsp\` and no \`extern alias\`.

Do not copy the folder into the project's \`Packages/\` directory. Do not unpack or replace the DLLs. Do not delete the unzipped SDK folder — Unity keeps a \`file:\` path to it.
EOF

python3 -c "
import json
from pathlib import Path
p = Path('$PKG_STAGE') / 'package.json'
assert p.is_file(), p
d = json.loads(p.read_text())
d['version'] = '$VERSION'
p.write_text(json.dumps(d, indent=2) + '\n')
assert not (Path('$PKG_STAGE') / 'Runtime' / 'LiftEngineSettings.cs').exists()
assert not list(Path('$PKG_STAGE').rglob('LiftEngine.Runtime.asmdef'))
assert (Path('$PKG_STAGE') / 'Plugins' / 'LiftEngine.Runtime.dll').is_file()
assert (Path('$PKG_STAGE') / 'Plugins' / 'LiftEngine.Runtime.dll.meta').is_file()
editor_meta = (Path('$PKG_STAGE') / 'Editor' / 'Plugins' / 'LiftEngine.Editor.dll.meta').read_text()
assert 'iPhone: iOS' in editor_meta and 'Android: Android' in editor_meta
assert 'isExplicitlyReferenced: 0' in (Path('$PKG_STAGE') / 'Plugins' / 'LiftEngine.Runtime.dll.meta').read_text()
print('package folder ok')
"

SHIP_DIR="$REPO_ROOT/dist/LiftEngine-SDK-${VERSION}"
rm -rf "$SHIP_DIR"
mkdir -p "$SHIP_DIR/Documentation"
cp -R "$PKG_STAGE" "$SHIP_DIR/com.liftengine.sdk"
cp "$PKG_STAGE/INSTALL.md" "$SHIP_DIR/INSTALL.txt"
cp "$PKG_STAGE/LICENSE.md" "$SHIP_DIR/LICENSE.txt" 2>/dev/null || true
cp "$PKG_STAGE/Documentation~/PACKAGING.md" "$SHIP_DIR/PACKAGING.md" 2>/dev/null || true
cp "$PKG_STAGE/Documentation~/INTEGRATION.md" "$SHIP_DIR/Documentation/" 2>/dev/null || true
cp "$PKG_STAGE/Documentation~/CURSOR_INTEGRATION_PROMPT.md" "$SHIP_DIR/Documentation/" 2>/dev/null || true
mkdir -p "$SHIP_DIR/Examples"
cp "$PKG_STAGE/Samples~/LiftEngineSettings/LiftEngineSettings.asset" "$SHIP_DIR/Examples/" 2>/dev/null || true
cp "$PKG_STAGE/Samples~/LiftEngineSettings/README.md" "$SHIP_DIR/Examples/" 2>/dev/null || true

SHIP_ZIP="$REPO_ROOT/dist/LiftEngine-SDK-${VERSION}.zip"
rm -f "$SHIP_ZIP"
(cd "$REPO_ROOT/dist" && zip -qr "LiftEngine-SDK-${VERSION}.zip" "LiftEngine-SDK-${VERSION}")

echo ""
echo "Done:"
echo "  $SHIP_DIR"
echo "  $SHIP_ZIP"
echo "Package folder:"
find "$SHIP_DIR/com.liftengine.sdk" -type f | head -40
echo "Next: Package Manager → Add package from disk → com.liftengine.sdk/package.json"
