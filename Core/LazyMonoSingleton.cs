using System.Threading;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Thread-safe lazy singleton base class for MonoBehaviours. The instance is created
    /// automatically when first accessed, and marked as <c>DontDestroyOnLoad</c>.
    /// </summary>
    /// <typeparam name="T">The concrete subclass type.</typeparam>
    public abstract class LazyMonoSingleton<T> : MonoBehaviour
        where T : LazyMonoSingleton<T>
    {
        #region Events
        // No events or delegates are declared.
        #endregion

        #region Inspector Fields
        [Header("Singleton Settings")]
        [Tooltip("Priority for initialization ordering (higher executes first).")]
        [Min(0)]
        [field: SerializeField]
        public int AwakePriority { get; private set; }

        [Space(10)]
        [Header("Debug")]
        [Tooltip("When enabled, debug messages are printed in the Editor.")]
        [SerializeField]
        protected bool showDebugLogs = true;
        #endregion

        #region Properties & Private Fields
        /// <summary>
        /// Thread-safe singleton accessor. Automatically creates the instance if it doesn't exist.
        /// Returns <c>null</c> if the application is quitting.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
#if UNITY_EDITOR
                    bool shouldLog = (_instance != null) ? _instance.showDebugLogs : true;
                    if (shouldLog)
                        Debug.LogWarning(
                            $"[MonoSingleton] Instance '{typeof(T)}' already destroyed on application quit. Returning null."
                        );
#endif
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // Search for existing instance in the scene
                        _instance = (T)FindAnyObjectByType(typeof(T));

                        // If no instance found, create a new GameObject
                        if (_instance == null)
                        {
                            GameObject singleton = new GameObject();
                            _instance = singleton.AddComponent<T>();
                            singleton.name = typeof(T).ToString() + " (Singleton)";
                            DontDestroyOnLoad(singleton);
                        }
                    }
                    return _instance;
                }
            }
        }

        protected static T _instance;
        protected static readonly object _lock = new object();
        protected static bool _isQuitting = false;
        #endregion

        #region Unity Lifecycle
        /// <summary>
        /// Ensures only one instance exists by destroying duplicates.
        /// </summary>
        protected void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject); // Ensure only one exists
            }
        }

        /// <summary>
        /// Flags the singleton as quitting to prevent access after destruction.
        /// </summary>
        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }
        #endregion
    }
}
