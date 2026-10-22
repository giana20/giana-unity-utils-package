using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Manages the global visibility and content of the tooltip UI.
    /// Inherits from MonoSingleton to ensure a single, accessible instance across the application.
    /// </summary>
    public class TooltipSystem : MonoSingleton<TooltipSystem>
    {
        #region Inspector Fields

        [Tooltip("Reference to the Tooltip component responsible for rendering the UI.")]
        [SerializeField]
        private Tooltip _tooltip;

        [Tooltip("Enables or disables debug logging for tooltip operations in the Unity Editor.")]
        [SerializeField]
        private bool _showDebugLogs = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Displays the tooltip with the specified content and optional header.
        /// </summary>
        /// <param name="content">The main body text to display in the tooltip.</param>
        /// <param name="header">The optional header text. Defaults to an empty string.</param>
        public void Show(string content, string header = "")
        {
            _tooltip.SetText(content, header);
            _tooltip.gameObject.SetActive(true);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[TooltipSystem] Tooltip shown. Header: '{header}'");
            }
#endif
        }

        /// <summary>
        /// Hides the tooltip UI.
        /// </summary>
        public void Hide()
        {
            _tooltip.gameObject.SetActive(false);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log("[TooltipSystem] Tooltip hidden.");
            }
#endif
        }

        #endregion
    }
}