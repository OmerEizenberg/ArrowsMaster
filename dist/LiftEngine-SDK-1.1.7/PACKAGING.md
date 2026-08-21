# LiftEngine Unity package — publish checklist

Studios install with **Add package from disk** (select `package.json`). Do not ship a `.tgz` as the install method.

## 1. Zip contains a real package folder

```
LiftEngine-SDK-1.1.7/
  INSTALL.txt
  com.liftengine.sdk/
    package.json
    Plugins/LiftEngine.Runtime.dll
    Plugins/LiftEngine.Runtime.dll.meta
    Editor/...
    Runtime/link.xml
    Samples~/LiftEngineSettings/LiftEngineSettings.asset
  Examples/
    LiftEngineSettings.asset
```

`package.json` must be directly inside `com.liftengine.sdk/`.

Studios install **1.1.7** — the first official LiftEngine Unity SDK. Ship `Examples/LiftEngineSettings.asset` with placeholders only — never a real API key.

## 2. Runtime DLL must not sit under a Runtime asmdef

Ship `Plugins/LiftEngine.Runtime.dll` at the package root `Plugins/` folder. Do **not** name an asmdef `LiftEngine.Runtime`.

## 3. Ship complete PluginImporter `.meta` files

- `Plugins/LiftEngine.Runtime.dll.meta` — Any + Editor, `isExplicitlyReferenced: 0`
- `Editor/Plugins/LiftEngine.Editor.dll.meta` — Editor-only: Any / iOS / Android / standalone **enabled 0**, Editor **enabled 1**. Runtime DLL stays `isExplicitlyReferenced: 0`.

## 4. Runtime DLL must not AssemblyRef UnityEditor

`LiftEngine.Runtime.dll` is included in the player. It must never reference `UnityEditor` / `UnityEditor.CoreModule` (verify with a metadata dump). OS/platform detection must use `Application.platform` only — no `EditorUserBuildSettings`.

`link.xml` must preserve **only** `LiftEngine.Runtime`, not `LiftEngine.Editor`.

## 5. Do not ship `LiftEngineSettings.cs` as source

The type is already in the runtime DLL.

## 6. Editor asmdef

Name it `LiftEngine.Editor.Package` (not `LiftEngine.Editor`) and precompile-ref both DLLs.

## Verify

1. New Unity project + MAX 8.x.
2. Package Manager → Add package from disk → `com.liftengine.sdk/package.json`.
3. Version shows **1.1.7** (first official). Integration Manager opens. Example settings sample is listed.
4. `Assets/` script with `using LiftEngine;` compiles with no `csc.rsp`.
5. iOS/Android player build: only `LiftEngine.Runtime` in player; no `UnityEditor` AssemblyRef on Runtime DLL.
