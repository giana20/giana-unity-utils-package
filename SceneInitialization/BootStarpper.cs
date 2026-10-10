using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Bootstraps the master LoadingManager system by instantiating a prefab from Resources
    /// before any scene loads, ensuring a persistent singleton is available.
    /// </summary>
    public class BootStrapper : MonoBehaviour
    {
        #region Events
        // No events declared.
        #endregion

        #region Inspector Fields
        // This class uses no serialized fields; initialization is fully automatic.
        #endregion

        #region Properties & Private Fields
        private static bool showDebugLogs = false;
        #endregion

        #region Unity Lifecycle
        // No lifecycle methods are required – all setup is handled via a static method.
        #endregion

        #region Runtime Initialization
        /// <summary>
        /// Executes before any scene is loaded. If no <see cref="LoadingManager"/> instance exists,
        /// it loads the master prefab from the "MasterLoadingManager" resource and marks it as
        /// DontDestroyOnLoad.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeMasterSystem()
        {
            if (FindAnyObjectByType<LoadingManager>() != null)
            {
                return;
            }

            GameObject loadingManagerPrefab = Resources.Load<GameObject>("MasterLoadingManager");

            if (loadingManagerPrefab == null)
            {
#if UNITY_EDITOR
                if (showDebugLogs)
                    Debug.Log("<color=#ff0000>Master loading manager not found in Resources folder</color>");
#endif
                return;
            }

            GameObject loadingManagerInstance = Instantiate(loadingManagerPrefab);
            DontDestroyOnLoad(loadingManagerInstance);
        }
        #endregion
    }
}
