using DG.Tweening;
using UnityEngine;
using Giana.UnityUtils;

namespace Giana.UnityUtils.DoTween
{
    public class DOTweenMenuAnimator : MenuAnimatorBase
    {
        private Tween _scaleUpTween;
        private Tween _scaleDownTween;

        public override void AnimateSelect(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            if (target == null) return;
            target.DOKill();
            _scaleUpTween = target.DOScale(toScale, duration).SetUpdate(unscaledTime);
        }

        public override void AnimateDeselect(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            if (target == null) return;
            target.DOKill();
            _scaleDownTween = target.DOScale(toScale, duration).SetUpdate(unscaledTime);
        }

        public override void Kill(Transform target = null)
        {
            if (target != null)
            {
                target.DOKill();
            }
            else
            {
                _scaleUpTween?.Kill();
                _scaleDownTween?.Kill();
            }
        }
    }
}
