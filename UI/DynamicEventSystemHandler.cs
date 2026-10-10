using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Dynamically manages UI menu navigation with selection animations and sound feedback.
    /// Supports runtime addition of Selectables and manual initialization.
    /// </summary>
    public class DynamicEventSystemHandler : EventSystemHandlerBase
    {
        #region Inspector Fields

        [Header("References")]
        [Space(10)]
        [SerializeField]
        [Tooltip("List of UI Selectable elements managed by this handler.")]
        private List<Selectable> _persistentSelectables = new List<Selectable>();
        
        private List<Selectable> _dynamicSelectables = new List<Selectable>();

        [SerializeField]
        [Tooltip("The first selected game object. If null the first selected game object will be the first dynamic selectable")]
        private Selectable _firstSelectable;

        #endregion

        #region Unity Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();

            // Reset scales for all known Selectables using safe TryGetValue
            foreach (Selectable selectable in _dynamicSelectables)
            {
                if (selectable != null && _scales.TryGetValue(selectable, out Vector3 originalScale))
                {
                    selectable.transform.localScale = originalScale;
                }
            }

            StartCoroutine(SelectAfterDelay());
        }

        #endregion

        #region Coroutines

        /// <summary>
        /// Waits one frame and then sets the first selectable in the list as the current selected GameObject.
        /// </summary>
        protected virtual IEnumerator SelectAfterDelay()
        {
            yield return null;
            if (EventSystem.current != null)
            {
                if (_firstSelectable != null)
                {
                    EventSystem.current.SetSelectedGameObject(_firstSelectable.gameObject);
                }
                else if (_dynamicSelectables.Count > 0 && _dynamicSelectables[0] != null)
                {
                    EventSystem.current.SetSelectedGameObject(_dynamicSelectables[0].gameObject);
                }
            }
        }

        #endregion

        #region Public Helper Methods

        /// <summary>
        /// Adds a new Selectable to the managed list and initializes its event listeners if not already present.
        /// </summary>
        public void AddSelectable(Selectable selectable)
        {
            if (selectable == null || _dynamicSelectables.Contains(selectable))
                return;

            _dynamicSelectables.Add(selectable);
        }

        public void ClearSelectable()
        {
            foreach (var selectable in _dynamicSelectables)
            {
                RemoveSelectionListener(selectable);
            }
            _dynamicSelectables.Clear();
        }

        /// <summary>
        /// Initializes all Selectables in the list by adding event listeners and storing their original scales.
        /// Should be called after adding Selectables via AddSelectable or before OnEnable.
        /// </summary>
        public void InitSelectable()
        {
            ResolveAnimator(); // Ensure animator is resolved here if called before Awake

            foreach (Selectable selectable in _dynamicSelectables)
            {
                if (selectable == null) continue;
                AddSelectionListener(selectable);
                _scales.TryAdd(selectable, selectable.transform.localScale);
            }
            foreach (Selectable selectable in _persistentSelectables)
            {
                if (selectable == null) continue;
                AddSelectionListener(selectable);
                _scales.TryAdd(selectable, selectable.transform.localScale);
            }
        }

        /// <summary>
        /// Triggers a delayed selection of the first selectable in the list.
        /// </summary>
        public void SetFirstSelected()
        {
            StartCoroutine(SelectAfterDelay());
        }

        #endregion
    }
}
