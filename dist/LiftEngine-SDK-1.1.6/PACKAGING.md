# LiftEngine Unity package — publish checklist

Studios install with **Add package from disk** (select `package.json`). Do not ship a `.tgz` as the install method.

## 1. Zip contains a real package folder

```
LiftEngine-SDK-1.1.6/
  INSTALL.txt
  com.liftengine.sdk/
    package.json
    Plugins/LiftEngine.Runtime.dll
    Plugins/LiftEngine.Runtime.dll.meta
    Editor/...
    Runtime/link.xml
```

`package.json` must be directly inside `com.liftengine.sdk/`.

Studios install **1.1.6**. Older drops failed Unity 6 player builds: 1.1.4 Editor DLL platform flags, 1.1.5 Runtime DLL still AssemblyRef'd `UnityEditor.CoreModule`.

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
3. Version shows **1.1.6**. Integration Manager opens.
4. `Assets/` script with `using LiftEngine;` compiles with no `csc.rsp`.
5. iOS/Android player build: only `LiftEngine.Runtime` in player; no `UnityEditor` AssemblyRef on Runtime DLL.
