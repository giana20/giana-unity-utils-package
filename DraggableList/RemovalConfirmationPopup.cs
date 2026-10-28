using System;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Ready-made yes/no confirmation. Instance based (no static events): the list passes the callbacks.
    /// The popup is hidden on start and drawn above the drag layer.
    /// </summary>
    public class RemovalConfirmationPopup : RemovalConfirmationBase
    {
        [Header("References")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Sorting")]
        [Tooltip("Must be higher than the list's Drag Sorting Order (default 500).")]
        [SerializeField] private int _sortingOrder = 600;

        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            // Own canvas so the popup renders above the dragged visual; it needs its own raycaster.
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _sortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Cancel);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Cancel);
        }

        private void OnDisable() => Cancel();

        public override void RequestConfirmation(DraggableItem item, Action onConfirm, Action onCancel)
        {
            Cancel(); // a previous pending request is cancelled, never left dangling
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            SetVisible(true);
        }

        private void Confirm()
        {
            var callback = _onConfirm;
            _onConfirm = _onCancel = null;
            SetVisible(false);
            callback?.Invoke();
        }

        private void Cancel()
        {
            var callback = _onCancel;
            _onConfirm = _onCancel = null;
            SetVisible(false);
            callback?.Invoke();
        }

        private void SetVisible(bool value)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = value ? 1f : 0f;
            _canvasGroup.interactable = value;
            _canvasGroup.blocksRaycasts = value;
        }
    }
}
