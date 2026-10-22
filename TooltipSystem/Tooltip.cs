using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Displays a tooltip UI that follows the mouse cursor and automatically flips its pivot
    /// based on which screen quadrant the cursor is in, keeping the tooltip fully on-screen.
    /// </summary>
    public class Tooltip : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("Text field used to display the tooltip header. Optional; hidden when header is empty.")]
        [SerializeField]
        private TextMeshProUGUI _headerField;

        [Tooltip("Text field used to display the tooltip content body.")]
        [SerializeField]
        private TextMeshProUGUI _contentField;

        [Tooltip("Layout element controlling the tooltip's size. Enabled when text exceeds the wrap limit.")]
        [SerializeField]
        private LayoutElement _layoutElement;

        [Tooltip("Character count threshold at which the layout element is enabled to allow text wrapping.")]
        [SerializeField]
        private int _characterWrapLimit = 50;

        [Tooltip("Enables or disables debug logging for tooltip operations in the Unity Editor.")]
        [SerializeField]
        private bool _showDebugLogs = false;

        #endregion

        #region Private Variables

        private RectTransform _rectTransform;

        // Pivot margins to anchor the tooltip away from the cursor in each screen quadrant.
        // Extracted from magic numbers for maintainability and clarity.
        private const float PivotInnerMargin = -0.05f;
        private const float PivotOuterMargin = 1.05f;
        private const float ScreenHalf = 0.5f;

        private static readonly Vector2 PivotTopLeft = new(PivotInnerMargin, PivotOuterMargin);
        private static readonly Vector2 PivotTopRight = new(PivotOuterMargin, PivotOuterMargin);
        private static readonly Vector2 PivotBottomLeft = new(PivotInnerMargin, PivotInnerMargin);
        private static readonly Vector2 PivotBottomRight = new(PivotOuterMargin, PivotInnerMargin);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            AdjustPosition();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the header and content text of the tooltip and configures wrapping behavior.
        /// </summary>
        /// <param name="content">The body text of the tooltip.</param>
        /// <param name="header">The header text. If null or empty, the header is hidden.</param>
        public void SetText(string content, string header)
        {
            if (_headerField == null || _contentField == null || _layoutElement == null)
            {
#if UNITY_EDITOR
                if (_showDebugLogs)
                {
                    Debug.LogWarning("[Tooltip] Missing required UI references (header, content, or layout element).");
                }
#endif
                return;
            }

            AdjustPosition();

            if (string.IsNullOrEmpty(header))
            {
                _headerField.gameObject.SetActive(false);
            }
            else
            {
                _headerField.gameObject.SetActive(true);
                _headerField.text = header;
            }

            _contentField.text = content;

            int headerLength = string.IsNullOrEmpty(header) ? 0 : header.Length;
            int contentLength = _contentField.text.Length;
            
            _layoutElement.enabled = headerLength >= _characterWrapLimit || contentLength >= _characterWrapLimit;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Determines the pivot for the tooltip based on which screen quadrant the cursor is in,
        /// ensuring the tooltip grows away from the cursor and stays visible.
        /// </summary>
        /// <param name="normalizedPosition">Cursor position normalized to [0,1] screen space.</param>
        /// <returns>The pivot vector to apply to the tooltip's RectTransform.</returns>
        private Vector2 CalculatePivot(Vector2 normalizedPosition)
        {
            if (normalizedPosition.x < ScreenHalf && normalizedPosition.y >= ScreenHalf)
            {
                return PivotTopLeft;
            }
            
            if (normalizedPosition.x >= ScreenHalf && normalizedPosition.y >= ScreenHalf)
            {
                return PivotTopRight;
            }
            
            if (normalizedPosition.x < ScreenHalf && normalizedPosition.y < ScreenHalf)
            {
                return PivotBottomLeft;
            }

            return PivotBottomRight;
        }

        /// <summary>
        /// Calculate and set the position of the tooltip based on the mouse position and calculated pivot point.
        /// </summary>
        private void AdjustPosition()
        {
            if(_rectTransform == null)
                return;

            Vector2 mousePos = Pointer.current.position.ReadValue();
            Vector2 normalizedPosition = new(mousePos.x / Screen.width, mousePos.y / Screen.height);
            
            Vector2 pivot = CalculatePivot(normalizedPosition);
            _rectTransform.pivot = pivot;
            transform.position = mousePos;
        }

        #endregion
    }
}
