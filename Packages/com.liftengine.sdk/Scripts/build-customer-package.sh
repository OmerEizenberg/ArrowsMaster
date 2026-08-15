#!/usr/bin/env bash
# Builds a customer-facing LiftEngine SDK .tgz with precompiled DLLs.
# Usage: ./Packages/com.liftengine.sdk/Scripts/build-customer-package.sh [version]
# Requires: Unity project already compiled (Library/ScriptAssemblies/*.dll exist)

set -euo pipefail

VERSION="${1:-1.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$PKG_DIR/../.." && pwd)"
DIST_DIR="$REPO_ROOT/dist/liftengine-sdk-build"
OUTPUT_TGZ="$REPO_ROOT/dist/com.liftengine.sdk-${VERSION}.tgz"

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
mkdir -p "$DIST_DIR/com.liftengine.sdk/Runtime/Plugins"
mkdir -p "$DIST_DIR/com.liftengine.sdk/Editor/Plugins"

# Copy package metadata and docs
cp "$PKG_DIR/package.json" "$DIST_DIR/com.liftengine.sdk/"
cp "$PKG_DIR/README.md" "$DIST_DIR/com.liftengine.sdk/"
cp "$PKG_DIR/LICENSE.md" "$DIST_DIR/com.liftengine.sdk/" 2>/dev/null || true
cp -R "$PKG_DIR/Documentation~" "$DIST_DIR/com.liftengine.sdk/" 2>/dev/null || true
# Customer docs only — exclude internal distribution guide
rm -f "$DIST_DIR/com.liftengine.sdk/Documentation~/DISTRIBUTION.md" 2>/dev/null || true
cp -R "$PKG_DIR/Samples~" "$DIST_DIR/com.liftengine.sdk/" 2>/dev/null || true

# Copy DLLs
cp "$RUNTIME_DLL" "$DIST_DIR/com.liftengine.sdk/Runtime/Plugins/"
cp "$EDITOR_DLL" "$DIST_DIR/com.liftengine.sdk/Editor/Plugins/"

# Keep only LiftEngineSettings as runtime source
cp "$PKG_DIR/Runtime/Core/LiftEngineSettings.cs" "$DIST_DIR/com.liftengine.sdk/Runtime/"
cp "$PKG_DIR/Runtime/link.xml" "$DIST_DIR/com.liftengine.sdk/Runtime/" 2>/dev/null || true

# Customer-facing asmdef (references precompiled DLL)
cat > "$DIST_DIR/com.liftengine.sdk/Runtime/LiftEngine.Runtime.asmdef" << 'EOF'
{
  "name": "LiftEngine.Runtime",
  "rootNamespace": "LiftEngine",
  "references": [
    "MaxSdk.Scripts",
    "Newtonsoft.Json"
  ],
  "precompiledReferences": [
    "LiftEngine.Runtime.dll"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "autoReferenced": true,
  "versionDefines": [
    {
      "name": "com.applovin.mediation.ads",
      "expression": "",
      "define": "LIFTENGINE_MAX"
    }
  ]
}
EOF

cat > "$DIST_DIR/com.liftengine.sdk/Editor/LiftEngine.Editor.asmdef" << 'EOF'
{
  "name": "LiftEngine.Editor",
  "rootNamespace": "LiftEngine.Editor",
  "references": [
    "LiftEngine.Runtime",
    "Newtonsoft.Json"
  ],
  "precompiledReferences": [
    "LiftEngine.Editor.dll"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "autoReferenced": true
}
EOF

# Bump version in dist package.json
if command -v python3 &>/dev/null; then
  python3 -c "
import json, sys
p = '$DIST_DIR/com.liftengine.sdk/package.json'
with open(p) as f: d = json.load(f)
d['version'] = '$VERSION'
d.pop('changelogUrl', None)
with open(p, 'w') as f: json.dump(d, f, indent=2); f.write('\n')
"
fi

# Customer install note (package root)
cat > "$DIST_DIR/com.liftengine.sdk/INSTALL.md" << EOF
# LiftEngine SDK ${VERSION} — Install

## Requirements
- Unity 2021.3 or newer
- AppLovin MAX 8.0+ (installed first)
- Newtonsoft Json (\`com.unity.nuget.newtonsoft-json\` 3.2.1+)

## Install
1. Install AppLovin MAX from the Unity Package Manager or MAX dashboard export.
2. In Unity: **Window → Package Manager → + → Add package from tarball** and select \`com.liftengine.sdk-${VERSION}.tgz\`.
3. Create settings: **Assets/Resources/LiftEngineSettings.asset** (or use **Window → LiftEngine → Integration Manager**).
4. Paste your LiftEngine API key, set environment, and enter MAX ad unit IDs.
5. Initialize **after** MAX is initialized. See \`Documentation~/INTEGRATION.md\`.
6. Optional: paste \`Documentation~/CURSOR_INTEGRATION_PROMPT.md\` into Cursor after the package is imported.

Do not copy a package folder into \`Packages/\`. Do not unpack or replace the DLLs.
EOF

mkdir -p "$(dirname "$OUTPUT_TGZ")"
tar -czf "$OUTPUT_TGZ" -C "$DIST_DIR" com.liftengine.sdk

# Ready-to-send zip: tarball + docs only (no folder-drop package)
SHIP_DIR="$REPO_ROOT/dist/LiftEngine-SDK-${VERSION}"
rm -rf "$SHIP_DIR"
mkdir -p "$SHIP_DIR/Documentation"
cp "$OUTPUT_TGZ" "$SHIP_DIR/"
cp "$DIST_DIR/com.liftengine.sdk/README.md" "$SHIP_DIR/"
cp "$DIST_DIR/com.liftengine.sdk/INSTALL.md" "$SHIP_DIR/"
cp "$DIST_DIR/com.liftengine.sdk/LICENSE.md" "$SHIP_DIR/" 2>/dev/null || true
cp "$DIST_DIR/com.liftengine.sdk/Documentation~/INTEGRATION.md" "$SHIP_DIR/Documentation/" 2>/dev/null || true
cp "$DIST_DIR/com.liftengine.sdk/Documentation~/CURSOR_INTEGRATION_PROMPT.md" "$SHIP_DIR/Documentation/" 2>/dev/null || true

SHIP_ZIP="$REPO_ROOT/dist/LiftEngine-SDK-${VERSION}.zip"
rm -f "$SHIP_ZIP"
(cd "$REPO_ROOT/dist" && zip -qr "LiftEngine-SDK-${VERSION}.zip" "LiftEngine-SDK-${VERSION}")

echo ""
echo "Done:"
echo "  $OUTPUT_TGZ"
echo "  $SHIP_DIR"
echo "  $SHIP_ZIP"
echo "Verify: tar -tzf $OUTPUT_TGZ | head -30"
echo "Next: test in a fresh Unity project before sending to customer."
