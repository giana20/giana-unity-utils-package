# UI Menu System — Documentation

A small set of scripts for gamepad / keyboard / mouse / touch menu navigation in Unity (uGUI), with hover/selection animations, a sound hook, and staggered enter/exit animations.

It comes in two flavours that share the same idea:

| | Static menu | Dynamic menu |
|---|---|---|
| Use it for | Fixed menus: main menu, pause menu, settings | Menus whose entries come from data: level select, inventory, save slots, shop |
| Handler | `MenuEventSystemHandler` | `DynamicEventSystemHandler` |
| Buttons come from | The Inspector | Code, via `UIBuilder<T>` |
| Enter/exit animation | None built in | Yes, via `IUITransition` |

---

## 1. Contents

| File | Role |
|---|---|
| `IUITransition.cs` | Interface for enter/exit animations of dynamic items |
| `UIBuilder.cs` | Abstract `UIBuilder<T>`: spawns, locks, animates and destroys the items of a dynamic menu |
| `DynamicEventSystemHandler.cs` | MonoBehaviour on the dynamic menu: focus, hover scale, sound, runtime registration of selectables |
| `MenuEventSystemHandler.cs` | MonoBehaviour for static menus: same features, fixed list of selectables |
| `FeelUITransition.cs` | `IUITransition` driven by Feel's `MMF_Player` (only compiled with `FEEL_ENABLED`) |
| `MobileUIOnly.cs` | Shows an object only when a touchscreen is present |
| `Samples/` | A working demo of both menus (see `Samples/SampleSetup.md`) |

```
                    ┌──────────────────────────┐
  your code ───────►│  UIBuilder<T> (plain C#) │──spawns──► item prefabs (Selectable + IUITransition)
 (ShowUI/HideUI)    └────────────┬─────────────┘
                                 │ AddSelectable / InitSelectable
                                 ▼
                    ┌──────────────────────────┐
                    │ DynamicEventSystemHandler│  hover scale, sound, focus restore
                    └──────────────────────────┘

  Static menu:      MenuEventSystemHandler  (same job, selectables assigned in the Inspector)
```

---

## 2. Requirements and setup

1. **Unity 6** (`MenuEventSystemHandler` uses `Awaitable`).
2. **Input System package**, and the EventSystem must use *Input System UI Input Module*.
3. **TextMeshPro** (only for the sample).
4. **`IInitializeAwake`** — `MenuEventSystemHandler` implements this interface from your project. It is *not* part of this folder. Something must call `InitializeAwake()` on the handler before it is used (see 3.3). The sample includes an optional `SampleAwakeBootstrapper`; if your project already has its own, delete the sample one.
5. **Optional Integrations**
   The core menu system works completely without third-party dependencies, utilizing coroutines and `CanvasGroup` fading as fallbacks. However, you can enhance it by installing DOTween or Feel (MoreMountains).
   - **DOTween**: Assign the `DOTweenMenuAnimator` in the Inspector to use DOTween for hover scales instead of the default coroutine lerp.
   - **Feel**: Assign the `FeelUITransition` on your items to use `MMF_Player` feedbacks for entrance/exit animations.
   
   *Dependency Detection:* GianaUtils will automatically detect these packages and compile their integration assemblies.
   
   **IMPORTANT: DOTween ASMDEF**
   DOTween ships without an Assembly Definition (`.asmdef`) by default. In order for GianaUtils to link against it, you must generate one. Go to **Tools > Demigiant > DOTween Utility Panel** and click **Create ASMDEF**.

6. Drop the `UIMenuSystem` folder anywhere under `Assets/`. There is no assembly definition, because `IInitializeAwake` lives in your main assembly. If you add an asmdef later, put it and `IInitializeAwake` in assemblies that can see each other.
7. In the Inspector, assign **Navigate Reference** to the `UI/Navigate` action of the *same* Input Actions asset that your EventSystem's UI Input Module uses. If it is a different asset whose action is never enabled, the "restore focus" feature will not fire.

---

## 3. Static menu (`MenuEventSystemHandler`)

### 3.1 What it does
- Adds an `EventTrigger` to each listed selectable and reacts to Select / Deselect / PointerEnter / PointerExit.
- On select: plays the sound event and scales the object up. On deselect: scales back to its original size.
- Mouse hover selects the button under the pointer; leaving clears the selection. If you then press a navigation key and nothing is selected, focus is restored to the last selected button.
- Focuses `First Selected` one frame after the object is enabled.

### 3.2 Inspector fields

| Field | Meaning |
|---|---|
| Awake Priority | Used by your `IInitializeAwake` bootstrapper for ordering (higher first) |
| Selectables | Every button/toggle/slider of this menu. **Anything not in this list gets no hover animation, sound or focus restore** |
| First Selected | Focused when the menu is enabled. **Required** (null causes an exception) |
| Navigate Reference | `UI/Navigate` action. **Required** (null causes an exception) |
| Selected Animation Scale | Hover/selection scale multiplier (1.1 = +10%) |
| Scale Duration | Seconds of the scale tween |
| Animation Exclusion | Objects that should not scale (e.g. sliders, or objects whose size is driven elsewhere) |
| Sound Event | `UnityEvent` fired on every select. Hook your audio manager here |

### 3.3 Setup steps
1. Create the menu root object (e.g. `MainMenu`) under your Canvas and add the buttons as children.
2. Add `MenuEventSystemHandler` to the root and fill the fields above.
3. **Leave the root GameObject inactive in the scene.** `OnEnable` reads the cached scales, which only exist after `InitializeAwake()` ran. Your bootstrapper calls `InitializeAwake()`, which caches the scales and then activates the object.
4. To show/hide it later, just `SetActive(true/false)` the root. Each time it becomes active, focus returns to `First Selected`.
5. Hook button `OnClick` events as usual.

---

## 4. Dynamic menu (`UIBuilder<T>` + `DynamicEventSystemHandler`)

### 4.1 Step by step
1. **Menu object** (e.g. `LevelMenu`): add `DynamicEventSystemHandler`. A `CanvasGroup` is added automatically by the builder if missing.
2. **Item prefab**: see 4.3.
3. **Items parent**: a child of the menu with a Layout Group; spawned items are put here.
4. **Persistent buttons** (Close, Back, …): normal buttons inside the menu. Add them to the handler's *Persistent Selectables* list so they get the same hover/sound.
5. **Write a builder** by inheriting `UIBuilder<T>`:

```csharp
public class SampleLevelMenu : UIBuilder<SampleItem>
{
    public SampleLevelMenu(Selectable prefab, DynamicEventSystemHandler menu, Transform parent)
        : base(prefab, menu, parent) { }

    protected override Selectable BuildUIComponent(SampleItem item)
    {
        Selectable s = UnityEngine.Object.Instantiate(_UISelectablePrefab, _selectablesParent);
        s.GetComponent<SampleItemButton>().Bind(item, SelectItem);   // <-- wire the click!
        return s;
    }

    protected override void OnItemSelected(SampleItem item)
    {
        // your game logic ...
        HideUI();   // closing is YOUR decision, see 4.5
    }
}
```

6. **Create and use it from a MonoBehaviour that stays active** (not on the menu object, because the builder deactivates the menu object):

```csharp
_builder = new SampleLevelMenu(prefab, menuHandler, itemsParent);   // constructor deactivates the menu
_builder.ShowUI(items);    // open
_builder.HideUI();         // close
```

### 4.2 `DynamicEventSystemHandler` fields

| Field | Meaning |
|---|---|
| Persistent Selectables | Buttons that always exist in the menu (Close, Back…) |
| First Selectable | Focus target when the menu opens. If empty, the first spawned item is used |
| Navigate Reference | `UI/Navigate` action (optional here, null is handled) |
| Selected Animation Scale / Scale Duration | Hover scale |
| Sound Event | Fired on every select |

Runtime API (called by `UIBuilder`, but usable directly): `AddSelectable`, `ClearSelectable`, `InitSelectable`, `SetFirstSelected`.

### 4.3 The item prefab (important rules)
- **Root** must have the `Selectable` (usually `Button`) **and** the `IUITransition` component. `UIBuilder` looks the transition up on the same GameObject. No transition = the item pops in and out instantly.
- The handler scales the **root** on hover. Do the enter/exit animation on a **child** (the sample calls it `Visual`), never on the root. Otherwise the handler may cache a wrong "original scale" (for example a zero scale, if the enter animation had already shrunk the root) and the item shrinks to nothing after the first deselect.
- Keep the root size under the control of a Layout Group (`LayoutElement` for the height).
- Button navigation: `Automatic` works well for lists.

### 4.4 Lifecycle

```
ShowUI(items)
  ├─ OnMenuOpened event
  ├─ menu.SetActive(true)
  └─ SpawnItems(items)
        ├─ lock menu (CanvasGroup interactable/blocksRaycasts = false)
        ├─ ClearSelectable()
        ├─ for each item: BuildUIComponent → AddSelectable → PlayEnterAnimation(i * 0.15s)
        ├─ InitSelectable()                     (listeners + cached scales)
        └─ after (lastDelay + 0.5s): unlock + SetFirstSelected()

SelectItem(item)       ← call from the item's click
  ├─ ignored while locked
  ├─ lock
  └─ OnItemSelected(item)   ← your code; stays locked until you close or unlock

HideUI()  = HideItems(→ FinalizeCloseMenu)
HideItems(onComplete)
  ├─ lock
  ├─ PlayExitAnimation on every item
  └─ when the LAST one reports completion: DestroyItems() → onComplete()
FinalizeCloseMenu: menu.SetActive(false) → OnMenuClosed event
```

### 4.5 `UIBuilder<T>` API

| Member | Notes |
|---|---|
| `ShowUI(T[] items)` | Opens the menu and spawns the items |
| `SpawnItems(T[] items)` | Builds items. **Does not remove existing ones.** To refresh an open menu use `HideItems(() => SpawnItems(newItems))` |
| `SelectItem(T item)` | Connect item clicks to this. Ignored while the menu is locked |
| `HideUI()` | Exit animations → destroy items → deactivate menu → `OnMenuClosed` |
| `HideItems(Action onComplete)` | Exit animations and destroy items, menu stays open |
| `DestroyItems()` | Immediate destroy, no animation |
| `OnMenuOpened` / `OnMenuClosed` | Events. Handy to hide/show the previous menu |
| `BuildUIComponent(T)` | Abstract. Create and fill one selectable |
| `OnItemSelected(T)` | Abstract. React to a choice. Note: `SelectItem`'s doc comment says it hides the UI, but it does **not**; closing is up to this method |

---

## 5. Transitions (`IUITransition`)

```csharp
public interface IUITransition
{
    void PlayEnterAnimation(float delay);              // delay = stagger, in seconds
    void PlayExitAnimation(Action onAnimationComplete); // MUST call the callback exactly once
}
```

Rules for any implementation:
1. Same GameObject as the `Selectable`.
2. `PlayEnterAnimation` should put the visual in its **hidden state immediately**, then wait `delay`, so items don't flash before their turn.
3. `PlayExitAnimation` must invoke the callback exactly once when finished. The builder destroys everything after the last callback; a missing callback leaves the menu locked forever.
4. Use unscaled time if the menu must work while paused (`Time.timeScale = 0`).
5. Animate a child, not the root (see 4.3).

Available implementations:
- `DefaultUITransition` (in `UI/`): no dependencies; fade + slide of the `Visual` child using coroutines.
- `SampleUITransition` (in `Samples/`): fade + scale + slide of the `Visual` child.
- `FeelUITransition` (in `Integration/Feel/`): assign two `MMF_Player` components (enter / exit). The feedbacks must target the `Visual` child.

Example: a minimal DOTween transition.

```csharp
using System;
using DG.Tweening;
using UnityEngine;

public class DOTweenUITransition : MonoBehaviour, IUITransition
{
    [SerializeField] private CanvasGroup _group;   // on a child, not on the root

    public void PlayEnterAnimation(float delay)
    {
        _group.alpha = 0f;
        _group.DOFade(1f, 0.3f).SetDelay(delay).SetUpdate(true);
    }

    public void PlayExitAnimation(Action done)
    {
        _group.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => done?.Invoke());
    }
}
```

---

## 6. `MobileUIOnly`

Put it on the root of on-screen controls (virtual joystick, touch buttons). At `Start` the object stays active if `Touchscreen.current != null`, otherwise it deactivates itself.

Limits: it only checks once; laptops with a touchscreen count as touch devices; the Device Simulator in the Editor creates a touchscreen. To react to devices appearing or disappearing, use this variant on an always-active object and toggle a child:

```csharp
public class MobileUIOnly : MonoBehaviour
{
    [SerializeField] private GameObject _target;   // the controls to show/hide

    private void Start()
    {
        Apply();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDestroy() => InputSystem.onDeviceChange -= OnDeviceChange;

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Touchscreen) Apply();
    }

    private void Apply() => _target.SetActive(Touchscreen.current != null);
}
```

---

## 7. How to change things

**Hover animation (scale, duration, exclusions)** — Inspector fields on the handler. For a different effect (colour, rotation, outline), override `OnSelect` / `OnDeselect` (both are `public virtual`) in a subclass. `MenuEventSystemHandler` fields are `protected`, so subclassing works directly; in `DynamicEventSystemHandler` the animation fields are `private` — change them to `protected` first.

**Stagger and unlock timing** — in `UIBuilder.SpawnItems`: `i * 0.15f` is the stagger between items, `+ 0.5f` is the time reserved for the last item's enter animation. If your enter animation lasts longer than 0.5 s, raise it, otherwise the menu unlocks while items are still animating. Make them `protected virtual` properties if you want per-builder values.

**Keep the menu open after a selection** — `SelectItem` locks the menu and nothing unlocks it except `SpawnItems`. Add this to `UIBuilder` and call it from `OnItemSelected`:

```csharp
protected void UnlockMenu()
{
    _menuCanvasGroup.interactable = true;
    _menuCanvasGroup.blocksRaycasts = true;
    _menu.SetFirstSelected();
}
```
(`_menuCanvasGroup` is currently `private`; the method lives inside `UIBuilder`, so it can use it.)

**Back / Cancel button** — subscribe to your `UI/Cancel` action in the controller:

```csharp
_cancel.action.performed += _ => { if (_isOpen) _builder.HideUI(); };
_builder.OnMenuOpened += () => _isOpen = true;
_builder.OnMenuClosed += () => _isOpen = false;
```
Don't call `HideUI()` twice in a row: the second call replays the exit animations and fires `OnMenuClosed` twice. `_isOpen` guards against it (set it to false right when you start closing if you need to be stricter).

**Refresh items while open** — `HideItems(() => SpawnItems(newItems))` (see `SampleMenuDemo.SwapLevelList`). Focus jumps to the first item once the new items unlock.

**Sound** — drag your audio manager method into *Sound Event*. It fires on every select, including the automatic first focus when a menu opens; if that is unwanted, guard it in your audio method (e.g. ignore calls during the first frame) or override `OnSelect`.

**Different selection data / several builders** — one builder class per menu type; they can share the same prefab. Each builder needs its own `DynamicEventSystemHandler` menu object.

**Another animation library** — implement `IUITransition` (section 5) or inherit from `MenuAnimatorBase`. `UIBuilder` and the handlers never touch the animation library directly.

---

## 8. Changes made to the original scripts in this package

| File | Change | Why |
|---|---|---|
| `DynamicEventSystemHandler.cs` | `#if DOTWEEN_ENBLED` → `#if DOTWEEN_ENABLED` | The typo skipped `using DG.Tweening`, so the file failed to compile as soon as `DOTWEEN_ENABLED` was defined |
| `DynamicEventSystemHandler.cs` | `InitSelectable` registers each selectable only once (`_initialized` set) | `UIBuilder.SpawnItems` calls it every time; persistent buttons got a new set of listeners each call (sound played several times) |
| `DynamicEventSystemHandler.cs` | `ClearSelectable` also prunes destroyed selectables from the internal caches | Avoids slow growth of the scale dictionary after many refreshes |
| `UIBuilder.cs` | Added a coroutine fallback for the unlock when `DOTWEEN_ENABLED` is not defined | Without DOTween the menu stayed locked forever (`interactable = false`) |

Everything else is untouched.

---

## 9. Known limitations (not changed)

- **Closing too early**: the DOTween unlock callback is not cancelled. If a menu is closed by code within the first ~0.5 s + stagger after opening (the player cannot, the menu is locked), the late callback runs `SetFirstSelected` on an inactive menu and Unity logs "Coroutine couldn't be started because the game object is inactive".
- **`EventTrigger` and scroll views**: the handlers add an `EventTrigger` to every selectable. It also implements drag events, so inside a `ScrollRect` it can swallow drags and the list will not scroll by dragging. Fix: forward the events or replace the `EventTrigger` with a small component implementing only `ISelectHandler`, `IDeselectHandler`, `IPointerEnterHandler`, `IPointerExitHandler`.
- **Hover scale math**: selection scales from the *current* scale (`localScale * 1.1`), not from the cached original. Re-selecting mid-animation gives a slightly smaller target. Deselect always returns to the cached original.
- **`MenuEventSystemHandler.OnPointerEnter`** has no null check (the dynamic version does). It only matters if a pointer-enter target has no `Selectable` above or below it.
- **`MenuEventSystemHandler.OnDeselect`** stores its tween in `_scaleUpTween` (a naming quirk; harmless).
- **Persistent selectables of the dynamic handler** only get their listeners at the first `InitSelectable` (the first `SpawnItems`), so they react to hover/sound only after the menu was opened once.
- **`FeelUITransition`** has no null checks for the two players; assign both.

---

## 10. Troubleshooting

| Symptom | Likely cause |
|---|---|
| `DynamicEventSystemHandler` does not compile with `DOTWEEN_ENABLED` | You are using the original file with the `ENBLED` typo; use the one in this package |
| Dynamic menu opens but nothing can be clicked | Menu still locked: no `DOTWEEN_ENABLED` and the original `UIBuilder` (no fallback), an exit/enter animation that never calls its callback, or `OnItemSelected` neither closed nor unlocked the menu |
| Clicking an item does nothing | `BuildUIComponent` did not connect the click to `SelectItem`, or the menu is still locked during the enter animation |
| Items appear twice after refreshing | `SpawnItems` was called without `HideItems` / `DestroyItems` first |
| Items shrink or vanish after the first hover | The enter/exit animation drives the root scale; move it to a child |
| `KeyNotFoundException` in `MenuEventSystemHandler.OnEnable` | The object was active before `InitializeAwake()` ran. Keep it inactive in the scene and make sure your bootstrapper initializes it |
| `NullReferenceException` in `SelectAfterDelay` / `OnEnable` of the static menu | `First Selected` or `Navigate Reference` not assigned |
| No hover / sound on a button | It is not in *Selectables* (static) or in *Persistent Selectables* (dynamic) |
| Keyboard/gamepad does nothing after using the mouse | `Navigate Reference` points to an action that is not enabled (use the action asset of your UI Input Module) |
| Sound plays several times per select | Using the original `DynamicEventSystemHandler` (duplicate listeners), or `InitializeAwake()` was called twice on a static menu |
| No hover scale at all | `DOTWEEN_ENABLED` is not defined |
| Menu controller stops working when the menu opens | The controller sits on the menu object, which the builder deactivates; move it to an always-active object |
