# TooltipSystem

A lightweight, cursor-following tooltip system for uGUI and 3D/physics objects in Unity 6 (`UnityEngine.InputSystem` + TextMeshPro).

The system keeps tooltips cleanly within screen bounds by calculating cursor screen quadrants and dynamically flipping pivots. It also supports automatic multiline text wrapping when exceeding configurable character limits, unscaled hover delays, and simple inspector or code-driven triggers.

> **Sample scene:** Check out [S_Tooltip_Showcase.unity](file:///home/giana/Unity/Packages/com.giana.giana-unity-utils/TooltipSystem/S_Tooltip_Showcase.unity) for a ready-to-test showcase containing UI buttons, icons, and 3D objects with tooltips.

---

## Files

| File | Type | Role |
|---|---|---|
| `TooltipSystem.cs` | Script (`MonoSingleton<TooltipSystem>`) | Global manager singleton; exposes `Show()` and `Hide()` methods |
| `Tooltip.cs` | Script (`MonoBehaviour`) | Follows pointer coordinates, calculates quadrant pivots, and controls TMP layout wrapping |
| `TooltipTrigger.cs` | Script (`MonoBehaviour`) | Attached to UI elements or 3D objects to trigger tooltips on hover with a configurable delay |
| `Tooltip.prefab` | Prefab (uGUI) | Default tooltip visuals (header, content, background panel, layout fitter) |
| `S_Tooltip_Showcase.unity` | Scene | Example showcase demonstrating UI and 3D collider setups |

---

## Recommended Setup

To ensure the best performance, visual presentation, and interaction fidelity, follow this setup:

```
[Tooltip Canvas]                      ◄── Screen Space - Overlay (Sort Order: 32767), Canvas Scaler, NO Graphic Raycaster
   ├── TooltipSystem (Script)         ◄── References child Tooltip component
   └── [Tooltip] (Prefab instance)    ◄── Tooltip.cs, Image, VerticalLayoutGroup, ContentSizeFitter, TMP fields
```

### Step 1: Create a Dedicated Tooltip Canvas
1. In your scene hierarchy, create a new Canvas (e.g., name it `Tooltip Canvas`).
2. Set **Render Mode** to **Screen Space - Overlay**.
3. Set **Sorting Order** to a high value such as `32767` (or higher than your other UI canvases) so tooltips always render above HUDs, modals, and menus.

### Step 2: Remove the Graphic Raycaster
- Select `Tooltip Canvas` and **remove / delete the `Graphic Raycaster` component**.
- **Why this is critical:** The tooltip follows the mouse pointer directly beneath the cursor. If the Tooltip Canvas has a `Graphic Raycaster`, the tooltip's background or text elements will intercept the mouse raycast. This triggers an `OnPointerExit` event on the underlying UI element or object, creating a flickering loop (show → raycast blocked → exit → hide → raycast unblocked → enter → show) and preventing clicks on buttons underneath.

### Step 3: Configure Canvas Scaler (Scale With Display)
1. Ensure the `Canvas Scaler` component is attached to `Tooltip Canvas`.
2. Set **UI Scale Mode** to **Scale With Screen Size**.
3. Set **Reference Resolution** to match your project UI reference (e.g., `1920 x 1080`).
4. Set **Screen Match Mode** to `Match Width Or Height` (typically slider value `0.5` or `0`).
- **Why:** Ensures font sizes, margins, and padding scale consistently across different aspect ratios and screen resolutions.

### Step 4: Add the TooltipSystem Component
1. Add the `TooltipSystem` component to `Tooltip Canvas` (or a manager object).
2. Because `TooltipSystem` inherits from `MonoSingleton<TooltipSystem>`, it is globally accessible via `TooltipSystem.Instance`.

### Step 5: Add the Tooltip Prefab
1. Drag the `Tooltip.prefab` from `TooltipSystem/` into the hierarchy as a child of `Tooltip Canvas`.
   - You can use the prefab directly, duplicate it to style your own visuals, or create a custom one with the same component structure.
2. In the inspector of `Tooltip Canvas`, drag the child `Tooltip` into the **Tooltip** field of `TooltipSystem`.
3. The prefab starts inactive; `TooltipSystem` will toggle its visibility when shown/hidden.

### Step 6: Add TooltipTrigger to Interactive Objects
On any GameObject that should display a tooltip when hovered:
1. Add the `TooltipTrigger` component.
2. Fill in the inspector fields:
   - **Header**: Optional title displayed in bold/larger font (leave empty to hide the header field entirely).
   - **Content**: The descriptive body text (supports multiline text).
   - **Delayed Showup Time**: Delay in seconds before displaying the tooltip (default `0.1s`).

---

## How It Works

### Dynamic Screen-Quadrant Pivots (`Tooltip.cs`)
The tooltip monitors the cursor position using the Unity Input System (`Pointer.current.position.ReadValue()`). It normalizes this coordinate against `Screen.width` and `Screen.height` into 4 quadrants:

| Cursor Quadrant | Applied Pivot | Offset Direction |
|---|---|---|
| **Top-Left** (`x < 0.5`, `y >= 0.5`) | `(-0.05, 1.05)` | Expands down and to the right |
| **Top-Right** (`x >= 0.5`, `y >= 0.5`) | `(1.05, 1.05)` | Expands down and to the left |
| **Bottom-Left** (`x < 0.5`, `y < 0.5`) | `(-0.05, -0.05)` | Expands up and to the right |
| **Bottom-Right** (`x >= 0.5`, `y < 0.5`) | `(1.05, -0.05)` | Expands up and to the left |

This ensures the tooltip never clips outside the visible screen edges, without needing complex bounding box clamps.

### Automatic Text Wrapping & Dynamic Sizing
- The tooltip prefab uses a `VerticalLayoutGroup` combined with a `ContentSizeFitter` set to `Preferred Size` on both axes.
- Short text fits snugly to the content width.
- When `header.Length` or `content.Length` reaches or exceeds `_characterWrapLimit` (default `50` or `80` in prefab), `Tooltip.cs` enables the attached `LayoutElement` (with `Preferred Width: 500`). This forces TextMeshPro to wrap text to multiple lines instead of expanding off the screen.

### UI vs. 3D Object Hover Handling
`TooltipTrigger` implements two detection mechanisms:
1. **uGUI Elements**: Implements `IPointerEnterHandler` and `IPointerExitHandler`.
   - Requires a UI Graphic (Image, Text, etc.) with **Raycast Target** enabled on an interactive canvas with a `GraphicRaycaster`.
2. **3D / 2D Collider Objects**: Implements `OnMouseEnter()` and `OnMouseExit()`.
   - Requires a `Collider` / `Collider2D` on the object.
   - Works either with standard Unity mouse events or via an EventSystem with a `PhysicsRaycaster` / `Physics2DRaycaster` on the camera.

### Realtime Delay
`TooltipTrigger` shows tooltips using `WaitForSecondsRealtime(_delayedShowupTime)`. This ensures tooltips continue to open smoothly even if gameplay is paused (`Time.timeScale = 0`). If the pointer exits before the delay completes, the coroutine is cancelled immediately.

---

## API Reference & Scripting

You can also display or hide tooltips programmatically from any script:

```csharp
using Giana.UnityUtils;
using UnityEngine;

public class InventorySlot : MonoBehaviour
{
    public void OnItemHovered(string itemName, string itemDescription)
    {
        // Show with header and body
        TooltipSystem.Instance.Show(itemDescription, itemName);
    }

    public void OnSimpleHovered(string description)
    {
        // Show body only (header will be hidden)
        TooltipSystem.Instance.Show(description);
    }

    public void OnItemUnhovered()
    {
        // Hide tooltip
        TooltipSystem.Instance.Hide();
    }
}
```

---

## Component Reference

### `TooltipSystem`
| Property | Type | Description |
|---|---|---|
| `_tooltip` | `Tooltip` | Reference to the `Tooltip` component on the visual prefab |
| `_showDebugLogs` | `bool` | Enables editor console logs for tooltip `Show` and `Hide` calls |

### `Tooltip`
| Property | Type | Description |
|---|---|---|
| `_headerField` | `TextMeshProUGUI` | Text component for the header. Automatically deactivated if header is empty |
| `_contentField` | `TextMeshProUGUI` | Text component for the body content |
| `_layoutElement` | `LayoutElement` | Layout element controlling preferred width constraint for text wrapping |
| `_characterWrapLimit` | `int` | Character count threshold above which `_layoutElement` is activated (wrapping kicks in) |
| `_showDebugLogs` | `bool` | Enables warning logs if UI references are missing |

### `TooltipTrigger`
| Property | Type | Description |
|---|---|---|
| `_header` | `string` | Header text to display |
| `_content` | `string` (`[TextArea]`) | Main body text to display |
| `_delayedShowupTime` | `float` | Delay in seconds before showing tooltip after pointer enter (uses unscaled time) |
| `_showDebugLogs` | `bool` | Enables editor console logs when triggering tooltips |

---

## Best Practices & Troubleshooting

- **Tooltip keeps flickering:** Verify that the `Graphic Raycaster` was removed from the `Tooltip Canvas`. If it has a raycaster, the tooltip GameObject blocks the pointer raycast to the trigger beneath it.
- **Tooltip looks tiny or huge on different displays:** Ensure `Canvas Scaler` is set to `Scale With Screen Size` on the `Tooltip Canvas`.
- **Tooltip appears behind other UI windows:** Ensure `Tooltip Canvas` has a higher `Sorting Order` than your gameplay or UI canvases (e.g. `32767`).
- **3D object not triggering tooltip:** Ensure the 3D object has a `Collider`, and either the camera has a `PhysicsRaycaster` (for EventSystem) or Unity legacy mouse events are active in the project.
- **NullReferenceException on `TooltipSystem.Instance`:** Make sure the `Tooltip Canvas` (with `TooltipSystem`) exists in the scene and was initialized via `LoadingManager` or scene startup.
