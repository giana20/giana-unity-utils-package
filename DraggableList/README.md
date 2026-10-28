# DraggableList

Drag-to-reorder lists and grids for uGUI: reorder by dragging, move items between lists, drag an item out to delete it (optionally with a confirmation). Works with mouse, touch and gamepad-driven pointers through the Input System UI module.

> **Not Unity's `ReorderableList`.** That class (`UnityEditorInternal`) is an editor-only IMGUI control for inspectors. It cannot be used at runtime in uGUI, so this module is a separate implementation, named `DraggableList` to avoid confusion.

## Files

| File | Role |
|---|---|
| `DraggableList.cs` | Container logic: reordering, cross-list transfer, removal, events |
| `DraggableItem.cs` | One entry. Lifts its visual out of the layout while dragged. Also works without a list |
| `DropZone.cs` | Drop target that triggers events: buy button, enemy, discard pile |
| `DragAnimatorBase.cs` | Animation abstraction (`Move`, `Placeholder`, `Removal` channels) |
| `DefaultDragAnimator.cs` | Dependency-free coroutine animator (unscaled time), added automatically |
| `RemovalConfirmationBase.cs` | Contract for a delete confirmation dialog |
| `RemovalConfirmationPopup.cs` | Ready-made yes/no popup |

Optional adapters: `DOTweenDragAnimator` and `FeelDragFeedback`, see [Integration](../Integration/README.md).

## How it works

```
[List container]  VerticalLayoutGroup / HorizontalLayoutGroup / GridLayoutGroup + DraggableList
   ├── Item   (DraggableItem + LayoutElement)  ◄── root = the slot / placeholder, stays in the layout
   │    └── Contents                           ◄── the visible part, lifted to the drag layer while dragging
   └── Item ...
```

1. The pointer moves further than *Drag Threshold* → `Contents` is reparented to the drag layer (root canvas by default) with its own override-sorting canvas, and the slot keeps its size.
2. **Linear layouts:** the slot swaps with a neighbour when the pointer crosses that neighbour's centre. **Grids:** the slot moves to the nearest cell.
3. Neighbours animate by offsetting their own `Contents` from the old position to the new one; they never leave their hierarchy, so masks and clipping keep working.
4. On release the visual slides back into its slot, or moves to another list, or is deleted.

## Setup

1. Container: a `RectTransform` with a Layout Group. Add `DraggableList`, assign the root canvas (auto-detected) and the container.
2. Item prefab: root with `DraggableItem` (a `LayoutElement` is added automatically) and **one child holding the visuals** (`Contents`). Put the `Image`/`Button`/text on `Contents`, not on the root.
3. Put items under the container and run the component's context menu **Grab List Entries** (⋮ menu in the Inspector), or call `AddItem` from code.
4. *(Optional)* Add a `DOTweenDragAnimator` next to the list for DOTween, or leave it empty for the default animator.

**Layout group requirements (linear lists):** *Control Child Size* on, *Child Force Expand* off along the main axis. Otherwise the slot cannot collapse smoothly.

## Inspector reference

| Section | Field | Meaning |
|---|---|---|
| Setup | Root Canvas, Container, Items, Arrangement | `Auto` picks *Grid* for a `GridLayoutGroup`, otherwise *Linear* |
| Animation | Animator, Swap Duration, Swap Ease | Empty animator → `DefaultDragAnimator` is added |
| Drag Layer | Drag Layer, Drag Sorting Order | Where lifted items live (default: root canvas, order 500) |
| Other Lists | Allow Transfer Out, Accept Incoming, Transfer Group | Items move between lists with the same group name (empty matches empty) |
| Removal Logic | Dragging Out Removes Entry, Distance Until Remove, Confirm Removal, Confirmation, Destroy On Remove | Distance is measured from the container's edge |
| Removal Animation | Remove Duration, Remove Ease | Scale to zero |

On `DraggableItem`: *Drag Threshold* (pixels), *Draggable* (toggle at runtime via `Draggable`), *Tag* and `Payload` (data for drop zones).

## Code

```csharp
[SerializeField] DraggableList _list;
[SerializeField] DraggableItem _prefab;

void OnEnable()  => _list.OrderChanged += OnOrderChanged;
void OnDisable() => _list.OrderChanged -= OnOrderChanged;

void OnOrderChanged(IReadOnlyList<DraggableItem> items)
{
    // read items in order and sync your data
}

void Add() => _list.AddItem(Instantiate(_prefab));
```

| Event | When |
|---|---|
| `OrderChanged(items)` | After a reorder, add, delete or transfer (fired on both lists for a transfer) |
| `ItemMoved(item, from, to)` | Live, each time the dragged item passes a neighbour |
| `ItemAdded(item)` | `AddItem` |
| `ItemRemoved(item)` | Deleted by drag-out or `RemoveItem`. The item is destroyed after the animation, don't keep references |
| `ItemTransferred(item, from, to)` | On the destination list |
| `DraggableItem.Lifted / Dropped` | Pointer lifts / releases the item |

Events are per instance, there are no static events.

## Drop zones (buy button, enemy, discard pile)

`DropZone` turns "release the item here" into an event. It works for items inside a list (a hand of cards that still reorders) and for **standalone items** with no list at all.

1. Add `DropZone` to the target and pick a **Mode**:
   - `UI`: the pointer is inside the zone's `RectTransform` (a button, a panel, a pile).
   - `World3D` / `World2D`: a ray from the pointer through the camera must hit a collider on the zone (enemies in the scene). Put zone colliders on their own layer and set *Layer Mask* so nothing else blocks the ray. Trigger colliders work.
2. Choose what happens to an accepted item: **Return Home** (it slides back) or **Remove** (it is consumed with the removal animation).
3. React with `ItemDropped` / the `On Item Dropped` UnityEvent. Read your data from `item.Payload`.

```csharp
void Start()
{
    _buyZone.Validator = item => _wallet.CanAfford((ShopEntry)item.Payload);
    _buyZone.ItemDropped += item =>
    {
        _wallet.Buy((ShopEntry)item.Payload);
        item.Remove();               // consume it; without this the zone's action is applied
    };
}
```

| Feature | How |
|---|---|
| Filter by kind | `Accepted Tags` on the zone, `Tag` on the item (empty list accepts everything) |
| Filter by rule | `Validator` (e.g. can the player afford it) |
| Hover feedback | `On Hover Valid` / `On Hover Invalid` / `On Hover End`, or `HoverChanged(item, state)`. A rejected zone shows *Invalid* and a drop there simply returns the item |
| Overlapping zones | `Priority`, highest wins. Only that zone is considered |
| Consume yourself | `item.Remove()` in a handler |

While an item hovers a valid zone, reordering and drag-out removal are paused so the list does not shuffle under it. Hand-of-cards example: the hand is a `DraggableList`, the discard pile a `DropZone` with *Remove*, the enemy a `World2D` zone with *Return Home* whose handler applies damage.

### Standalone items

Remove the list and fill the **Standalone** section on `DraggableItem` (root canvas, drag layer, return/remove timing). The item then lifts, follows the pointer, shows hover feedback, and returns home or is consumed. Use it for a single draggable card or a loot icon.

## Removal confirmation

Enable *Confirm Removal* and put a `RemovalConfirmationPopup` in the scene (assign *Confirm* / *Cancel* buttons and a `CanvasGroup`). It hides itself on start and renders above the drag layer (sorting 600). Assign it in the list, or leave the field empty to use the first one found. For a custom dialog derive from `RemovalConfirmationBase` and call exactly one of the two callbacks. While a confirmation is open, dragging is locked on that list; cancelling slides the item back.

## Custom animation

Derive from `DragAnimatorBase` (same idea as `MenuAnimatorBase`). Contract: `Play` calls `onUpdate` with the eased progress, always once with the final value, then `onComplete`; starting the same (owner, channel) again replaces the old one without calling its `onComplete`; use unscaled time; the animator GameObject must stay active.

## Notes and limitations

- **Scroll views:** a draggable item takes the drag from a parent `ScrollRect`. Keep a scroll area (or handle) outside the items, or scroll with the wheel/gamepad.
- `Contents` should not have its own Canvas on another sorting layer; if it has one it is used and restored afterwards.
- Items are matched to the layout by sibling order, other non-draggable children in the container are fine.
- Drop zones are only checked while an item is dragged; `World2D` needs the 2D physics module enabled (default).
- Cross-list moves are decided on drop (the target list is found by geometry, not raycasts). Items of different size in different lists reflow when the item settles.
- Placeholder and neighbour animations run on unscaled time.
- Drag sorting order (500) and popup (600) are below the `LoadingManager` overlay; keep your own canvases consistent.
