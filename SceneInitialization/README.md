# SceneInitialization

Deterministic scene startup and scene transitions.

Unity gives no guaranteed order between `Awake`/`Start` of different objects and no way to `await` them. This module replaces that with two awaitable interfaces executed in a defined order by a persistent `LoadingManager`, hidden behind a fullscreen fade overlay.

| File | Role |
|---|---|
| `BootStarpper.cs` → `BootStrapper` | Spawns the master prefab before the first scene loads |
| `LoadingManager.cs` | Singleton: initialization pass + async scene transitions |
| `LoadingOverlayController.cs` | Fade/instant control of the overlay `Image` |
| `IInitializeAwake.cs` / `IInitializeStart.cs` | Contracts for objects that want ordered async init |
| `Resources/MasterLoadingManager.prefab` | Canvas (sort order 32767) + black `Panel` + `LoadingManager` + `LoadingOverlayController` |

---

## Startup flow

1. **`BootStrapper.InitializeMasterSystem`** runs `BeforeSceneLoad`. If no `LoadingManager` exists it loads `Resources/MasterLoadingManager`, instantiates it and calls `DontDestroyOnLoad`. (Nothing to add to your scenes.)
2. **`LoadingManager.Awake`**: becomes the singleton (`LoadingManager.Instance`), creates its lifetime `CancellationTokenSource`, hides the overlay.
3. **`LoadingManager.Start`** (for the scene that was loaded at launch):
   - scene **not** in *Bypassed Scenes*: overlay is made visible instantly → initialization pass → 2 frames → overlay fades out;
   - scene in *Bypassed Scenes*: initialization pass only, no overlay.
4. `LoadingManager.OnInitializationCompleted` (static `Action`) fires when finished.

## The initialization pass

For the target scene, `LoadingManager`:

1. Takes `scene.GetRootGameObjects()` and gathers `IInitializeAwake` and `IInitializeStart` components with `GetComponentsInChildren(includeInactive: true)`.
2. Sorts each list by priority **descending** (higher runs first).
3. `await`s every `InitializeAwake(ct)` one after another, **then** every `InitializeStart(ct)`.

So all Awake-phase work completes before any Start-phase work begins, and each call fully finishes (including `await`s) before the next begins.

```csharp
public interface IInitializeAwake { int AwakePriority { get; set; }  Awaitable InitializeAwake(CancellationToken ct); }
public interface IInitializeStart { int StartPriority { get; set; }  Awaitable InitializeStart(CancellationToken ct); }
```

### Implementing

```csharp
using System.Threading;
using UnityEngine;
using Giana.UnityUtils;

public class SaveSystem : MonoBehaviour, IInitializeAwake, IInitializeStart
{
    [field: SerializeField] public int AwakePriority { get; set; } = 100;  // runs early
    [field: SerializeField] public int StartPriority { get; set; } = 0;

    public async Awaitable InitializeAwake(CancellationToken ct)
    {
        await LoadFromDiskAsync(ct);           // other objects wait for this
    }

    public async Awaitable InitializeStart(CancellationToken ct)
    {
        // all Awake-phase work of the whole scene is finished here
        await Awaitable.NextFrameAsync(ct);
    }
}
```

Built-in implementers: `MonoSingleton<T>` (`IInitializeAwake`, so `ObjectPoolManager` too) and `MenuEventSystemHandler` (`IInitializeAwake`).

Rules and pitfalls:

- Only objects **inside the scene's hierarchy** are scanned. Objects already in the `DontDestroyOnLoad` scene (e.g. the already-initialized singletons) are not re-initialized on later scene loads.
- Inactive objects are included. Their `InitializeAwake` is called even though Unity's own `Awake` has not run yet; design for that (it is why menus are kept inactive in the scene).
- Exceptions abort the pass: in the Editor the error is logged, the overlay is hidden and (if *Pause Editor On Error*) the Editor is paused.
- Honour the `CancellationToken`; it is cancelled when the manager is destroyed.

---

## Changing scenes

```csharp
bool started = await LoadingManager.Instance.TransitionToSceneAsync(
    "Level_01",
    useOverlay: true,            // default true; ignored if the TARGET scene is bypassed
    ct: destroyCancellationToken // optional; linked with the manager's lifetime token
);
```

Returns `false` (and does nothing) if another transition is already running.

Sequence:

1. Fade overlay in (if used).
2. `LoadSceneAsync(name, Additive)` with `allowSceneActivation = false`; waits until progress ≥ 0.9, then activates.
3. `SetActiveScene(newScene)`, then unloads the previous active scene.
4. Runs the **initialization pass** on the new scene.
5. Waits 2 frames, fades the overlay out.
6. `finally`: `OnInitializationCompleted` fires.

Scenes are loaded **additively** and the previous one unloaded, so the persistent `LoadingManager` and any `DontDestroyOnLoad` objects are untouched. Scene names must be in *Build Settings*.

---

## Inspector (`LoadingManager`)

| Field | Meaning |
|---|---|
| Overlay | `LoadingOverlayController` (set in the prefab) |
| Bypassed Scenes | Scene **names** that skip the overlay on startup and on transitions to them |
| Show Debug Logs | Editor-only logs of each initialized system (green) |
| Pause Editor On Error | Pauses the Editor when initialization throws |

## `LoadingOverlayController`

| Member | Description |
|---|---|
| `SetInstant(bool visible)` | Sets alpha to 1/0 immediately and toggles the image GameObject |
| `FadeInAsync(ct)` / `FadeOutAsync(ct)` | Fade to opaque / transparent over *Fade Duration* (default 0.4 s); fade-out then deactivates the image |
| `FadeAlpahAsyn(alpha, ct)` | Generic fade to a target alpha (note the original method name spelling) |

Customize the look by editing the `Panel` Image in `MasterLoadingManager.prefab` (colour, sprite, add a spinner as a child). The fade uses scaled `Time.deltaTime`.

## Customizing the master prefab

`BootStrapper` loads by the resource name **`MasterLoadingManager`**. To override it, create your own prefab with that name in any `Resources` folder of your project, but keep a `LoadingManager` and `LoadingOverlayController` on it. If no prefab is found, nothing is spawned (set `showDebugLogs` in `BootStrapper` to see why).

## Using it together with the UI module

Keep static menus **inactive** in the scene. `MenuEventSystemHandler.InitializeAwake` caches its buttons' scales and then activates the menu. See [UI/README.md](../UI/README.md).
