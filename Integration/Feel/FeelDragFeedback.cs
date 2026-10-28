using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using Giana.UnityUtils;

namespace Giana.UnityUtils.FEEL
{
    /// <summary>
    /// Plays Feel (<see cref="MMF_Player"/>) feedbacks for a <see cref="DraggableList"/>: lift, reorder step,
    /// drop, transfer and removal. Add it next to the list; every slot is optional.
    /// </summary>
    [RequireComponent(typeof(DraggableList))]
    public class FeelDragFeedback : MonoBehaviour
    {
        [Header("Feedbacks (all optional)")]
        [SerializeField] private MMF_Player _onLift;
        [Tooltip("Played every time the dragged item passes a neighbour.")]
        [SerializeField] private MMF_Player _onReorderStep;
        [SerializeField] private MMF_Player _onDrop;
        [Tooltip("Played on the destination list's component when an item arrives from another list.")]
        [SerializeField] private MMF_Player _onTransferIn;
        [SerializeField] private MMF_Player _onRemove;

        private DraggableList _list;
        private readonly HashSet<DraggableItem> _subscribed = new();

        private void Awake() => _list = GetComponent<DraggableList>();

        private void OnEnable()
        {
            _list.ItemMoved += HandleMoved;
            _list.ItemAdded += Subscribe;
            _list.ItemRemoved += HandleRemoved;
            _list.ItemTransferred += HandleTransferred;
            foreach (var item in _list.Items) Subscribe(item);
        }

        private void OnDisable()
        {
            if (_list != null)
            {
                _list.ItemMoved -= HandleMoved;
                _list.ItemAdded -= Subscribe;
                _list.ItemRemoved -= HandleRemoved;
                _list.ItemTransferred -= HandleTransferred;
            }

            foreach (var item in _subscribed)
            {
                if (item == null) continue;
                item.Lifted -= HandleLifted;
                item.Dropped -= HandleDropped;
            }
            _subscribed.Clear();
        }

        private void Subscribe(DraggableItem item)
        {
            if (item == null || !_subscribed.Add(item)) return;
            item.Lifted += HandleLifted;
            item.Dropped += HandleDropped;
        }

        // Items can move to another list; only react while they still belong to ours.
        private void HandleLifted(DraggableItem item) { if (item.Owner == _list) Play(_onLift, item); }
        private void HandleDropped(DraggableItem item) { if (item.Owner == _list) Play(_onDrop, item); }
        private void HandleMoved(DraggableItem item, int from, int to) => Play(_onReorderStep, item);
        private void HandleRemoved(DraggableItem item) => Play(_onRemove, item);

        private void HandleTransferred(DraggableItem item, DraggableList from, DraggableList to)
        {
            Subscribe(item);
            Play(_onTransferIn, item);
        }

        private static void Play(MMF_Player player, DraggableItem item)
        {
            if (player == null) return;
            player.PlayFeedbacks(item != null ? item.RectTransform.position : player.transform.position);
        }
    }
}
