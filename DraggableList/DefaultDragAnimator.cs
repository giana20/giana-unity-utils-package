using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Dependency-free coroutine animator (unscaled time). Used automatically when a
    /// <see cref="DraggableList"/> has no animator assigned.
    /// </summary>
    public class DefaultDragAnimator : DragAnimatorBase
    {
        private readonly Dictionary<(UnityEngine.Object, DragAnimationChannel), Coroutine> _running = new();
        private readonly List<(UnityEngine.Object, DragAnimationChannel)> _scratch = new();

        public override void Play(UnityEngine.Object owner, DragAnimationChannel channel, float duration,
            AnimationCurve ease, Action<float> onUpdate, Action onComplete)
        {
            if (owner == null) return;
            Kill(owner, channel);

            // Nothing to animate (or we can't run coroutines): jump to the end state.
            if (duration <= 0f || !isActiveAndEnabled)
            {
                onUpdate?.Invoke(Evaluate(ease, 1f));
                onComplete?.Invoke();
                return;
            }

            var key = (owner, channel);
            _running[key] = StartCoroutine(Run(key, duration, ease, onUpdate, onComplete));
        }

        public override void Kill(UnityEngine.Object owner, DragAnimationChannel channel)
        {
            var key = (owner, channel);
            if (!_running.TryGetValue(key, out var routine)) return;
            if (routine != null) StopCoroutine(routine);
            _running.Remove(key);
        }

        public override void KillAll(UnityEngine.Object owner)
        {
            _scratch.Clear();
            foreach (var key in _running.Keys)
                if (key.Item1 == owner) _scratch.Add(key);
            foreach (var key in _scratch) Kill(key.Item1, key.Item2);
        }

        private IEnumerator Run((UnityEngine.Object owner, DragAnimationChannel channel) key, float duration,
            AnimationCurve ease, Action<float> onUpdate, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (key.owner == null) { _running.Remove(key); yield break; }
                elapsed += Time.unscaledDeltaTime;
                onUpdate?.Invoke(Evaluate(ease, elapsed / duration));
                yield return null;
            }

            if (key.owner == null) { _running.Remove(key); yield break; }
            onUpdate?.Invoke(Evaluate(ease, 1f));
            _running.Remove(key);
            onComplete?.Invoke();
        }
    }
}
