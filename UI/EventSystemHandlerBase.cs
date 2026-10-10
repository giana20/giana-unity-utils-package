using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
public abstract class EventSystemHandlerBase : MonoBehaviour
{
    #region Inspector Fields

    [Header("Controls")]
    [Space(10)]
    [SerializeField]
    protected InputActionReference _navigateReference;

    [Header("Animation")]
    [Space(10)]
    [SerializeField]
    [Range(0f, 2f)]
    [Tooltip("Scale multiplier applied to the selected element.")]
    protected float _selectedAnimationScale = 1.1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Duration of the scale animation in seconds.")]
    protected float _scaleDuration = 0.1f;

    [Header("Sound")]
    [Space(10)]
    [SerializeField]
    [Tooltip("Event invoked when a Selectable is selected (plays a sound).")]
    protected UnityEvent _soundEvent;

    [Header("Animator")]
    [Space(10)]
    [Tooltip("Optional. If left null, it will look for one on this GameObject, or add a default one.")]
    [SerializeField]
    protected MenuAnimatorBase _animator;

    #endregion

    #region Properties & Private Fields

    /// <summary>Stores original local scales of each Selectable for resetting animations.</summary>
    protected Dictionary<Selectable, Vector3> _scales = new Dictionary<Selectable, Vector3>();

    /// <summary>Last selected Selectable, used to restore focus when navigation occurs.</summary>
    protected Selectable _lastSelected;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        ResolveAnimator();
    }

    protected void ResolveAnimator()
    {
        if (_animator == null)
        {
            _animator = GetComponent<MenuAnimatorBase>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<DefaultMenuAnimator>();
            }
        }
    }

    protected virtual void OnEnable()
    {
        if (_navigateReference != null && _navigateReference.action != null)
            _navigateReference.action.performed += OnNavigateInput;
    }

    protected virtual void OnDisable()
    {
        if (_navigateReference != null && _navigateReference.action != null)
            _navigateReference.action.performed -= OnNavigateInput;

        if (_animator != null)
            _animator.Kill();
    }

    #endregion

    #region Selection Listener Setup

    /// <summary>
    /// Adds EventTrigger entries to a Selectable for selection, deselection, pointer enter, and pointer exit.
    /// </summary>
    /// <param name="selectable">The Selectable to attach listeners to.</param>
    protected virtual void AddSelectionListener(Selectable selectable)
    {
        if (selectable == null) return;
        
        if (!selectable.gameObject.TryGetComponent<EventTrigger>(out var eventTrigger))
            eventTrigger = selectable.gameObject.AddComponent<EventTrigger>();

        // Remove old listeners to avoid double-registration leaks
        RemoveSelectionListener(selectable, eventTrigger);

        EventTrigger.Entry selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
        selectEntry.callback.AddListener(OnSelect);
        eventTrigger.triggers.Add(selectEntry);

        EventTrigger.Entry deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
        deselectEntry.callback.AddListener(OnDeselect);
        eventTrigger.triggers.Add(deselectEntry);

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        pointerEnterEntry.callback.AddListener(OnPointerEnter);
        eventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        pointerExitEntry.callback.AddListener(OnPointerExit);
        eventTrigger.triggers.Add(pointerExitEntry);
    }

    /// <summary>
    /// Cleans up event listeners from a Selectable to prevent memory leaks when dynamically removed.
    /// </summary>
    protected virtual void RemoveSelectionListener(Selectable selectable, EventTrigger eventTrigger = null)
    {
        if (selectable == null) return;
        if (eventTrigger == null)
        {
            if (!selectable.gameObject.TryGetComponent<EventTrigger>(out eventTrigger))
                return;
        }

        foreach (var entry in eventTrigger.triggers)
        {
            if (entry.eventID == EventTriggerType.Select) entry.callback.RemoveListener(OnSelect);
            if (entry.eventID == EventTriggerType.Deselect) entry.callback.RemoveListener(OnDeselect);
            if (entry.eventID == EventTriggerType.PointerEnter) entry.callback.RemoveListener(OnPointerEnter);
            if (entry.eventID == EventTriggerType.PointerExit) entry.callback.RemoveListener(OnPointerExit);
        }
    }

    #endregion

    #region Selection and Deselection Handlers

    /// <summary>
    /// Called when a Selectable is selected. Triggers sound, scales the element up, and tracks it as the last selected.
    /// </summary>
    public virtual void OnSelect(BaseEventData eventData)
    {
        _soundEvent?.Invoke();

        if (eventData == null || eventData.selectedObject == null) return;
        if (IsExcluded(eventData.selectedObject)) return;

        Selectable selectable = eventData.selectedObject.GetComponent<Selectable>();
        if (selectable == null) return;

        _lastSelected = selectable;

        if (_scales.TryGetValue(selectable, out Vector3 originalScale))
        {
            Vector3 toScale = originalScale * _selectedAnimationScale;
            _animator.AnimateSelect(selectable.transform, toScale, _scaleDuration, true);
        }
    }

    /// <summary>
    /// Called when a Selectable is deselected. Resets the scale to its original value.
    /// </summary>
    public virtual void OnDeselect(BaseEventData eventData)
    {
        if (eventData == null || eventData.selectedObject == null) return;
        if (IsExcluded(eventData.selectedObject)) return;

        Selectable selectable = eventData.selectedObject.GetComponent<Selectable>();
        if (selectable == null) return;

        if (_scales.TryGetValue(selectable, out Vector3 originalScale))
        {
            _animator.AnimateDeselect(selectable.transform, originalScale, _scaleDuration, true);
        }
    }

    protected virtual bool IsExcluded(GameObject obj)
    {
        return false;
    }

    #endregion

    #region Pointer Event Handlers

    /// <summary>
    /// Called when the pointer enters a Selectable's area. Sets the selected object to that Selectable.
    /// </summary>
    public virtual void OnPointerEnter(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointerEventData && pointerEventData.pointerEnter != null)
        {
            Selectable selectable = pointerEventData.pointerEnter.GetComponentInParent<Selectable>();
            if (selectable == null)
            {
                selectable = pointerEventData.pointerEnter.GetComponentInChildren<Selectable>();
            }
            if (selectable != null)
            {
                pointerEventData.selectedObject = selectable.gameObject;
            }
        }
    }

    /// <summary>
    /// Called when the pointer exits a Selectable's area. Clears the selected object.
    /// </summary>
    public virtual void OnPointerExit(BaseEventData eventData)
    {
        if (eventData is PointerEventData pointerEventData)
            pointerEventData.selectedObject = null;
    }

    #endregion

    #region Navigation Logic

    /// <summary>
    /// Handles navigation input. If no object is currently selected but a last selected exists, restores focus.
    /// </summary>
    public virtual void OnNavigateInput(InputAction.CallbackContext ctx)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && _lastSelected != null)
            EventSystem.current.SetSelectedGameObject(_lastSelected.gameObject);
    }

    #endregion
}
}
