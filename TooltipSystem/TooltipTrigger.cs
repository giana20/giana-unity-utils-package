using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Handles pointer and mouse events to trigger the display and hiding of a tooltip.
    /// Implements both EventSystem interfaces and legacy mouse events for broader compatibility.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Inspector Fields

        [Tooltip("The header text to display in the tooltip.")]
        [SerializeField]
        private string _header = string.Empty;

        [Tooltip("The main content text to display in the tooltip.")]
        [SerializeField, TextArea]
        private string _content = string.Empty;

        [Tooltip("The delay in seconds before the tooltip is shown after pointer enter.")]
        [SerializeField]
        private float _delayedShowupTime = 0.1f;

        [Tooltip("Enables or disables debug logging for tooltip trigger events in the Unity Editor.")]
        [SerializeField]
        private bool _showDebugLogs = false;

        #endregion

        #region Private Variables

        private Coroutine _delayedShowCoroutine;

        #endregion

        #region Unity Lifecycle & Event Handlers

        /// <summary>
        /// Called when the pointer enters the object's collider or UI element.
        /// </summary>
        /// <param name="eventData">The pointer event data.</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(_delayedShowCoroutine != null)
                StopCoroutine(_delayedShowCoroutine);
            _delayedShowCoroutine = StartCoroutine(ShowAfterDelay());
        }

        /// <summary>
        /// Called when the pointer exits the object's collider or UI element.
        /// </summary>
        /// <param name="eventData">The pointer event data.</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_delayedShowCoroutine != null)
            {
                StopCoroutine(_delayedShowCoroutine);
                _delayedShowCoroutine = null;
            }
            TooltipSystem.Instance.Hide();
        }

        /// <summary>
        /// Legacy mouse event called when the mouse enters the object's collider.
        /// Acts as a fallback for non-UI elements without a Physics Raycaster.
        /// </summary>
        private void OnMouseEnter()
        {
            if(_delayedShowCoroutine != null)
                StopCoroutine(_delayedShowCoroutine);
            _delayedShowCoroutine = StartCoroutine(ShowAfterDelay());
        }

        /// <summary>
        /// Legacy mouse event called when the mouse exits the object's collider.
        /// </summary>
        private void OnMouseExit()
        {
            if (_delayedShowCoroutine != null)
            {
                StopCoroutine(_delayedShowCoroutine);
                _delayedShowCoroutine = null;
            }
            TooltipSystem.Instance.Hide();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Coroutine that waits for a specified delay before showing the tooltip.
        /// </summary>
        /// <returns>An IEnumerator for the coroutine.</returns>
        public IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_delayedShowupTime);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[TooltipTrigger] Showing tooltip for: {gameObject.name}");
            }
#endif
            TooltipSystem.Instance.Show(_content, _header);
        }

        #endregion
    }
}