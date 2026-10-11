# UI

Menu navigation and animation for uGUI with the Input System: keyboard, mouse, gamepad and touch share one selection model. Two flavours:

| | Static menu | Dynamic menu |
|---|---|---|
| Use for | Main / pause / settings | Level select, inventory, shop, save slots |
| Handler | `MenuEventSystemHandler` | `DynamicEventSystemHandler` |
| Buttons come from | Inspector list | Code via `UIBuilder<T>` + a prefab |
| Enter/exit animation | n/a | `IUITransition` on each item |

> Full reference, lifecycle diagrams, troubleshooting: [Documentation/UIMenuSystem.md](./Documentation/UIMenuSystem.md). A working demo is in `Samples/Scripts/S_UISample.unity`.

## Files

| File | Purpose |
|---|---|
| `EventSystemHandlerBase` | Shared logic: hover/selection scale, sound event, focus restore |
| `MenuEventSystemHandler` | Static menus (implements `IInitializeAwake`) |
| `DynamicEventSystemHandler` | Dynamic menus (runtime registration of selectables) |
| `UIBuilder<T>` | Abstract builder: spawns items, locks input, animates, destroys |
| `IUITransition` | Enter/exit animation contract |
| `DefaultUITransition` | Dependency-free coroutine fade + slide |
| `MenuAnimatorBase`, `DefaultMenuAnimator` | Selection scale animation abstraction + coroutine default |

---

## Base behaviour (both handlers)

`EventSystemHandlerBase` adds an `EventTrigger` to each registered `Selectable` and listens to Select, Deselect, PointerEnter, PointerExit.

- **Select**: invokes *Sound Event*, scales the selectable to `originalScale × Selected Animation Scale`.
- **Deselect**: scales back to the cached original.
- **Pointer enter** selects the hovered selectable; **pointer exit** clears selection. If you then press a navigation key with nothing selected, the *last selected* one is re-selected (needs `Navigate Reference`).
- Animation is delegated to a `MenuAnimatorBase`: a component on the same GameObject, otherwise a `DefaultMenuAnimator` is added automatically. Add `DOTweenMenuAnimator` to use DOTween instead ([Integration](../Integration/README.md)).

| Field | Meaning |
|---|---|
| Navigate Reference | `InputActionReference` to `UI/Navigate`. Must be from the **same asset** your EventSystem's UI Input Module uses (the package ships `GianaUtilsActions.inputactions`) |
| Selected Animation Scale | Multiplier, default 1.1 |
| Scale Duration | Seconds, default 0.1 (unscaled time, works while paused) |
| Sound Event | `UnityEvent` fired on every select, including the automatic first focus |
| Animator | Optional `MenuAnimatorBase` override |

Only selectables **registered with the handler** get animation/sound/focus restore.

---

## Static menu

1. Create the menu root under a Canvas, add buttons as children.
2. Add `MenuEventSystemHandler` to the root. Fill **Selectables** (every button), **First Selected**, **Navigate Reference**, optional **Animation Exclusion** (e.g. sliders).
3. **Leave the root GameObject inactive in the scene.** `LoadingManager` calls `InitializeAwake`, which registers listeners, caches scales and then activates the object. Activating earlier makes `OnEnable` run before scales are cached.
4. Show/hide later with `SetActive`; each time it enables, focus returns to *First Selected* (after one frame).
5. Set **Awake Priority** if ordering between several initializables matters.

Requires the [SceneInitialization](../SceneInitialization/README.md) flow (it is the thing that calls `InitializeAwake`).

---

## Dynamic menu

### 1. Scene objects

- **Menu object** with `DynamicEventSystemHandler` (a `CanvasGroup` is added by the builder if missing).
- **Items parent**: child with a Vertical/Horizontal Layout Group.
- **Persistent buttons** (Close, Back…): children of the menu, added to the handler's *Persistent Selectables*.

### 2. Item prefab (rules that matter)

- **Root**: a `Selectable` (usually `Button`) + an `IUITransition` component + a `LayoutElement`.
- The handler scales the **root** on hover, so the enter/exit animation must act on a **child** (the sample names it `Visual`). Animating root scale makes the handler cache a wrong original scale and items shrink after the first deselect.
- See `Samples/Scripts/SampleItemPrefab.prefab`.

### 3. Data type + builder

```csharp
[Serializable]
public class LevelInfo { public string Title; public Sprite Icon; }

public class LevelMenu : UIBuilder<LevelInfo>
{
    public event Action<LevelInfo> LevelChosen;

    public LevelMenu(Selectable prefab, DynamicEventSystemHandler menu, Transform parent)
        : base(prefab, menu, parent) { }

    protected override Selectable BuildUIComponent(LevelInfo item)
    {
        var s = UnityEngine.Object.Instantiate(_UISelectablePrefab, _selectablesParent);
        s.GetComponent<LevelButton>().Bind(item, SelectItem);   // you must wire the click to SelectItem
        return s;
    }

    protected override void OnItemSelected(LevelInfo item)
    {
        LevelChosen?.Invoke(item);
        HideUI();      // SelectItem locks the menu; closing is your decision
    }
}
```

### 4. Controller (on an always-active object)

```csharp
public class MenuController : MonoBehaviour
{
    [SerializeField] DynamicEventSystemHandler _menu;
    [SerializeField] Selectable _itemPrefab;
    [SerializeField] Transform _itemsParent;
    [SerializeField] LevelInfo[] _levels;
    LevelMenu _builder;

    void Awake()
    {
        _builder = new LevelMenu(_itemPrefab, _menu, _itemsParent); // constructor deactivates the menu
        _builder.OnMenuOpened += () => mainMenu.SetActive(false);
        _builder.OnMenuClosed += () => mainMenu.SetActive(true);
    }

    public void Open()  => _builder.ShowUI(_levels);
    public void Close() => _builder.HideUI();
}
```

Do **not** put the controller on the menu object: the builder deactivates it.

### `UIBuilder<T>` API

| Member | Notes |
|---|---|
| `ShowUI(T[])` | Fires `OnMenuOpened`, activates menu, `SpawnItems` |
| `SpawnItems(T[])` | Locks, builds, registers, plays enter animations staggered by 0.15 s per item, unlocks after `lastDelay + 0.5 s` and focuses. **Does not remove old items** |
| `SelectItem(T)` | Click target. Ignored while locked; locks and calls `OnItemSelected` |
| `HideItems(Action)` | Exit animations on all items; after the last callback destroys them and runs the callback (menu stays open). Use to refresh: `HideItems(() => SpawnItems(next))` |
| `HideUI()` | `HideItems` then deactivate menu and fire `OnMenuClosed` |
| `DestroyItems()` | Immediate destroy |
| `BuildUIComponent(T)` / `OnItemSelected(T)` | Abstract, you implement |

### `DynamicEventSystemHandler` API

`AddSelectable`, `ClearSelectable`, `InitSelectable` (registers listeners + caches scales for dynamic and persistent selectables), `SetFirstSelected`. *First Selectable* overrides the default focus target (first spawned item).

---

## Transitions (`IUITransition`)

```csharp
void PlayEnterAnimation(float delay);                // delay = stagger. Hide immediately, then wait
void PlayExitAnimation(Action onAnimationComplete);  // MUST invoke the callback exactly once
```

- Must live on the **same GameObject as the Selectable** (`UIBuilder` uses `TryGetComponent`). No transition means the item pops in/out instantly.
- A missing exit callback leaves the menu locked forever. Use unscaled time if menus must work during pause.
- Implementations: `DefaultUITransition` (needs a `CanvasGroup` on the root; fades and slides the root's anchored position, so it is best with items not driven by a layout group's position), `Samples/SampleUITransition` (child `Visual`, fade+scale+slide), `FeelUITransition` ([Integration](../Integration/README.md)). Writing a DOTween one is ~10 lines (see the documentation).

## Tuning

Stagger (`i * 0.15f`) and unlock delay (`+ 0.5f`) are constants in `UIBuilder.SpawnItems`; raise the latter if your enter animation lasts longer than 0.5 s. For custom hover effects subclass a handler and override `OnSelect`/`OnDeselect` (both `public virtual`), or write a `MenuAnimatorBase`.

## Common problems

| Symptom | Cause |
|---|---|
| Nothing clickable in dynamic menu | Menu still locked (enter animation running, callback never invoked, or `OnItemSelected` didn't close/unlock) |
| `KeyNotFoundException` in static menu `OnEnable` | Object was active before `InitializeAwake` ran; keep it inactive in the scene |
| No hover/sound on a button | Not in *Selectables* / *Persistent Selectables* |
| Keys do nothing after using the mouse | `Navigate Reference` from a different, disabled action asset |
| Items doubled after refresh | `SpawnItems` without `HideItems`/`DestroyItems` first |
