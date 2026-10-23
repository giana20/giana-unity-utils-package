using System.Threading;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Abstract base class for a persistent, thread-safe MonoBehaviour singleton that
    /// auto-initializes through the <see cref="IInitializeAwake"/> system. Subclasses
    /// gain a global <see cref="Instance"/> accessor and automatic duplicate destruction.
    /// </summary>
    /// <typeparam name="T">The concrete subclass type.</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour, IInitializeAwake
        where T : MonoSingleton<T>
    {
        #region Events
        // No events or delegates are declared.
        #endregion

        #region Inspector Fields
        [field: Header("Singleton Settings")]
        [field: Tooltip("Priority for the Awake initialization step (higher executes first).")]
        [field: SerializeField]
        public int AwakePriority { get; set; }

        [Tooltip("Set this to true if this singleton should persist across scene and dont get destroyed when changing scene.")]
        [SerializeField]
        protected bool _persistAcrossScenes = true;
        #endregion

        #region Properties & Private Fields
        /// <summary>
        /// The current singleton instance. Null before first initialization.
        /// </summary>
        protected static T _instance;
        #endregion

        #region Unity Lifecycle
        // No Unity lifecycle methods are implemented.
        #endregion

        #region Singleton Initialization
        /// <summary>
        /// Thread-safe singleton accessor. Returns the current instance (may be null before initialization).
        /// </summary>
        public static T Instance => _instance;
#pragma warning disable CS1998
        /// <summary>
        /// Performs singleton assignment. If no instance exists, sets this as the instance and
        /// marks it to survive scene loads. If a different instance already exists, destroys this object.
        /// </summary>
        /// <param name="ct">Cancellation token (respected by the async operation, though no long-running work is performed).</param>
        public virtual async Awaitable InitializeAwake(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_instance == null)
            {
                _instance = this as T;
                if (_persistAcrossScenes)
                    DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject); // Ensure only one exists
            }
        }
#pragma warning restore CS1998
        #endregion
    }
}
