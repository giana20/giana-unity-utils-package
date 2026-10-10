using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
/// <summary>
/// Abstract base class for building and managing UI menus that display a list of items of type <typeparamref name="T"/>.
/// Handles menu opening/closing events, instantiation of UI components, and selection logic.
/// </summary>
/// <typeparam name="T">The type of data item to display and select.</typeparam>
public abstract class UIBuilder<T>
{
    #region Events

    /// <summary>Event invoked when the menu is opened (e.g., before showing UI).</summary>
    public event Action OnMenuOpened;

    /// <summary>Event invoked when the menu is closed (e.g., after hiding UI).</summary>
    public event Action OnMenuClosed;

    #endregion

    #region Properties & Private Fields

    /// <summary>Prefab used to create each UI element (must be a Selectable).</summary>
    protected Selectable _UISelectablePrefab;

    /// <summary>Reference to the DynamicEventSystemHandler that manages navigation for the menu.</summary>
    protected DynamicEventSystemHandler _menu;
    protected Transform _selectablesParent;

    /// <summary>List of instantiated UI GameObjects currently displayed.</summary>
    protected List<GameObject> _instantiatedUIPrefabs;

    private CanvasGroup _menuCanvasGroup;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the UIBuilder.
    /// </summary>
    /// <param name="UIPrefab">The Selectable prefab used to build each item's UI.</param>
    /// <param name="menu">The DynamicEventSystemHandler that handles menu navigation.</param>
    /// <param name="selectablesParent">The Transform which will be the parent of the instantiated prefabs.</param>
    public UIBuilder(
        Selectable UIPrefab,
        DynamicEventSystemHandler menu,
        Transform selectablesParent
    )
    {
        _UISelectablePrefab = UIPrefab;
        _menu = menu;
        _selectablesParent = selectablesParent;
        _instantiatedUIPrefabs = new List<GameObject>();
        _menuCanvasGroup = _menu.GetComponent<CanvasGroup>();
        if (_menuCanvasGroup == null)
            _menuCanvasGroup = _menu.gameObject.AddComponent<CanvasGroup>();
        _menu.gameObject.SetActive(false);
    }

    #endregion

    #region Public Virtual Methods

    /// <summary>
    /// Displays the UI menu and spawns the provided items.
    /// </summary>
    public virtual void ShowUI(T[] items)
    {
        OnMenuOpened?.Invoke();
        _menu.gameObject.SetActive(true);

        SpawnItems(items);
    }

    /// <summary>
    /// Builds UI components for each item, adds them to the menu, and plays their enter animations.
    /// Can be called independently to refresh items while the menu remains open.
    /// </summary>
    public virtual void SpawnItems(T[] items)
    {
        _menuCanvasGroup.interactable = false;
        _menuCanvasGroup.blocksRaycasts = false;

        _menu.ClearSelectable(); // Ensure menu is cleared before adding new items
        float maxDelay = 0f;

        for (int i = 0; i < items.Length; i++)
        {
            Selectable selectable = BuildUIComponent(items[i]);
            _menu.AddSelectable(selectable);
            _instantiatedUIPrefabs.Add(selectable.gameObject);

            if (selectable.TryGetComponent(out IUITransition transition))
            {
                float delay = i * 0.15f;
                transition.PlayEnterAnimation(delay);
                maxDelay = delay; // Track the longest delay
            }
        }

        _menu.InitSelectable();

        _menu.StartCoroutine(UnlockMenuAfterDelay(maxDelay + 0.5f));
    }

    private IEnumerator UnlockMenuAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.interactable = true;
            _menuCanvasGroup.blocksRaycasts = true;
            _menu.SetFirstSelected(); // Set focus only when it's interactable!
        }
    }

    /// <summary>
    /// Handles selection of an item, invokes the derived selection logic, and hides the UI.
    /// </summary>
    public virtual void SelectItem(T item)
    {
        if (!_menuCanvasGroup.interactable)
            return;

        _menuCanvasGroup.interactable = false;
        _menuCanvasGroup.blocksRaycasts = false;

        OnItemSelected(item);
    }

    /// <summary>
    /// Triggers exit animations, destroys items, and completely closes the menu.
    /// </summary>
    public virtual void HideUI()
    {
        HideItems(() =>
        {
            FinalizeCloseMenu();
        });
    }

    /// <summary>
    /// Triggers the exit animations for all instantiated UI elements.
    /// Once complete, it calls DestroyItems() and invokes an optional callback.
    /// Useful for animating items out without turning off the UI Canvas.
    /// </summary>
    public virtual void HideItems(Action onComplete = null)
    {
        // Prevent further interactions while hiding
        _menuCanvasGroup.interactable = false;
        _menuCanvasGroup.blocksRaycasts = false;

        // If there's nothing to animate, destroy immediately and run callback
        if (_instantiatedUIPrefabs.Count == 0)
        {
            DestroyItems();
            onComplete?.Invoke();
            return;
        }

        // Create a snapshot array so modifying _instantiatedUIPrefabs doesn't throw during enumeration
        GameObject[] itemsToAnimate = _instantiatedUIPrefabs.ToArray();
        int activeAnimations = itemsToAnimate.Length;

        foreach (GameObject obj in itemsToAnimate)
        {
            if (obj != null && obj.TryGetComponent(out IUITransition transition))
            {
                transition.PlayExitAnimation(() =>
                {
                    activeAnimations--;

                    if (activeAnimations == 0)
                    {
                        DestroyItems();
                        onComplete?.Invoke();
                    }
                });
            }
            else
            {
                // Fallback for prefabs without animations
                activeAnimations--;

                if (activeAnimations == 0)
                {
                    DestroyItems();
                    onComplete?.Invoke();
                }
            }
        }
    }

    /// <summary>
    /// Immediately destroys all instantiated UI GameObjects and clears their references from the lists.
    /// </summary>
    public virtual void DestroyItems()
    {
        foreach (GameObject obj in _instantiatedUIPrefabs)
        {
            if (obj != null)
            {
                MonoBehaviour.Destroy(obj);
            }
        }

        _instantiatedUIPrefabs.Clear();
        _menu.ClearSelectable();
    }

    private void FinalizeCloseMenu()
    {
        _menu.gameObject.SetActive(false);
        OnMenuClosed?.Invoke();
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Creates and returns a Selectable UI component for the given item.
    /// Derived classes must implement this to populate the visual elements.
    /// </summary>
    protected abstract Selectable BuildUIComponent(T item);

    /// <summary>
    /// Defines the behavior when an item is selected by the user.
    /// Derived classes must implement the specific logic for the item type.
    /// </summary>
    protected abstract void OnItemSelected(T item);

    #endregion
}
}
