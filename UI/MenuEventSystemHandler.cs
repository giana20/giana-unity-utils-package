using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Handles UI menu navigation with selection animations and sound feedback.
    /// Listens to selection, deselection, and pointer events on a list of <see cref="Selectable"/> elements.
    /// </summary>
    public class MenuEventSystemHandler : EventSystemHandlerBase, IInitializeAwake
    {
        #region Inspector Fields
        
        [Header("Initialization")]
        [Tooltip("Priority for the Awake initialization step (higher executes first).")]
        [field: SerializeField]
        public int AwakePriority { get; set; }

        [Tooltip("If true it set this gameobject as active on awake.")]
        [SerializeField]
        private bool _showOnAwake = false;

        [Space(10)]
        [Header("References")]
        [Tooltip("List of UI Selectable elements managed by this handler.")]
        [SerializeField]
        protected List<Selectable> _selectables;

        [Tooltip("Reference to the first selectable to be focused when the menu becomes active.")]
        [SerializeField]
        protected Selectable _firstSelected;

        [Space(10)]
        [Header("Animation")]
        [Tooltip("GameObjects that should not receive scaling animations on selection.")]
        [SerializeField]
        protected List<GameObject> _animationExclusion = new List<GameObject>();

        #endregion

        #region Unity Lifecycle

#pragma warning disable CS1998
        /// <summary>
        /// Registers selection listeners and caches original scales for all managed selectables.
        /// </summary>
        public async Awaitable InitializeAwake(CancellationToken ct)
        {
            gameObject.SetActive(false);
            ResolveAnimator(); // Ensure animator is resolved here if Awake wasn't called yet

            foreach (Selectable selectable in _selectables)
            {
                if (selectable == null) continue;
                AddSelectionListener(selectable);
                _scales.TryAdd(selectable, selectable.transform.localScale);
            }

            if(_showOnAwake)
                gameObject.SetActive(true);
        }
#pragma warning restore CS1998

        protected override void OnEnable()
        {
            base.OnEnable();

            // Reset scales to original values in case tweens were interrupted
            foreach (Selectable selectable in _selectables)
            {
                if (selectable != null && _scales.TryGetValue(selectable, out Vector3 originalScale))
                {
                    selectable.transform.localScale = originalScale;
                }
            }

            StartCoroutine(SelectAfterDelay());
        }

        #endregion

        #region Selection Overrides

        protected override bool IsExcluded(GameObject obj)
        {
            return _animationExclusion.Contains(obj);
        }

        #endregion

        #region Coroutines

        /// <summary>
        /// Waits one frame and then sets the first selected object as the current selected GameObject.
        /// </summary>
        protected virtual IEnumerator SelectAfterDelay()
        {
            yield return null;
            if (_firstSelected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_firstSelected.gameObject);
            }
        }

        #endregion
    }
}
