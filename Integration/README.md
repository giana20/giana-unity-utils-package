# Integration

Optional adapters for third-party assets. Each lives in its own assembly that is **only compiled when its scripting define is set**, so the package builds fine without these assets.

| Integration | Assembly | Define (`defineConstraints`) | Provides |
|---|---|---|---|
| DOTween | `GianaUtils.DoTween` | `GIANAUTILS_DOTWEEN` | `DOTweenMenuAnimator` |
| Feel (MoreMountains) | `GianaUtils.FEEL` | `GIANAUTILS_FEEL` | `FeelUITransition` |

The defines are added/removed automatically by the [Editor dependency detector](../Editor/README.md); you never set them by hand. Run *Tools → GianaUtils → Check Dependencies* after importing an asset.

---

## DOTween

### Setup

DOTween ships without an Assembly Definition, and an assembly can't reference one that has none. Generate it once:

**Tools → Demigiant → DOTween Utility Panel → Create ASMDEF**

If DOTween is present without asmdefs, the detector shows a dialog telling you exactly this and keeps `GIANAUTILS_DOTWEEN` off.

### `DOTweenMenuAnimator : MenuAnimatorBase`

Replaces the coroutine-based hover scale with DOTween.

- `AnimateSelect/AnimateDeselect`: `target.DOKill()` then `DOScale(toScale, duration).SetUpdate(unscaledTime)`.
- `Kill(target)`: kills tweens on the target, or the last stored select/deselect tweens when `target` is null.

Usage: add the component to the **same GameObject as your `MenuEventSystemHandler` / `DynamicEventSystemHandler`** (or drag it into the handler's *Animator* field). Without it, handlers fall back to `DefaultMenuAnimator`.

Item enter/exit animations are a separate concept (`IUITransition`); write one with DOTween if desired:

```csharp
public class DOTweenUITransition : MonoBehaviour, IUITransition
{
    [SerializeField] CanvasGroup _group;   // on a child "Visual", not the root
    public void PlayEnterAnimation(float delay) { _group.alpha = 0; _group.DOFade(1, .3f).SetDelay(delay).SetUpdate(true); }
    public void PlayExitAnimation(Action done)  { _group.DOFade(0, .2f).SetUpdate(true).OnComplete(() => done?.Invoke()); }
}
```

(Put it in your own project/assembly that references DOTween and `GianaUtils`.)

---

## Feel (MoreMountains)

### `FeelUITransition : MonoBehaviour, IUITransition`

Drives item enter/exit with two `MMF_Player` components.

| Field | Meaning |
|---|---|
| Enter Feedback | Played by `PlayEnterAnimation(delay)`; `InitialDelay` is set to `delay` for stagger |
| Exit Feedback | Played by `PlayExitAnimation`; its `Events.OnComplete` invokes the builder's callback once, then the listener is removed |

Setup on an item prefab root (next to the `Selectable`):

1. Add `FeelUITransition` and two `MMF_Player`s (can be on the same object).
2. Build the feedbacks so they target the **child `Visual`**, not the root (the handler owns the root's scale).
3. Assign Enter/Exit.

Notes: players are initialized lazily on first use. Assign **both** players; if the exit player is missing the callback is invoked immediately (no animation), if the enter one is missing nothing plays.

Detection: the Feel assembly `MoreMountains.Tools` must be loaded or its asmdef found.
