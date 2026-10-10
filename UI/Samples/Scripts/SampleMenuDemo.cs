using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Demo controller showing both systems working together.
    ///
    ///   MAIN MENU  (static)  : MenuEventSystemHandler   - fixed buttons set up in the Inspector.
    ///   LEVEL MENU (dynamic) : DynamicEventSystemHandler + UIBuilder - items created from data at runtime.
    ///
    /// Put this component on an object that is ALWAYS ACTIVE (not on the main menu or level menu objects:
    /// the builder deactivates the level menu object, which would also stop this script).
    /// Hook the public methods to the buttons' OnClick events in the Inspector.
    /// </summary>
    public class SampleMenuDemo : MonoBehaviour
    {
        [Header("Static menu (MenuEventSystemHandler)")]
        [SerializeField]
        [Tooltip("Root of the main menu. Keep it INACTIVE in the scene: the bootstrapper initializes and enables it.")]
        private GameObject _mainMenuRoot;

        [Header("Dynamic menu (DynamicEventSystemHandler)")]
        [SerializeField]
        private DynamicEventSystemHandler _levelMenu;

        [SerializeField]
        [Tooltip("Prefab with Button + SampleItemButton + SampleUITransition on its root.")]
        private Selectable _itemPrefab;

        [SerializeField]
        [Tooltip("The object with the Vertical Layout Group that will contain the spawned items.")]
        private Transform _itemsParent;

        [SerializeField]
        private SampleItem[] _levels =
        {
            new SampleItem { Title = "Green Meadows", Description = "Easy - 5 min" },
            new SampleItem { Title = "Crystal Caves", Description = "Medium - 8 min" },
            new SampleItem { Title = "Volcano Rim", Description = "Hard - 12 min" },
        };

        [SerializeField]
        [Tooltip("Second data set, used by the 'Swap list' button to demonstrate refreshing items.")]
        private SampleItem[] _alternativeLevels =
        {
            new SampleItem { Title = "Neon City", Description = "Medium - 7 min" },
            new SampleItem { Title = "Orbital Station", Description = "Hard - 10 min" },
        };

        [Header("Output")]
        [SerializeField]
        private TMP_Text _statusLabel;

        private SampleLevelMenu _builder;
        private bool _showingAlternative;

        private void Awake()
        {
            // The builder is a plain C# object, so we create it ourselves.
            // Note: its constructor deactivates the level menu object.
            _builder = new SampleLevelMenu(_itemPrefab, _levelMenu, _itemsParent);

            // Hide the main menu while the level menu is open, bring it back when it closes.
            _builder.OnMenuOpened += () => _mainMenuRoot.SetActive(false);
            _builder.OnMenuClosed += () => _mainMenuRoot.SetActive(true);
            _builder.ItemChosen += item => SetStatus($"Level chosen: {item.Title}");

            SetStatus("Use arrows / D-pad / mouse");
        }

        // ---------------------------------------------------------------
        // Static main menu buttons
        // ---------------------------------------------------------------

        /// <summary>Main menu > "Choose level".</summary>
        public void OpenLevelMenu()
        {
            _showingAlternative = false;
            _builder.ShowUI(_levels);
        }

        /// <summary>Main menu > "Settings" (does nothing but prove the static buttons work).</summary>
        public void ShowSettings()
        {
            SetStatus("Settings clicked (static button)");
        }

        /// <summary>Main menu > "Quit".</summary>
        public void Quit()
        {
            SetStatus("Quit clicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------
        // Persistent buttons inside the dynamic level menu
        // ---------------------------------------------------------------

        /// <summary>Level menu > "Close": animates the items out and closes the whole menu.</summary>
        public void CloseLevelMenu()
        {
            _builder.HideUI();
        }

        /// <summary>
        /// Level menu > "Swap list": refreshes the items WITHOUT closing the menu.
        /// SpawnItems alone does not remove the old items, so we animate them out first (HideItems)
        /// and spawn the new ones in the completion callback.
        /// </summary>
        public void SwapLevelList()
        {
            _showingAlternative = !_showingAlternative;
            SampleItem[] next = _showingAlternative ? _alternativeLevels : _levels;
            _builder.HideItems(() => _builder.SpawnItems(next));
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.text = text;
        }
    }
}
