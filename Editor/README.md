# Editor

Editor-only assembly (`GianaUtils.Editor`, platform: Editor) containing the dependency detector. Nothing here ships in builds.

## `DependencyDetector` (`PackageDependencyChecker.cs`)

An `[InitializeOnLoad]` class that decides whether the optional [integrations](../Integration/README.md) can compile and keeps the matching scripting define symbols in sync.

### What it checks

| Dependency | Detected when | Define |
|---|---|---|
| Feel | `MoreMountains.Tools` assembly is loaded **or** an asmdef named so exists | `GIANAUTILS_FEEL` |
| DOTween | A `DOTween`/`DOTween.Modules` assembly is loaded **and** a DOTween asmdef exists | `GIANAUTILS_DOTWEEN` |

If DOTween is installed but has no asmdef, the integration assembly cannot reference it: the detector logs a warning, shows a one-time dialog (per session; always on manual run) and leaves the define off. Fix: **Tools → Demigiant → DOTween Utility Panel → Create ASMDEF**.

### When it runs

- After every assembly reload (silently).
- Once per Editor session on load (guarded by `SessionState`).
- On demand: **Tools → GianaUtils → Check Dependencies** (also logs `Dependencies verified. Feel: …, DOTween: …`).

### How it syncs defines

For every valid `BuildTargetGroup` it reads `PlayerSettings.GetScriptingDefineSymbols`, adds or removes the symbol so it matches the detection result, and writes it back. Groups that are invalid or unsupported are skipped. A change triggers a recompile, which then includes or excludes the integration assemblies.

### Troubleshooting

| Problem | Fix |
|---|---|
| `DOTweenMenuAnimator` / `FeelUITransition` not found | Run *Check Dependencies*; confirm DOTween has asmdefs / Feel is imported; check *Project Settings → Player → Scripting Define Symbols* |
| Define stays off for DOTween | Create the DOTween ASMDEF |
| Removed an asset but compile errors remain | Run *Check Dependencies* to remove the stale define |
