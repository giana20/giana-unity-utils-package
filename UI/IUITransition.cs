using System;

namespace Giana.UnityUtils
{
    public interface IUITransition
    {
        // Pass a delay index so you can stagger animations (e.g., cards appear one by one)
        void PlayEnterAnimation(float delay);

        // Pass an Action callback so the builder knows when it's safe to destroy the object
        void PlayExitAnimation(Action onAnimationComplete);
    }
}
