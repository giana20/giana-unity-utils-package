using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Giana.UnityUtils
{
    /// <summary>How a <see cref="DropZone"/> finds out that the pointer is over it.</summary>
    public enum DropZoneMode
    {
        /// <summary>uGUI: the pointer is inside the zone's RectTransform.</summary>
        UI,
        /// <summary>The camera ray hits a 3D collider that belongs to this zone.</summary>
        World3D,
        /// <summary>The camera ray hits a 2D collider that belongs to this zone.</summary>
        World2D
    }

    /// <summary>What happens to an item that was accepted by a zone (after the drop events ran).</summary>
    public enum DropAction
    {
        /// <summary>The item slides back to its slot (buy button, "play card on enemy" when the card stays).</summary>
        ReturnHome,
        /// <summary>The item is deleted with its removal animation (discard pile).</summary>
        Remove
    }

    public enum DropHoverState { None, Valid, Invalid }

    [Serializable] public class DraggableItemEvent : UnityEvent<DraggableItem> { }

    /// <summary>
    /// A place a <see cref="DraggableItem"/> can be dropped on to trigger something: a buy button, an enemy,
    /// a discard pile. Works with items inside a <see cref="DraggableList"/> and with standalone items.
    /// </summary>
    /// <remarks>
    /// Drop handlers get the item; read <see cref="DraggableItem.Payload"/> for your data. Call
    /// <see cref="DraggableItem.Remove"/> in a handler to consume the item yourself, otherwise the zone's
    /// <see cref="DropAction"/> is applied afterwards. Only the highest-priority zone under the pointer is considered.
    /// </remarks>
    [DisallowMultipleComponent]
    public class DropZone : MonoBehaviour
    {
        #region Inspector

        [Header("Detection")]
        [SerializeField] private DropZoneMode _mode = DropZoneMode.UI;
        [Tooltip("UI mode. Empty = this object's RectTransform.")]
        [SerializeField] private RectTransform _rect;
        [Tooltip("World modes. Empty = Camera.main.")]
        [SerializeField] private Camera _worldCamera;
        [Tooltip("World modes: layers the ray can hit. Put the zone colliders on their own layer to ignore everything else.")]
        [SerializeField] private LayerMask _layerMask = ~0;
        [SerializeField, Min(0f)] private float _maxDistance = 1000f;
        [Tooltip("Higher wins when several zones are under the pointer.")]
        [SerializeField] private int _priority;

        [Header("Accepting")]
        [Tooltip("Item tags this zone accepts. Empty = any item.")]
        [SerializeField] private string[] _acceptedTags = Array.Empty<string>();

        [Header("On Drop")]
        [SerializeField] private DropAction _action = DropAction.ReturnHome;

        [Header("Events")]
        [SerializeField] private DraggableItemEvent _onItemDropped = new();
        [Tooltip("An item that this zone accepts is hovering over it.")]
        [SerializeField] private DraggableItemEvent _onHoverValid = new();
        [Tooltip("An item that this zone rejects is hovering over it (show a red outline, etc.).")]
        [SerializeField] private DraggableItemEvent _onHoverInvalid = new();
        [SerializeField] private DraggableItemEvent _onHoverEnd = new();

        #endregion

        #region Public API

        public DropAction Action => _action;
        public DraggableItem HoveredItem { get; private set; }
        public DropHoverState HoverState { get; private set; }

        /// <summary>Extra rule on top of the tags, e.g. <c>item => wallet.CanAfford((ShopEntry)item.Payload)</c>.</summary>
        public Func<DraggableItem, bool> Validator { get; set; }

        /// <summary>An accepted item was released on the zone.</summary>
        public event Action<DraggableItem> ItemDropped;
        /// <summary>Hover started (Valid / Invalid) or ended (None).</summary>
        public event Action<DraggableItem, DropHoverState> HoverChanged;

        public bool CanAccept(DraggableItem item)
        {
            if (item == null || !isActiveAndEnabled) return false;
            if (_acceptedTags.Length > 0 && Array.IndexOf(_acceptedTags, item.Tag) < 0) return false;
            return Validator == null || Validator(item);
        }

        #endregion

        #region Registry

        private static readonly List<DropZone> s_active = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear(); // play mode without domain reload

        /// <summary>The highest-priority zone under the pointer (accepting or not), or null.</summary>
        internal static DropZone Find(PointerEventData eventData)
        {
            DropZone best = null;
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                DropZone zone = s_active[i];
                if (best != null && zone._priority <= best._priority) continue;
                if (zone.Contains(eventData.position)) best = zone;
            }
            return best;
        }

        #endregion

        #region Unity

        private Canvas _canvas;

        private void Reset() => _rect = GetComponent<RectTransform>();

        private void Awake()
        {
            if (_rect == null) _rect = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvas = canvas.rootCanvas;
        }

        private void OnEnable() => s_active.Add(this);

        private void OnDisable()
        {
            s_active.Remove(this);
            HoveredItem = null;
            HoverState = DropHoverState.None;
        }

        #endregion

        #region Detection

        private bool Contains(Vector2 screenPoint)
        {
            switch (_mode)
            {
                case DropZoneMode.UI:
                {
                    if (_rect == null) return false;
                    Camera cam = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
                    return RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, cam);
                }
                case DropZoneMode.World3D:
                {
                    Camera cam = WorldCamera;
                    if (cam == null) return false;
                    Ray ray = cam.ScreenPointToRay(screenPoint);
                    return Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _layerMask, QueryTriggerInteraction.Collide)
                           && hit.collider.GetComponentInParent<DropZone>() == this;
                }
                case DropZoneMode.World2D:
                {
                    Camera cam = WorldCamera;
                    if (cam == null) return false;
                    Ray ray = cam.ScreenPointToRay(screenPoint);
                    RaycastHit2D hit = Physics2D.GetRayIntersection(ray, _maxDistance, _layerMask);
                    return hit.collider != null && hit.collider.GetComponentInParent<DropZone>() == this;
                }
                default:
                    return false;
            }
        }

        private Camera WorldCamera => _worldCamera != null ? _worldCamera : Camera.main;

        #endregion

        #region Item callbacks

        internal void NotifyHoverStart(DraggableItem item)
        {
            HoveredItem = item;
            HoverState = CanAccept(item) ? DropHoverState.Valid : DropHoverState.Invalid;
            HoverChanged?.Invoke(item, HoverState);
            (HoverState == DropHoverState.Valid ? _onHoverValid : _onHoverInvalid).Invoke(item);
        }

        internal void NotifyHoverEnd(DraggableItem item)
        {
            if (HoveredItem != item) return;
            HoveredItem = null;
            HoverState = DropHoverState.None;
            HoverChanged?.Invoke(item, DropHoverState.None);
            _onHoverEnd.Invoke(item);
        }

        internal void Receive(DraggableItem item)
        {
            ItemDropped?.Invoke(item);
            _onItemDropped.Invoke(item);
        }

        #endregion
    }
}
