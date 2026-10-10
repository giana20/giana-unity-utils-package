using System;
using System.Collections;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Dependency-free <see cref="IUITransition"/>: fades, scales and slides the child "Visual" object in and out.
    /// Must live on the SAME GameObject as the Selectable (UIBuilder looks it up there).
    ///
    /// Why a child? The event system handlers scale the ROOT of every Selectable on hover/selection.
    /// If the enter/exit animation also drove the root scale, the two would fight and the handler could
    /// cache a wrong "original scale". So: root = hover scale, "Visual" child = enter/exit animation.
    ///
    /// Uses unscaled time, so menus still animate while Time.timeScale = 0 (pause menus).
    /// To use DOTween or Feel instead, write another IUITransition (see FeelUITransition) and swap the component.
    /// </summary>
    public class SampleUITransition : MonoBehaviour, IUITransition
    {
        [Header("Targets")]
        [SerializeField]
        [Tooltip("The animated child. Should stretch to fill the root.")]
        private RectTransform _visual;

        [SerializeField]
        [Tooltip("CanvasGroup on the Visual child, used for the fade.")]
        private CanvasGroup _visualGroup;

        [Header("Timing")]
        [SerializeField, Min(0.01f)]
        private float _enterDuration = 0.35f;

        [SerializeField, Min(0.01f)]
        private float _exitDuration = 0.2f;

        [Header("Look")]
        [SerializeField, Range(0f, 1f)]
        private float _hiddenScale = 0.85f;

        [SerializeField]
        [Tooltip("The Visual starts this far from its resting position and slides in.")]
        private Vector2 _hiddenOffset = new(0f, -40f);

        private Vector2 _restPosition;
        private Coroutine _running;

        private void Awake()
        {
            _restPosition = _visual.anchoredPosition;
        }

        /// <summary>Auto-fills the references when the component is added in the editor.</summary>
        private void Reset()
        {
            Transform child = transform.Find("Visual");
            if (child != null)
            {
                _visual = child as RectTransform;
                _visualGroup = child.GetComponent<CanvasGroup>();
            }
        }

        public void PlayEnterAnimation(float delay)
        {
            Stop();
            Apply(0f); // hidden immediately, so the item doesn't flash while waiting for its stagger delay
            _running = StartCoroutine(Animate(delay, _enterDuration, 0f, 1f, null));
        }

        public void PlayExitAnimation(Action onAnimationComplete)
        {
            Stop();
            _running = StartCoroutine(Animate(0f, _exitDuration, 1f, 0f, onAnimationComplete));
        }

        private void Stop()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
        }

        private IEnumerator Animate(float delay, float duration, float from, float to, Action done)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f); // ease-out cubic
                Apply(Mathf.Lerp(from, to, eased));
                yield return null;
            }

            Apply(to);
            _running = null;
            done?.Invoke(); // the builder destroys the item after the LAST exit callback
        }

        /// <param name="shown">0 = fully hidden, 1 = fully shown.</param>
        private void Apply(float shown)
        {
            _visual.localScale = Vector3.one * Mathf.Lerp(_hiddenScale, 1f, shown);
            _visual.anchoredPosition = _restPosition + _hiddenOffset * (1f - shown);
            _visualGroup.alpha = shown;
        }
    }
}
