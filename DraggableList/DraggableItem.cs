using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// One entry of a <see cref="DraggableList"/>.
    /// <para>
    /// Hierarchy: the item root stays inside the list's layout group and acts as the placeholder (slot).
    /// The visible part lives in a child (<c>Contents</c>) that is lifted out to the drag layer while dragging,
    /// so the layout never has to follow the pointer.
    /// </para>
    /// <para>
    /// An item does not need a list: without one it works standalone (set the Standalone fields) and can be
    /// dropped on <see cref="DropZone"/>s, e.g. a card played onto an enemy.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DraggableItem : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Inspector

        [Header("Drag")]
        [Tooltip("Pixels the pointer must travel before the item is lifted.")]
        [SerializeField, Min(0f)] private float _dragThreshold = 10f;
        [SerializeField] private bool _draggable = true;

        [Header("Identity")]
        [Tooltip("Matched against DropZone accepted tags (e.g. \"Weapon\").")]
        [SerializeField] private string _tag = "";

        [Header("Standalone (only used when the item is not in a DraggableList)")]
        [Tooltip("Empty = the root canvas of this item.")]
        [SerializeField] private Canvas _rootCanvas;
        [Tooltip("Where the lifted visual lives. Empty = the root canvas.")]
        [SerializeField] private RectTransform _dragLayer;
        [SerializeField] private int _dragSortingOrder = 500;
        [Tooltip("Optional. If empty, a DefaultDragAnimator is added to this object.")]
        [SerializeField] private DragAnimatorBase _animator;
        [SerializeField, Min(0f)] private float _returnDuration = 0.15f;
        [SerializeField] private AnimationCurve _returnEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField, Min(0f)] private float _removeDuration = 0.2f;
        [SerializeField] private AnimationCurve _removeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool _destroyOnRemove = true;

        [Header("References")]
        [SerializeField] private RectTransform _rectTransform;
        [Tooltip("The visible child. Must not already have a Canvas on a different sorting layer.")]
        [SerializeField] private RectTransform _contents;
        [Tooltip("Holds the slot size while the contents are lifted. Added automatically if missing.")]
        [SerializeField] private LayoutElement _placeholderLayout;

        #endregion

        #region Public API

        public RectTransform RectTransform => _rectTransform;
        public RectTransform Contents => _contents;
        public DraggableList Owner => _list;
        public bool IsDragging => _isDragging;

        public bool Draggable
        {
            get => _draggable;
            set => _draggable = value;
        }

        /// <summary>Free-form label used by <see cref="DropZone"/> tag filters.</summary>
        public string Tag
        {
            get => _tag;
            set => _tag = value;
        }

        /// <summary>Runtime data you attach (shop entry, card, ...). Read it back in drop handlers.</summary>
        public object Payload { get; set; }

        /// <summary>
        /// Deletes the item with the removal animation (list or standalone). Call it from a drop handler to consume the item.
        /// </summary>
        public void Remove()
        {
            if (_removing || _isDragging) return;
            _removing = true;

            if (_list != null && _list.IndexOf(this) >= 0)
                _list.RemoveItem(this);
            else
                PlayRemoval(RemoveDuration, RemoveEase, DestroyOnRemove);
        }

        /// <summary>Raised when the item leaves its slot (after the drag threshold).</summary>
        public event Action<DraggableItem> Lifted;
        /// <summary>Raised when the pointer is released, before the item returns, moves or is removed.</summary>
        public event Action<DraggableItem> Dropped;

        #endregion

        #region State

        private DraggableList _list;
        private DropZone _hoverZone;
        private bool _removing;
        private DragAnimatorBase _standaloneAnimator;
        private Canvas _standaloneCanvas;

        private bool _armed;
        private bool _isDragging;
        private int _pointerId = int.MinValue;
        private Vector3 _grabOffset;

        private bool _detached;
        private bool _reattachWhenEnabled;
        private Canvas _dragCanvas;
        private bool _addedCanvas;
        private bool _prevOverride;
        private int _prevOrder;
        private CanvasGroup _dragGroup;
        private bool _addedGroup;
        private bool _prevBlocks;

        // Original layout of Contents, restored when it returns home.
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePivot, _homeSizeDelta;
        private Vector3 _homeAnchoredPos, _homeScale;
        private Quaternion _homeRotation;
        private int _homeSibling;
        private bool _homeCaptured;

        // Placeholder (LayoutElement) bookkeeping.
        private bool _layoutHeld;
        private Vector2 _natural;
        private Vector2 _axisMask = Vector2.one;
        private float _placeholderFraction = 1f;
        private float _bMinW, _bMinH, _bPrefW, _bPrefH, _bFlexW, _bFlexH;
        private bool _bIgnore;

        // Everything that depends on the owner is resolved here: the list when there is one, our own fields otherwise.
        private DragAnimatorBase Animator
        {
            get
            {
                DragAnimatorBase animator;
                if (_list != null) animator = _list.Animator;
                else
                {
                    EnsureStandalone();
                    animator = _standaloneAnimator;
                }
                return animator != null ? animator : null; // real null, so ?. is safe
            }
        }

        private RectTransform DragLayer => _list != null
            ? _list.DragLayer
            : _dragLayer != null ? _dragLayer : (RectTransform)_standaloneCanvas.transform;

        private Camera EventCamera => _list != null
            ? _list.EventCamera
            : _standaloneCanvas == null || _standaloneCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _standaloneCanvas.worldCamera;

        private int DragSortingOrder => _list != null ? _list.DragSortingOrder : _dragSortingOrder;
        private float ReturnDuration => _list != null ? _list.SwapDuration : _returnDuration;
        private AnimationCurve ReturnEase => _list != null ? _list.SwapEase : _returnEase;
        private float RemoveDuration => _list != null ? _list.RemoveDuration : _removeDuration;
        private AnimationCurve RemoveEase => _list != null ? _list.RemoveEase : _removeEase;
        private bool DestroyOnRemove => _list != null ? _list.DestroyOnRemove : _destroyOnRemove;

        private bool EnsureStandalone()
        {
            if (_standaloneCanvas == null)
            {
                _standaloneCanvas = _rootCanvas != null ? _rootCanvas : null;
                if (_standaloneCanvas == null)
                {
                    var parent = GetComponentInParent<Canvas>();
                    if (parent != null) _standaloneCanvas = parent.rootCanvas;
                }
            }

            if (_standaloneAnimator == null)
            {
                _standaloneAnimator = _animator != null ? _animator : GetComponent<DragAnimatorBase>();
                if (_standaloneAnimator == null) _standaloneAnimator = gameObject.AddComponent<DefaultDragAnimator>();
            }

            return _standaloneCanvas != null;
        }

        #endregion

        #region Unity

        private void Reset()
        {
            _rectTransform = GetComponent<RectTransform>();
            _placeholderLayout = GetComponent<LayoutElement>();
            if (_placeholderLayout == null) _placeholderLayout = gameObject.AddComponent<LayoutElement>();
            if (transform.childCount > 0) _contents = transform.GetChild(0) as RectTransform;
        }

        private void Awake()
        {
            if (_rectTransform == null) _rectTransform = (RectTransform)transform;
            if (_contents == null)
            {
                Debug.LogError($"[{nameof(DraggableItem)}] '{name}' has no Contents assigned.", this);
                enabled = false;
                return;
            }

            if (_placeholderLayout == null)
            {
                _placeholderLayout = GetComponent<LayoutElement>();
                if (_placeholderLayout == null) _placeholderLayout = gameObject.AddComponent<LayoutElement>();
            }

            CaptureHome();
        }

        private void OnEnable()
        {
            if (!_reattachWhenEnabled) return;
            _reattachWhenEnabled = false;
            if (_contents != null) _contents.gameObject.SetActive(true);
            Reattach();
        }

        private void OnDisable()
        {
            _armed = false;
            Animator?.KillAll(this);
            SetHoverZone(null);

            if (_isDragging)
            {
                _isDragging = false;
                if (_list != null) _list.NotifyDragAborted(this);
            }

            // Hierarchy can't change while we deactivate: hide now, reattach on enable.
            if (_detached && _contents != null)
            {
                _contents.gameObject.SetActive(false);
                _reattachWhenEnabled = true;
            }
        }

        private void OnDestroy()
        {
            Animator?.KillAll(this);
            // Detached contents are no longer our child, so they would leak.
            if (_detached && _contents != null) Destroy(_contents.gameObject);
        }

        #endregion

        #region Binding (called by the list)

        internal void Bind(DraggableList list) => _list = list;

        internal bool IsDetached => _detached;

        internal Vector3 ContentsWorldPosition => _contents.position;

        /// <summary>Puts the item back into a clean, idle state (used when re-adding a pooled item).</summary>
        internal void ResetState()
        {
            if (!_homeCaptured) return; // Awake has not run yet, nothing to reset
            Animator?.KillAll(this);
            Reattach();
            _contents.localScale = _homeScale;
            _contents.anchoredPosition3D = _homeAnchoredPos;
            if (_placeholderLayout != null) _placeholderLayout.ignoreLayout = false;
            _isDragging = false;
            _armed = false;
            _removing = false;
        }

        #endregion

        #region Pointer events

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (!CanInteract) return;
            // We apply our own threshold in OnDrag so that _dragThreshold is the real distance.
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanInteract || _armed || _isDragging) return; // first pointer wins
            _armed = true;
            _pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            if (!_isDragging)
            {
                if (!_armed) return;
                if ((eventData.position - eventData.pressPosition).sqrMagnitude < _dragThreshold * _dragThreshold) return;
                if (_list != null && !_list.CanStartDrag(this)) return;
                Lift(eventData);
            }

            MoveWithPointer(eventData);

            DropZone zone = DropZone.Find(eventData);
            SetHoverZone(zone);
            bool overValidZone = zone != null && zone.CanAccept(this);
            if (_list != null) _list.HandleDrag(this, eventData, overValidZone);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            bool wasDragging = _isDragging;
            _armed = false;
            _isDragging = false;
            _pointerId = int.MinValue;
            if (!wasDragging) return;

            Dropped?.Invoke(this);

            DropZone zone = DropZone.Find(eventData);
            SetHoverZone(null);

            if (zone != null && zone.CanAccept(this))
            {
                bool consume = zone.Action == DropAction.Remove;
                if (_list != null) _list.HandleDroppedOnZone(this, !consume);

                zone.Receive(this);

                // A handler may already have consumed the item (item.Remove()); otherwise apply the zone's action.
                if (!_removing)
                {
                    if (consume) Remove();
                    else AnimateReturn(ReturnDuration, ReturnEase);
                }
                return;
            }

            if (_list != null) _list.HandleDrop(this, eventData);
            else AnimateReturn(ReturnDuration, ReturnEase);
        }

        private void SetHoverZone(DropZone zone)
        {
            if (zone == _hoverZone) return;
            if (_hoverZone != null) _hoverZone.NotifyHoverEnd(this);
            _hoverZone = zone;
            if (_hoverZone != null) _hoverZone.NotifyHoverStart(this);
        }

        private bool CanInteract => _draggable && _contents != null && (_list != null || EnsureStandalone());

        #endregion

        #region Lift / follow

        private void Lift(PointerEventData eventData)
        {
            _isDragging = true;
            if (!_homeCaptured) CaptureHome();

            HoldLayout();
            _placeholderFraction = 1f;
            ApplyPlaceholder();

            RectTransform layer = DragLayer;

            // Remember where the contents are so we can recreate the same size and place under the layer.
            Vector3 pivotWorld = _contents.position;
            Quaternion worldRotation = _contents.rotation;
            Vector2 parentScale = _rectTransform.lossyScale;
            Vector2 layerScale = layer.lossyScale;
            Vector2 size = _contents.rect.size;

            _contents.SetParent(layer, false);
            _contents.anchorMin = _contents.anchorMax = new Vector2(0.5f, 0.5f);
            _contents.sizeDelta = new Vector2(
                size.x * parentScale.x / Mathf.Max(layerScale.x, 1e-5f),
                size.y * parentScale.y / Mathf.Max(layerScale.y, 1e-5f));
            _contents.rotation = worldRotation;
            _contents.position = pivotWorld;
            _detached = true;

            // Draw above everything else in the root canvas.
            _dragCanvas = _contents.GetComponent<Canvas>();
            _addedCanvas = _dragCanvas == null;
            if (_addedCanvas) _dragCanvas = _contents.gameObject.AddComponent<Canvas>();
            else { _prevOverride = _dragCanvas.overrideSorting; _prevOrder = _dragCanvas.sortingOrder; }
            _dragCanvas.overrideSorting = true;
            _dragCanvas.sortingOrder = DragSortingOrder;

            // The lifted visual must never block what is under it.
            _dragGroup = _contents.GetComponent<CanvasGroup>();
            _addedGroup = _dragGroup == null;
            if (_addedGroup) _dragGroup = _contents.gameObject.AddComponent<CanvasGroup>();
            else _prevBlocks = _dragGroup.blocksRaycasts;
            _dragGroup.blocksRaycasts = false;

            // Keep the grab point under the pointer, no jump.
            RectTransformUtility.ScreenPointToWorldPointInRectangle(layer, eventData.position, EventCamera, out Vector3 pointerWorld);
            _grabOffset = _contents.position - pointerWorld;

            if (_list != null) _list.NotifyLifted(this);
            Lifted?.Invoke(this);
        }

        private void MoveWithPointer(PointerEventData eventData)
        {
            if (!_detached) return;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(DragLayer, eventData.position, EventCamera, out Vector3 pointerWorld);
            _contents.position = pointerWorld + _grabOffset;
        }

        #endregion

        #region Returning / neighbour motion

        /// <summary>Slides the lifted contents back into the slot, then reattaches them.</summary>
        internal void AnimateReturn(float duration, AnimationCurve ease)
        {
            var animator = Animator;
            if (!_detached || animator == null)
            {
                Reattach();
                return;
            }

            animator.Kill(this, DragAnimationChannel.Move);
            Vector3 start = _contents.position;
            animator.Play(this, DragAnimationChannel.Move, duration, ease,
                t => { if (_contents != null) _contents.position = Vector3.LerpUnclamped(start, HomeWorldPosition(), t); },
                Reattach);
        }

        /// <summary>
        /// Neighbour animation: the layout already moved the slot instantly, so the visual starts at its old world
        /// position and eases to zero offset. It stays inside the item hierarchy (masks and scroll views keep working).
        /// </summary>
        internal void AnimateFromWorld(Vector3 previousWorld, float duration, AnimationCurve ease)
        {
            if (_detached || _isDragging || !isActiveAndEnabled) return;
            var animator = Animator;
            if (animator == null) return;

            animator.Kill(this, DragAnimationChannel.Move);
            _contents.anchoredPosition3D = _homeAnchoredPos;

            Vector3 offset = _rectTransform.InverseTransformVector(previousWorld - _contents.position);
            offset.z = 0f;
            if (offset.sqrMagnitude < 0.01f) return;

            Vector3 home = _homeAnchoredPos;
            animator.Play(this, DragAnimationChannel.Move, duration, ease,
                t => { if (_contents != null) _contents.anchoredPosition3D = home + offset * (1f - t); },
                () => { if (_contents != null) _contents.anchoredPosition3D = home; });
        }

        private Vector3 HomeWorldPosition()
        {
            // Pivot position of the contents if they were attached with their home values.
            Rect r = _rectTransform.rect;
            Vector2 min = new Vector2(r.xMin + r.width * _homeAnchorMin.x, r.yMin + r.height * _homeAnchorMin.y);
            Vector2 max = new Vector2(r.xMin + r.width * _homeAnchorMax.x, r.yMin + r.height * _homeAnchorMax.y);
            Vector2 reference = min + Vector2.Scale(max - min, _homePivot);
            Vector3 local = new Vector3(reference.x + _homeAnchoredPos.x, reference.y + _homeAnchoredPos.y, _homeAnchoredPos.z);
            return _rectTransform.TransformPoint(local);
        }

        #endregion

        #region Placeholder

        /// <summary>
        /// Smooth (linear layouts): animates the slot size along the main axis.
        /// Instant (grids): toggles <c>ignoreLayout</c>; the list animates the neighbours.
        /// </summary>
        internal void SetPlaceholderCollapsed(bool collapsed, bool instant, Vector2 axisMask, float duration, AnimationCurve ease)
        {
            HoldLayout();
            _axisMask = axisMask;

            if (instant)
            {
                _placeholderLayout.ignoreLayout = collapsed;
                return;
            }

            var animator = Animator;
            float from = _placeholderFraction;
            float to = collapsed ? 0f : 1f;
            if (animator == null)
            {
                _placeholderFraction = to;
                ApplyPlaceholder();
                return;
            }

            animator.Play(this, DragAnimationChannel.Placeholder, duration, ease, t =>
            {
                _placeholderFraction = Mathf.LerpUnclamped(from, to, t);
                ApplyPlaceholder();
            }, null);
        }

        /// <summary>Used by the list right before the root is destroyed so the layout closes the gap.</summary>
        internal void SetIgnoreLayout(bool value)
        {
            HoldLayout();
            _placeholderLayout.ignoreLayout = value;
        }

        /// <summary>Back to a full-size, in-layout slot without animation (cross-list transfer).</summary>
        internal void ResetPlaceholderImmediate()
        {
            Animator?.Kill(this, DragAnimationChannel.Placeholder);
            _placeholderFraction = 1f;
            if (!_layoutHeld) return;
            _placeholderLayout.ignoreLayout = false;
            ApplyPlaceholder();
        }

        private void HoldLayout()
        {
            if (_layoutHeld) return;
            _layoutHeld = true;
            _natural = _rectTransform.rect.size;

            _bMinW = _placeholderLayout.minWidth;
            _bMinH = _placeholderLayout.minHeight;
            _bPrefW = _placeholderLayout.preferredWidth;
            _bPrefH = _placeholderLayout.preferredHeight;
            _bFlexW = _placeholderLayout.flexibleWidth;
            _bFlexH = _placeholderLayout.flexibleHeight;
            _bIgnore = _placeholderLayout.ignoreLayout;
        }

        private void ApplyPlaceholder()
        {
            float fx = Mathf.Lerp(1f, _placeholderFraction, _axisMask.x);
            float fy = Mathf.Lerp(1f, _placeholderFraction, _axisMask.y);
            float w = Mathf.Max(0f, _natural.x * fx);
            float h = Mathf.Max(0f, _natural.y * fy);

            _placeholderLayout.minWidth = w;
            _placeholderLayout.preferredWidth = w;
            _placeholderLayout.minHeight = h;
            _placeholderLayout.preferredHeight = h;
            _placeholderLayout.flexibleWidth = 0f;
            _placeholderLayout.flexibleHeight = 0f;
        }

        private void ReleaseLayout()
        {
            if (!_layoutHeld) return;
            _layoutHeld = false;
            _placeholderFraction = 1f;

            _placeholderLayout.minWidth = _bMinW;
            _placeholderLayout.minHeight = _bMinH;
            _placeholderLayout.preferredWidth = _bPrefW;
            _placeholderLayout.preferredHeight = _bPrefH;
            _placeholderLayout.flexibleWidth = _bFlexW;
            _placeholderLayout.flexibleHeight = _bFlexH;
            _placeholderLayout.ignoreLayout = _bIgnore;
        }

        #endregion

        #region Removal

        /// <summary>Scales the contents to zero, then destroys (or deactivates) the item.</summary>
        internal void PlayRemoval(float duration, AnimationCurve ease, bool destroyWhenDone)
        {
            _armed = false;
            var animator = Animator;
            animator?.Kill(this, DragAnimationChannel.Move);

            Vector3 start = _contents.localScale;
            if (animator == null)
            {
                FinishRemoval(destroyWhenDone);
                return;
            }

            animator.Play(this, DragAnimationChannel.Removal, duration, ease,
                t => { if (_contents != null) _contents.localScale = Vector3.LerpUnclamped(start, Vector3.zero, t); },
                () => FinishRemoval(destroyWhenDone));
        }

        private void FinishRemoval(bool destroyWhenDone)
        {
            if (_list != null) _list.NotifyRemovalFinished(this);

            if (destroyWhenDone)
            {
                Destroy(gameObject);
                return;
            }

            // Pooling-friendly: leave a clean, inactive item behind.
            ResetState();
            gameObject.SetActive(false);
        }

        #endregion

        #region Home / reattach

        private void CaptureHome()
        {
            _homeAnchorMin = _contents.anchorMin;
            _homeAnchorMax = _contents.anchorMax;
            _homePivot = _contents.pivot;
            _homeSizeDelta = _contents.sizeDelta;
            _homeAnchoredPos = _contents.anchoredPosition3D;
            _homeScale = _contents.localScale;
            _homeRotation = _contents.localRotation;
            _homeSibling = _contents.GetSiblingIndex();
            _homeCaptured = true;
        }

        /// <summary>Puts the contents back under the item with their original layout values.</summary>
        private void Reattach()
        {
            Animator?.Kill(this, DragAnimationChannel.Move);

            if (_detached && _contents != null)
            {
                if (_addedCanvas) Destroy(_dragCanvas);
                else if (_dragCanvas != null) { _dragCanvas.overrideSorting = _prevOverride; _dragCanvas.sortingOrder = _prevOrder; }
                if (_addedGroup) Destroy(_dragGroup);
                else if (_dragGroup != null) _dragGroup.blocksRaycasts = _prevBlocks;
                _dragCanvas = null;
                _dragGroup = null;

                _contents.SetParent(_rectTransform, false);
                _contents.pivot = _homePivot;
                _contents.anchorMin = _homeAnchorMin;
                _contents.anchorMax = _homeAnchorMax;
                _contents.sizeDelta = _homeSizeDelta;
                _contents.anchoredPosition3D = _homeAnchoredPos;
                _contents.localScale = _homeScale;
                _contents.localRotation = _homeRotation;
                _contents.SetSiblingIndex(Mathf.Min(_homeSibling, _rectTransform.childCount - 1));
                _detached = false;
            }

            ReleaseLayout();
        }

        #endregion
    }
}
