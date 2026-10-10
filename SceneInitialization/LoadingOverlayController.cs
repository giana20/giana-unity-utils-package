using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Controls the fade in/out and instant visibility of a loading overlay image.
    /// </summary>
    public class LoadingOverlayController : MonoBehaviour
    {
        #region Events
        // No events declared.
        #endregion

        #region Inspector Fields
        [Header("Fade Settings")]
        [Tooltip("The Image component used as the full-screen overlay.")]
        [SerializeField]
        private Image _overlayImage;

        [Tooltip("Duration of the fade transition in seconds.")]
        [Min(0f)]
        [SerializeField]
        private float _fadeDuration = 0.4f;
        #endregion

        #region Properties & Private Fields
        // No additional private fields or properties.
        #endregion

        #region Unity Lifecycle
        // No Unity lifecycle methods are implemented.
        #endregion

        #region Fade & Overlay Control
        /// <summary>
        /// Sets the overlay to be instantly fully visible or invisible.
        /// </summary>
        /// <param name="visible">True to show the overlay, false to hide it instantly.</param>
        public void SetInstant(bool visible)
        {
            Color color = _overlayImage.color;
            color.a = visible ? 1f : 0f;
            _overlayImage.color = color;

            _overlayImage.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Fades the overlay in from its current alpha to fully opaque.
        /// </summary>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeInAsync(CancellationToken ct)
        {
            _overlayImage.gameObject.SetActive(true);
            await FadeAlpahAsyn(1f, ct);
        }

        /// <summary>
        /// Fades the overlay out from its current alpha to fully transparent, then deactivates the GameObject.
        /// </summary>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeOutAsync(CancellationToken ct)
        {
            await FadeAlpahAsyn(0f, ct);
            _overlayImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// Smoothly interpolates the overlay's alpha to the target value over <see cref="_fadeDuration"/> seconds.
        /// </summary>
        /// <param name="alpha">Target alpha value (clamped between 0 and 1).</param>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeAlpahAsyn(float alpha, CancellationToken ct)
        {
            float startAlpha = _overlayImage.color.a;
            float elapsedTime = 0;

            ct.ThrowIfCancellationRequested();

            while (elapsedTime < _fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / _fadeDuration);

                Color color = _overlayImage.color;
                color.a = Mathf.Lerp(startAlpha, alpha, t);
                _overlayImage.color = color;

                await Awaitable.NextFrameAsync();
            }

            Color finalColor = _overlayImage.color;
            finalColor.a = alpha;
            _overlayImage.color = finalColor;
        }
        #endregion
    }
}
