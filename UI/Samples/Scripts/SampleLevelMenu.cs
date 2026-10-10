using System;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Minimal UIBuilder&lt;T&gt; implementation. This is all you need to write for a new dynamic menu:
    /// 1) build one Selectable per item, 2) say what happens when one is chosen.
    /// </summary>
    public class SampleLevelMenu : UIBuilder<SampleItem>
    {
        /// <summary>Raised when the player picks an item (before the menu closes).</summary>
        public event Action<SampleItem> ItemChosen;

        public SampleLevelMenu(
            Selectable itemPrefab,
            DynamicEventSystemHandler menu,
            Transform itemsParent
        )
            : base(itemPrefab, menu, itemsParent) { }

        protected override Selectable BuildUIComponent(SampleItem item)
        {
            // Instantiate under the items parent (a layout group arranges the children)
            Selectable instance = UnityEngine.Object.Instantiate(_UISelectablePrefab, _selectablesParent);

            // IMPORTANT: wire the click to SelectItem. The base class does NOT do this for you.
            // SelectItem ignores clicks while the menu is locked (during enter/exit animations).
            instance.GetComponent<SampleItemButton>().Bind(item, SelectItem);

            return instance;
        }

        protected override void OnItemSelected(SampleItem item)
        {
            ItemChosen?.Invoke(item);

            // SelectItem locks the menu and leaves closing to us: close it here...
            HideUI();
            // ...or, to keep the menu open, you would have to unlock it again (see the documentation).
        }
    }
}
