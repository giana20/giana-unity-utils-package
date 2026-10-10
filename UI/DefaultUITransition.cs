using System;
using System.Collections;
using UnityEngine;

namespace Giana.UnityUtils
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DefaultUITransition : MonoBehaviour, IUITransition
    {
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private Vector3 _startOffset = new Vector3(0, -50f, 0);

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Vector3 _originalPosition;
        private Coroutine _currentTransition;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            _originalPosition = _rectTransform.anchoredPosition3D;
        }

        public void PlayEnterAnimation(float delay)
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }
            
            // Setup initial state immediately
            _canvasGroup.alpha = 0f;
            _rectTransform.anchoredPosition3D = _originalPosition + _startOffset;
            
            if (gameObject.activeInHierarchy)
            {
                _currentTransition = StartCoroutine(EnterRoutine(delay));
            }
            else
            {
                _canvasGroup.alpha = 1f;
                _rectTransform.anchoredPosition3D = _originalPosition;
            }
        }

        public void PlayExitAnimation(Action onAnimationComplete)
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }
            
            if (gameObject.activeInHierarchy)
            {
                _currentTransition = StartCoroutine(ExitRoutine(onAnimationComplete));
            }
            else
            {
                _canvasGroup.alpha = 0f;
                _rectTransform.anchoredPosition3D = _originalPosition + _startOffset;
                onAnimationComplete?.Invoke();
            }
        }

        private IEnumerator EnterRoutine(float delay)
        {
            if (delay > 0)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            float time = 0f;
            Vector3 startPos = _rectTransform.anchoredPosition3D;

            while (time < _duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / _duration);
                
                // Ease out
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, ease);
                _rectTransform.anchoredPosition3D = Vector3.Lerp(startPos, _originalPosition, ease);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _rectTransform.anchoredPosition3D = _originalPosition;
            _currentTransition = null;
        }

        private IEnumerator ExitRoutine(Action onAnimationComplete)
        {
            float time = 0f;
            Vector3 startPos = _rectTransform.anchoredPosition3D;
            float startAlpha = _canvasGroup.alpha;

            while (time < _duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / _duration);
                
                // Ease in
                float ease = t * t * t;
                
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, ease);
                _rectTransform.anchoredPosition3D = Vector3.Lerp(startPos, _originalPosition + _startOffset, ease);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _rectTransform.anchoredPosition3D = _originalPosition + _startOffset;
            _currentTransition = null;
            onAnimationComplete?.Invoke();
        }
    }
}
