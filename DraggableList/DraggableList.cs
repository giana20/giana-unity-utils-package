using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>How the container lays out its items; decides how reordering and the placeholder behave.</summary>
    public enum DragArrangement
    {
        /// <summary>Grid layout group → <see cref="Grid"/>, anything else → <see cref="Linear"/>.</summary>
        Auto,
        /// <summary>Horizontal / Vertical layout group. Reorders when the pointer crosses a neighbour's centre, the slot collapses smoothly.</summary>
        Linear,
        /// <summary>Grid-like layouts. Reorders to the nearest slot, the slot leaves the layout instantly and neighbours slide.</summary>
        Grid
    }

    /// <summary>
    /// Container for <see cref="DraggableItem"/>s: drag to reorder, move between lists, drag out to delete
    /// (optionally with a confirmation). Animation is delegated to a <see cref="DragAnimatorBase"/>.
    /// </summary>
    /// <remarks>
    /// Not related to Unity's <c>ReorderableList</c> (UnityEditorInternal), which is an editor-only IMGUI control.
    /// </remarks>
    [DisallowMultipleComponent]
    public class DraggableList : MonoBehaviour
    {
        #region Inspector

        [Header("Setup")]
        [SerializeField] private Canvas _rootCanvas;
        [Tooltip("The RectTransform with the Layout Group that holds the items.")]
        [SerializeField] private RectTransform _container;
        [SerializeField] private List<DraggableItem> _items = new();
        [SerializeField] private DragArrangement _arrangement = DragArrangement.Auto;

        [Header("Animation")]
        [Tooltip("Optional. If empty, a DefaultDragAnimator is added automatically.")]
        [SerializeField] private DragAnimatorBase _animator;
        [SerializeField, Min(0f)] private float _swapDuration = 0.15f;
        [SerializeField] private AnimationCurve _swapEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Drag Layer")]
        [Tooltip("Where lifted items are parented. Empty = the root canvas.")]
        [SerializeField] private RectTransform _dragLayer;
        [Tooltip("Sorting order of the lifted item. Keep below popups and loading overlays.")]
        [SerializeField] private int _dragSortingOrder = 500;

        [Header("Other Lists")]
        [Tooltip("Items of this list can be dropped into other lists.")]
        [SerializeField] private bool _allowTransferOut;
        [Tooltip("This list accepts items coming from other lists.")]
        [SerializeField] private bool _acceptIncoming = true;
        [Tooltip("Items only move between lists that share the same group name (empty matches empty).")]
        [SerializeField] private string _transferGroup = "";

        [Header("Removal Logic")]
        [SerializeField] private bool _draggingOutRemovesEntry;
        [Tooltip("Distance outside the container (in container units) after which a release deletes the item.")]
        [SerializeField, Min(0f)] private float _distanceUntilRemove = 100f;
        [SerializeField] private bool _confirmRemoval;
        [Tooltip("Optional. If empty, the first RemovalConfirmationBase in the scene is used.")]
        [SerializeField] private RemovalConfirmationBase _confirmation;
        [Tooltip("Destroy removed items. Disable to keep them (deactivated) for pooling.")]
        [SerializeField] private bool _destroyOnRemove = true;

        [Header("Removal Animation")]
        [SerializeField, Min(0f)] private float _removeDuration = 0.2f;
        [SerializeField] private AnimationCurve _removeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        #endregion

        #region Public API

        public Canvas RootCanvas => _rootCanvas;
        public RectTransform Container => _container;
        public IReadOnlyList<DraggableItem> Items => _items;
        public int Count => _items.Count;
        public int IndexOf(DraggableItem item) => _items.IndexOf(item);

        internal DragAnimatorBase Animator => _animator;
        internal float SwapDuration => _swapDuration;
        internal AnimationCurve SwapEase => _swapEase;
        internal float RemoveDuration => _removeDuration;
        internal AnimationCurve RemoveEase => _removeEase;
        internal bool DestroyOnRemove => _destroyOnRemove;
        internal int DragSortingOrder => _dragSortingOrder;
        internal RectTransform DragLayer => _dragLayer != null ? _dragLayer : (RectTransform)_rootCanvas.transform;

        internal Camera EventCamera =>
            _rootCanvas == null || _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;

        /// <summary>Order changed (reorder finished, item added/removed, transfer). Read <see cref="Items"/> to sync data.</summary>
        public event Action<IReadOnlyList<DraggableItem>> OrderChanged;
        /// <summary>Raised live, once per step, while an item is dragged over its neighbours.</summary>
        public event Action<DraggableItem, int, int> ItemMoved;
        /// <summary>An item was added with <see cref="AddItem"/>.</summary>
        public event Action<DraggableItem> ItemAdded;
        /// <summary>An item was deleted (drag-out or <see cref="RemoveItem"/>). It is destroyed after its animation.</summary>
        public event Action<DraggableItem> ItemRemoved;
        /// <summary>Raised on the destination list: item, source list, destination list.</summary>
        public event Action<DraggableItem, DraggableList, DraggableList> ItemTransferred;

        #endregion

        #region State

        private static readonly List<DraggableList> s_active = new();

        private readonly List<(DraggableItem item, Vector3 position)> _snapshot = new();

        private DraggableItem _dragged;
        private int _startIndex;
        private int _currentIndex;
        private bool _removalPending;
        private bool _confirming;
        private bool _isGrid;
        private bool _isVertical;
        private Vector2 _axisMask;

        #endregion

        #region Unity

        private void Reset()
        {
            _container = GetComponent<RectTransform>();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _rootCanvas = canvas.rootCanvas;
            GrabListEntries();
        }

        private void OnValidate()
        {
            _distanceUntilRemove = Mathf.Max(0f, _distanceUntilRemove);
        }

        private void Awake()
        {
            if (_container == null) _container = (RectTransform)transform;
            if (_rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _rootCanvas = canvas.rootCanvas;
            }

            if (_rootCanvas == null)
            {
                Debug.LogError($"[{nameof(DraggableList)}] '{name}' needs a Canvas.", this);
                enabled = false;
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponent<DragAnimatorBase>();
                if (_animator == null) _animator = gameObject.AddComponent<DefaultDragAnimator>();
            }

            ResolveArrangement();

            _items.RemoveAll(i => i == null);
            foreach (var item in _items) item.Bind(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear(); // play mode without domain reload

        private void OnEnable() => s_active.Add(this);
        private void OnDisable() => s_active.Remove(this);

        private void ResolveArrangement()
        {
            var group = _container.GetComponent<LayoutGroup>();
            _isVertical = _container.GetComponent<VerticalLayoutGroup>() != null;

            _isGrid = _arrangement switch
            {
                DragArrangement.Grid => true,
                DragArrangement.Linear => false,
                _ => group != null && group is not HorizontalOrVerticalLayoutGroup
            };

            if (group == null)
                Debug.LogWarning($"[{nameof(DraggableList)}] '{name}': the container has no Layout Group.", this);

            // Only the main axis collapses so the cross axis never reflows.
            _axisMask = _isVertical ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
        }

        #endregion

        #region Items

        /// <summary>Rebuilds the item list from the container's direct children (also in the editor).</summary>
        [ContextMenu("Grab List Entries")]
        public void GrabListEntries()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Grab List Entries");
#endif
            if (_container == null) _container = transform as RectTransform;
            _items.Clear();
            if (_container == null) return;

            for (int i = 0; i < _container.childCount; i++)
            {
                var item = _container.GetChild(i).GetComponent<DraggableItem>();
                if (item != null) _items.Add(item);
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Adds an item (new or pooled) at <paramref name="index"/>, or at the end when negative.</summary>
        public void AddItem(DraggableItem item, int index = -1)
        {
            if (item == null || _items.Contains(item)) return;
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);

            item.ResetState();
            CaptureSnapshot();
            InsertInternal(item, index);
            RebuildLayout();
            AnimateNeighbours(item);

            ItemAdded?.Invoke(item);
            OrderChanged?.Invoke(_items);
        }

        /// <summary>Deletes an item with the removal animation (no confirmation).</summary>
        public void RemoveItem(DraggableItem item)
        {
            if (item == null || item == _dragged) return;
            DeleteItem(item, false);
        }

        private void InsertInternal(DraggableItem item, int index)
        {
            if (index < 0 || index > _items.Count) index = _items.Count;

            item.RectTransform.SetParent(_container, false);
            if (index < _items.Count)
                item.RectTransform.SetSiblingIndex(_items[index].RectTransform.GetSiblingIndex());
            else
                item.RectTransform.SetAsLastSibling();

            _items.Insert(index, item);
            item.Bind(this);
        }

        #endregion

        #region Drag callbacks (from DraggableItem)

        internal bool CanStartDrag(DraggableItem item) =>
            _dragged == null && !_confirming && _items.Contains(item);

        internal void NotifyLifted(DraggableItem item)
        {
            _dragged = item;
            _startIndex = _currentIndex = _items.IndexOf(item);
            _removalPending = false;
        }

        internal void NotifyDragAborted(DraggableItem item)
        {
            if (_dragged != item) return;
            _dragged = null;
            _removalPending = false;
        }

        internal void HandleDrag(DraggableItem item, UnityEngine.EventSystems.PointerEventData eventData, bool overDropZone)
        {
            if (item != _dragged) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, eventData.position, EventCamera, out Vector2 local);

            DraggableList hovered = FindTransferTarget(eventData.position);
            bool wantsRemove = _draggingOutRemovesEntry && hovered == null && !overDropZone && IsOutsideRemoveThreshold(local);

            if (wantsRemove != _removalPending)
            {
                _removalPending = wantsRemove;
                CollapsePlaceholder(item, wantsRemove);
            }

            if (_removalPending || hovered != null || overDropZone) return;
            ReorderTowards(local);
        }

        internal void HandleDrop(DraggableItem item, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (item != _dragged) return;
            _dragged = null;

            DraggableList destination = FindTransferTarget(eventData.position);
            if (destination != null)
            {
                _removalPending = false;
                TransferTo(item, destination, eventData.position);
                return;
            }

            if (_removalPending)
            {
                RequestRemoval(item);
                return;
            }

            item.AnimateReturn(_swapDuration, _swapEase);
            if (_currentIndex != _startIndex) OrderChanged?.Invoke(_items);
        }

        /// <summary>The item was released on an accepting <see cref="DropZone"/>: cancel list-side drag state.</summary>
        internal void HandleDroppedOnZone(DraggableItem item, bool returning)
        {
            if (item != _dragged) return;
            _dragged = null;

            if (_removalPending)
            {
                _removalPending = false;
                if (returning) CollapsePlaceholder(item, false); // when consuming, the slot stays collapsed
            }

            if (returning && _currentIndex != _startIndex) OrderChanged?.Invoke(_items);
        }

        internal void NotifyRemovalFinished(DraggableItem item)
        {
            // Closes the gap left by the item (spacing included) and slides the neighbours into it.
            CaptureSnapshot();
            item.SetIgnoreLayout(true);
            RebuildLayout();
            AnimateNeighbours(null);
        }

        #endregion

        #region Reordering

        private void ReorderTowards(Vector2 local)
        {
            if (_items.Count < 2) return;

            if (_isGrid)
            {
                int nearest = GetNearestIndex(local);
                if (nearest != _currentIndex) MoveItem(_currentIndex, nearest);
                return;
            }

            // Linear: step over a neighbour as soon as the pointer passes its centre.
            for (int guard = _items.Count; guard > 0; guard--)
            {
                int c = _currentIndex;
                int next = -1;
                if (c + 1 < _items.Count && PassedCentre(local, c, c + 1)) next = c + 1;
                else if (c - 1 >= 0 && PassedCentre(local, c, c - 1)) next = c - 1;
                if (next < 0) break;
                MoveItem(c, next);
            }
        }

        private bool PassedCentre(Vector2 local, int from, int to)
        {
            Vector2 start = Center(_items[from]);
            Vector2 target = Center(_items[to]);
            Vector2 direction = target - start;
            if (direction.sqrMagnitude < 1e-4f) return false;
            return Vector2.Dot(local - target, direction) > 0f;
        }

        private void MoveItem(int from, int to)
        {
            DraggableItem moved = _items[from];

            CaptureSnapshot();
            // Taking the neighbour's sibling index works even when non-draggable children share the container.
            moved.RectTransform.SetSiblingIndex(_items[to].RectTransform.GetSiblingIndex());
            _items.RemoveAt(from);
            _items.Insert(to, moved);
            RebuildLayout();
            AnimateNeighbours(moved);

            _currentIndex = to;
            ItemMoved?.Invoke(moved, from, to);
        }

        private int GetNearestIndex(Vector2 local)
        {
            int nearest = _currentIndex;
            float best = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
            {
                float d = (Center(_items[i]) - local).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }
            return nearest;
        }

        private int GetInsertIndex(Vector2 screenPoint)
        {
            if (_items.Count == 0) return 0;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screenPoint, EventCamera, out Vector2 local);

            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
            {
                float d = (Center(_items[i]) - local).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }

            Vector2 c = Center(_items[nearest]);
            bool after = !_isGrid && _isVertical ? local.y < c.y : local.x > c.x;
            return nearest + (after ? 1 : 0);
        }

        /// <summary>Centre of an item's slot (the root, not the lifted visual) in container space.</summary>
        private Vector2 Center(DraggableItem item)
        {
            RectTransform rt = item.RectTransform;
            return _container.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
        }

        private void CaptureSnapshot()
        {
            _snapshot.Clear();
            foreach (var item in _items) _snapshot.Add((item, item.ContentsWorldPosition));
        }

        private void RebuildLayout() => LayoutRebuilder.ForceRebuildLayoutImmediate(_container);

        private void AnimateNeighbours(DraggableItem skip)
        {
            foreach (var (item, position) in _snapshot)
            {
                if (item == null || item == skip || !_items.Contains(item)) continue;
                item.AnimateFromWorld(position, _swapDuration, _swapEase);
            }
        }

        private void CollapsePlaceholder(DraggableItem item, bool collapsed)
        {
            if (_isGrid)
            {
                CaptureSnapshot();
                item.SetPlaceholderCollapsed(collapsed, true, _axisMask, 0f, null);
                RebuildLayout();
                AnimateNeighbours(item);
            }
            else
            {
                item.SetPlaceholderCollapsed(collapsed, false, _axisMask, _swapDuration, _swapEase);
            }
        }

        #endregion

        #region Transfer

        private DraggableList FindTransferTarget(Vector2 screenPoint)
        {
            if (!_allowTransferOut) return null;
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                DraggableList other = s_active[i];
                if (other == this || !other.CanReceiveFrom(this)) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(other._container, screenPoint, other.EventCamera))
                    return other;
            }
            return null;
        }

        private bool CanReceiveFrom(DraggableList source) =>
            _acceptIncoming && isActiveAndEnabled && _transferGroup == source._transferGroup;

        private void TransferTo(DraggableItem item, DraggableList destination, Vector2 screenPoint)
        {
            int from = _items.IndexOf(item);
            if (from < 0) return;

            CaptureSnapshot();
            destination.CaptureSnapshot();

            _items.RemoveAt(from);
            item.ResetPlaceholderImmediate();
            destination.InsertInternal(item, destination.GetInsertIndex(screenPoint));

            RebuildLayout();
            destination.RebuildLayout();
            AnimateNeighbours(item);
            destination.AnimateNeighbours(item);

            item.AnimateReturn(destination._swapDuration, destination._swapEase);

            OrderChanged?.Invoke(_items);
            destination.OrderChanged?.Invoke(destination._items);
            destination.ItemTransferred?.Invoke(item, this, destination);
        }

        #endregion

        #region Removal

        private bool IsOutsideRemoveThreshold(Vector2 local)
        {
            Rect rect = _container.rect;
            float dx = Mathf.Max(rect.xMin - local.x, 0f, local.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - local.y, 0f, local.y - rect.yMax);
            if (dx <= 0f && dy <= 0f) return false;
            return new Vector2(dx, dy).magnitude >= _distanceUntilRemove;
        }

        private void RequestRemoval(DraggableItem item)
        {
            if (!_confirmRemoval)
            {
                _removalPending = false;
                DeleteItem(item, true);
                return;
            }

            if (_confirmation == null)
                _confirmation = FindFirstObjectByType<RemovalConfirmationBase>(FindObjectsInactive.Include);

            if (_confirmation == null)
            {
                Debug.LogWarning($"[{nameof(DraggableList)}] '{name}' asks for confirmation but no {nameof(RemovalConfirmationBase)} exists. Removing without asking.", this);
                _removalPending = false;
                DeleteItem(item, true);
                return;
            }

            _confirming = true;
            bool resolved = false;

            _confirmation.RequestConfirmation(item,
                () =>
                {
                    if (resolved) return;
                    resolved = true;
                    if (this == null) return;
                    _confirming = false;
                    _removalPending = false;
                    if (item != null) DeleteItem(item, true);
                },
                () =>
                {
                    if (resolved) return;
                    resolved = true;
                    if (this == null) return;
                    _confirming = false;
                    _removalPending = false;
                    if (item != null) CancelRemoval(item);
                });
        }

        private void CancelRemoval(DraggableItem item)
        {
            if (!_items.Contains(item)) return;
            CollapsePlaceholder(item, false);
            item.AnimateReturn(_swapDuration, _swapEase);
        }

        private void DeleteItem(DraggableItem item, bool alreadyCollapsed)
        {
            int index = _items.IndexOf(item);
            if (index < 0) return;

            if (!alreadyCollapsed) CollapsePlaceholder(item, true);
            _items.RemoveAt(index);

            item.PlayRemoval(_removeDuration, _removeEase, _destroyOnRemove);

            OrderChanged?.Invoke(_items);
            ItemRemoved?.Invoke(item);
        }

        #endregion
    }
}
