using UnityEngine;

namespace Giana.UnityUtils
{
    public abstract class MenuAnimatorBase : MonoBehaviour
    {
        /// <summary>
        /// Animates the target's selection state.
        /// </summary>
        public abstract void AnimateSelect(Transform target, Vector3 toScale, float duration, bool unscaledTime);

        /// <summary>
        /// Animates the target's deselection state.
        /// </summary>
        public abstract void AnimateDeselect(Transform target, Vector3 toScale, float duration, bool unscaledTime);

        /// <summary>
        /// Cancels any running animation on the target. If target is null, cancels all running animations.
        /// </summary>
        public abstract void Kill(Transform target = null);
    }
}
