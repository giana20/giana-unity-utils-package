# GianaUtils

Unity 6 utility package (`com.giana.unityutils`) providing:

- **Core**: singleton base classes and a full-featured object pool.
- **SceneInitialization**: a bootstrapper + `LoadingManager` that loads scenes asynchronously behind a fade overlay and runs an *ordered, awaitable* initialization pass on every scene.
- **TooltipSystem**: dynamic cursor-following tooltip system with quadrant pivot flipping, configurable delays, and TMP formatting.
- **UI**: gamepad/keyboard/mouse/touch menu navigation (static and data-driven menus), selection animations, and staggered enter/exit transitions.
- **DraggableList**: drag-to-reorder lists and grids with cross-list transfer, drag-out deletion with optional confirmation, drop zones (buy button, enemy, discard pile) and pluggable animation.
- **Integration**: optional adapters for DOTween and Feel (MoreMountains).
- **Editor**: automatic detection of optional dependencies and scripting-define management.

Namespaces: `Giana.UnityUtils` (everything), `Giana.UnityUtils.DoTween`, `Giana.UnityUtils.FEEL`, `Giana.UnityUtils.Editor`.

---

## Requirements

| Requirement | Notes |
|---|---|
| Unity **6000.6+** | Uses `Awaitable`, `EntityId` (`Object.GetEntityId()`), `Rigidbody.linearVelocity` |
| `com.unity.inputsystem` | Menus read the `UI/Navigate` action; the EventSystem must use *Input System UI Input Module* |
| `com.unity.ugui` | uGUI + TextMeshPro (the `GianaUtils` assembly references `Unity.TextMeshPro`) |
| DOTween *(optional)* | Needs an `.asmdef`, see [Integration](./Integration/README.md) |
| Feel *(optional)* | Detected via the `MoreMountains.Tools` assembly |

## Installation

Package Manager → **Add package from git URL…**:

```
https://github.com/giana20/giana-unity-utils-package.git
```

or add to `Packages/manifest.json`:

```json
"com.giana.unityutils": "https://github.com/giana20/giana-unity-utils-package.git"
```

## Package layout

```
GianaUtils.asmdef                 Main runtime assembly "GianaUtils"
GianaUtilsActions.inputactions    Ready-made Input Actions asset (Player + UI maps)
Core/                             MonoSingleton, LazyMonoSingleton, ObjectPoolManager
SceneInitialization/              BootStrapper, LoadingManager, LoadingOverlayController,
                                  IInitializeAwake, IInitializeStart, Resources/MasterLoadingManager.prefab
TooltipSystem/                    TooltipSystem, Tooltip, TooltipTrigger, Tooltip.prefab, showcase scene
UI/                               UIBuilder<T>, event-system handlers, transitions, animators, Samples/
DraggableList/                    DraggableList, DraggableItem, drag animators, removal confirmation
Integration/DoTween/              GianaUtils.DoTween.asmdef (define: GIANAUTILS_DOTWEEN)
Integration/Feel/                 GianaUtils.FEEL.asmdef   (define: GIANAUTILS_FEEL)
Editor/                           Dependency detector (Editor-only assembly)
```

## How the pieces fit together

```
Play / app start
   │
   ▼
BootStrapper  (RuntimeInitializeOnLoadMethod, BeforeSceneLoad)
   │  instantiates Resources/"MasterLoadingManager" (DontDestroyOnLoad)
   ▼
LoadingManager.Start()
   │  covers the screen with the overlay, then for the active scene:
   │    1. collects every IInitializeAwake / IInitializeStart under the scene roots (incl. inactive)
   │    2. awaits all InitializeAwake (priority desc), then all InitializeStart (priority desc)
   │    3. fades the overlay out
   ▼
Your objects are now initialized in a deterministic order:
   MonoSingleton<T>  ──┐  (become Instance in InitializeAwake)
   MenuEventSystemHandler ─┤
   your own managers ──┘
```

`MonoSingleton<T>` and `MenuEventSystemHandler` **depend on this flow**: they are initialized by `LoadingManager`, not by Unity's `Awake`. See each module README for details and pitfalls.

## 5-minute quick start

1. **Install** the package (above). Open *Tools → GianaUtils → Check Dependencies* once.
2. **Scene setup**: nothing to add for the loading system. `BootStrapper` spawns the `MasterLoadingManager` prefab automatically.
3. **Make a manager**:
   ```csharp
   using Giana.UnityUtils;

   public class AudioManager : MonoSingleton<AudioManager>
   {
       public override async Awaitable InitializeAwake(CancellationToken ct)
       {
           await base.InitializeAwake(ct);   // sets Instance + DontDestroyOnLoad
           // load banks, etc.
       }
   }
   ```
   Place it on a GameObject in the first scene.
4. **Change scene**:
   ```csharp
   await LoadingManager.Instance.TransitionToSceneAsync("Level_01");
   ```
5. **Pool objects**:
   ```csharp
   var bullet = ObjectPoolManager.Instance.Spawn(bulletPrefab, pos, rot);
   ObjectPoolManager.Instance.Despawn(bullet, 2f);
   ```
   (`ObjectPoolManager` is a `MonoSingleton`, so it must exist in the scene and be initialized by `LoadingManager`.)
6. **Menus**: open `UI/Samples/Scripts/S_UISample.unity` and read [UI/README.md](./UI/README.md).
7. **Draggable lists**: add `DraggableList` to a container with a Layout Group, give each item a `DraggableItem` plus one child holding its visuals, then run *Grab List Entries*. See [DraggableList/README.md](./DraggableList/README.md).
8. **Tooltips**: create a dedicated Canvas (no `GraphicRaycaster`, `Scale With Screen Size`), add `TooltipSystem`, drop `Tooltip.prefab` as a child, and attach `TooltipTrigger` to interactive objects. See [TooltipSystem/README.md](./TooltipSystem/README.md).

## Module documentation

| Module | Doc |
|---|---|
| Core | [Core/README.md](./Core/README.md) |
| SceneInitialization | [SceneInitialization/README.md](./SceneInitialization/README.md) |
| TooltipSystem | [TooltipSystem/README.md](./TooltipSystem/README.md) |
| DraggableList | [DraggableList/README.md](./DraggableList/README.md) |
| UI | [UI/README.md](./UI/README.md) (deep dive: [UI/Documentation/UIMenuSystem.md](./UI/Documentation/UIMenuSystem.md)) |
| Integration | [Integration/README.md](./Integration/README.md) |
| Editor | [Editor/README.md](./Editor/README.md) |

## License

MIT, see [LICENSE](./LICENSE).
