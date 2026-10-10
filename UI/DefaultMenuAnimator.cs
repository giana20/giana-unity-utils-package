using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Giana.UnityUtils
{
    public class DefaultMenuAnimator : MenuAnimatorBase
    {
        private Dictionary<Transform, Coroutine> _runningCoroutines = new Dictionary<Transform, Coroutine>();

        public override void AnimateSelect(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            RunScaleCoroutine(target, toScale, duration, unscaledTime);
        }

        public override void AnimateDeselect(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            RunScaleCoroutine(target, toScale, duration, unscaledTime);
        }

        public override void Kill(Transform target = null)
        {
            if (target != null)
            {
                if (_runningCoroutines.TryGetValue(target, out Coroutine coroutine))
                {
                    if (coroutine != null)
                    {
                        StopCoroutine(coroutine);
                    }
                    _runningCoroutines.Remove(target);
                }
            }
            else
            {
                foreach (var coroutine in _runningCoroutines.Values)
                {
                    if (coroutine != null)
                    {
                        StopCoroutine(coroutine);
                    }
                }
                _runningCoroutines.Clear();
            }
        }

        private void RunScaleCoroutine(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            Kill(target);
            if (gameObject.activeInHierarchy)
            {
                Coroutine coroutine = StartCoroutine(ScaleRoutine(target, toScale, duration, unscaledTime));
                _runningCoroutines[target] = coroutine;
            }
            else
            {
                if (target != null)
                    target.localScale = toScale;
            }
        }

        private IEnumerator ScaleRoutine(Transform target, Vector3 toScale, float duration, bool unscaledTime)
        {
            if (target == null) yield break;
            
            Vector3 initialScale = target.localScale;
            float time = 0f;

            while (time < duration)
            {
                if (target == null) yield break;
                
                time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                
                // Simple ease-out
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                
                target.localScale = Vector3.Lerp(initialScale, toScale, ease);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = toScale;
                _runningCoroutines.Remove(target);
            }
        }
    }
}
