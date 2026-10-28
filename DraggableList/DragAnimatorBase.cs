using System;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// The independent animation "lanes" used by the draggable list.
    /// Each (owner, channel) pair runs at most one animation at a time.
    /// </summary>
    public enum DragAnimationChannel
    {
        /// <summary>Visual sliding to a new slot or back home after a drop.</summary>
        Move,
        /// <summary>Placeholder collapsing / expanding (smooth layouts).</summary>
        Placeholder,
        /// <summary>Scale-down when an item is being deleted.</summary>
        Removal
    }

    /// <summary>
    /// Animation backend for <see cref="DraggableList"/>. Same idea as <see cref="MenuAnimatorBase"/>:
    /// the list never animates on its own, it asks an animator to drive a normalized value.
    /// </summary>
    /// <remarks>
    /// Contract: <c>onUpdate</c> receives the eased progress (0 → 1, overshoot allowed) and is always called
    /// once with the final value before <c>onComplete</c>. Starting a new animation on the same
    /// (owner, channel) replaces the old one without calling its <c>onComplete</c>.
    /// Implementations must use unscaled time. The animator GameObject has to stay active.
    /// </remarks>
    public abstract class DragAnimatorBase : MonoBehaviour
    {
        public abstract void Play(UnityEngine.Object owner, DragAnimationChannel channel, float duration,
            AnimationCurve ease, Action<float> onUpdate, Action onComplete);

        /// <summary>Stops one animation without invoking its completion callback.</summary>
        public abstract void Kill(UnityEngine.Object owner, DragAnimationChannel channel);

        /// <summary>Stops every animation of <paramref name="owner"/> without invoking callbacks.</summary>
        public abstract void KillAll(UnityEngine.Object owner);

        protected static float Evaluate(AnimationCurve ease, float t)
        {
            t = Mathf.Clamp01(t);
            return ease != null && ease.length > 0 ? ease.Evaluate(t) : t;
        }
    }
}
