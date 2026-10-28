using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Giana.UnityUtils;

namespace Giana.UnityUtils.DoTween
{
    /// <summary>
    /// DOTween backend for <see cref="DraggableList"/>. Drives the normalized progress with
    /// <c>DOVirtual.Float</c>, so every channel can use a DOTween <see cref="Ease"/> instead of the list's curves.
    /// </summary>
    public class DOTweenDragAnimator : DragAnimatorBase
    {
        [Header("Ease")]
        [Tooltip("On: use the eases below (the list's AnimationCurves are ignored). Off: use the list's curves.")]
        [SerializeField] private bool _overrideCurves = true;
        [SerializeField] private Ease _moveEase = Ease.OutCubic;
        [SerializeField] private Ease _placeholderEase = Ease.OutCubic;
        [SerializeField] private Ease _removalEase = Ease.InBack;

        private readonly Dictionary<(UnityEngine.Object, DragAnimationChannel), Tween> _tweens = new();
        private readonly List<(UnityEngine.Object, DragAnimationChannel)> _scratch = new();

        public override void Play(UnityEngine.Object owner, DragAnimationChannel channel, float duration,
            AnimationCurve ease, Action<float> onUpdate, Action onComplete)
        {
            if (owner == null) return;
            Kill(owner, channel);

            if (duration <= 0f)
            {
                onUpdate?.Invoke(1f);
                onComplete?.Invoke();
                return;
            }

            var key = (owner, channel);
            Tween tween = null;
            tween = DOVirtual.Float(0f, 1f, duration, value =>
                {
                    if (owner == null) { tween?.Kill(); return; }
                    onUpdate?.Invoke(value);
                })
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnKill(() =>
                {
                    if (_tweens.TryGetValue(key, out var current) && current == tween) _tweens.Remove(key);
                })
                .OnComplete(() => onComplete?.Invoke());

            if (_overrideCurves) tween.SetEase(EaseFor(channel));
            else if (ease != null && ease.length > 0) tween.SetEase(ease);
            else tween.SetEase(Ease.Linear);

            _tweens[key] = tween;
        }

        public override void Kill(UnityEngine.Object owner, DragAnimationChannel channel)
        {
            if (_tweens.TryGetValue((owner, channel), out var tween)) tween.Kill();
            _tweens.Remove((owner, channel));
        }

        public override void KillAll(UnityEngine.Object owner)
        {
            _scratch.Clear();
            foreach (var key in _tweens.Keys)
                if (key.Item1 == owner) _scratch.Add(key);
            foreach (var key in _scratch) Kill(key.Item1, key.Item2);
        }

        private Ease EaseFor(DragAnimationChannel channel) => channel switch
        {
            DragAnimationChannel.Move => _moveEase,
            DragAnimationChannel.Placeholder => _placeholderEase,
            _ => _removalEase
        };
    }
}
