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

RUNTIME_DLL="$REPO_ROOT/Library/ScriptAssemblies/LiftEngine.Runtime.dll"
EDITOR_DLL="$REPO_ROOT/Library/ScriptAssemblies/LiftEngine.Editor.dll"

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
cp "$PKG_DIR/CHANGELOG.md" "$DIST_DIR/com.liftengine.sdk/" 2>/dev/null || true
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
with open(p, 'w') as f: json.dump(d, f, indent=2); f.write('\n')
"
fi

mkdir -p "$(dirname "$OUTPUT_TGZ")"
tar -czf "$OUTPUT_TGZ" -C "$DIST_DIR" com.liftengine.sdk

echo ""
echo "Done: $OUTPUT_TGZ"
echo "Verify: tar -tzf $OUTPUT_TGZ | head -30"
echo "Next: test in a fresh Unity project before sending to customer."
