using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using Giana.UnityUtils;

namespace Giana.UnityUtils.FEEL
{
    public class FeelUITransition : MonoBehaviour, IUITransition
    {
        [SerializeField]
        private MMF_Player _enterFeedback;

        [SerializeField]
        private MMF_Player _exitFeedback;

        private bool _initialized;

        public void PlayEnterAnimation(float delay)
        {
            if (_enterFeedback == null) return;

            if (!_initialized)
            {
                _enterFeedback.Initialization();
                if (_exitFeedback != null) _exitFeedback.Initialization();
                _initialized = true;
            }

            _enterFeedback.InitialDelay = delay; // Apply stagger delay
            _enterFeedback.PlayFeedbacks();
        }

        public void PlayExitAnimation(Action onAnimationComplete)
        {
            if (_exitFeedback == null)
            {
                onAnimationComplete?.Invoke();
                return;
            }

            if (!_initialized)
            {
                if (_enterFeedback != null) _enterFeedback.Initialization();
                _exitFeedback.Initialization();
                _initialized = true;
            }

            // Avoid dangling listeners if played multiple times
            _exitFeedback.Events.OnComplete.RemoveAllListeners();
            
            _exitFeedback.Events.OnComplete.AddListener(() =>
            {
                _exitFeedback.Events.OnComplete.RemoveAllListeners(); // clean up
                onAnimationComplete?.Invoke();
            });

            _exitFeedback.PlayFeedbacks();
        }
    }
}