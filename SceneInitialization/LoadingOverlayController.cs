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
        private Image[] _overlayImages;

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
            foreach (Image image in _overlayImages)
            {
                Color color = image.color;
                color.a = visible ? 1f : 0f;
                image.color = color;

                image.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Fades the overlay in from its current alpha to fully opaque.
        /// </summary>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeInAsync(CancellationToken ct)
        {
            foreach (Image image in _overlayImages)
            {
                image.gameObject.SetActive(true);
            }
            await FadeAlphaAsync(1f, ct);
        }

        /// <summary>
        /// Fades the overlay out from its current alpha to fully transparent, then deactivates the GameObject.
        /// </summary>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeOutAsync(CancellationToken ct)
        {
            await FadeAlphaAsync(0f, ct);
            foreach (Image image in _overlayImages)
            {
                image.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Smoothly interpolates the overlay's alpha to the target value over <see cref="_fadeDuration"/> seconds.
        /// </summary>
        /// <param name="alpha">Target alpha value (clamped between 0 and 1).</param>
        /// <param name="ct">Cancellation token to stop the fade operation.</param>
        public async Awaitable FadeAlphaAsync(float alpha, CancellationToken ct)
        {
            // Cache the starting alpha for each image before the loop starts
            int count = _overlayImages.Length;
            float[] startAlphas = new float[count];

            for (int i = 0; i < count; i++)
            {
                startAlphas[i] = _overlayImages[i].color.a;
            }

            float elapsedTime = 0f;
            ct.ThrowIfCancellationRequested();

            while (elapsedTime < _fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / _fadeDuration);

                for (int i = 0; i < count; i++)
                {
                    Color color = _overlayImages[i].color;
                    color.a = Mathf.Lerp(startAlphas[i], alpha, t);
                    _overlayImages[i].color = color;
                }

                await Awaitable.NextFrameAsync();
            }

            // Ensure final values are exact
            for (int i = 0; i < count; i++)
            {
                Color finalColor = _overlayImages[i].color;
                finalColor.a = alpha;
                _overlayImages[i].color = finalColor;
            }
        }
        #endregion
    }
}
