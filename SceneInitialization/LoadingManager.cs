using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Giana.UnityUtils
{
/// <summary>
/// Manages scene transitions with an optional loading overlay and initializes
/// custom Awake/Start systems on each scene. Implements a singleton pattern.
/// </summary>
public class LoadingManager : MonoBehaviour
{
    #region Events
    public static Action OnInitializationCompleted;
    #endregion

    #region Inspector Fields
    [Header("Overlay")]
    [Tooltip("Reference to the LoadingOverlayController used for fade-in / fade-out transitions.")]
    [SerializeField]
    private LoadingOverlayController _overlay;

    [Space(10)]
    [Header("Bypassed Scenes")]
    [Tooltip("Scenes listed here will skip the overlay transition entirely.")]
    [SerializeField]
    private List<string> _bypassedScenes = new List<string>();

    [Space(10)]
    [Header("Debug")]
    [Tooltip("When enabled, debug messages are logged (Editor only).")]
    [SerializeField]
    private bool showDebugLogs = true;

    [Tooltip("If true, pauses the Unity Editor automatically when an initialization error occurs.")]
    [SerializeField]
    private bool pauseEditorOnError = true;
    #endregion

    #region Properties & Private Fields
    /// <summary>
    /// Singleton instance of the LoadingManager.
    /// </summary>
    public static LoadingManager Instance { get; private set; }

    private CancellationTokenSource _lifeTimeCts;
    private bool _isTransitioning;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject); // Ensure only one exists
            return;
        }

        _lifeTimeCts = new CancellationTokenSource();
        _overlay.SetInstant(false);
    }

    private void Start()
    {
        if (_isTransitioning)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (IsBypassedScene(activeScene.name))
        {
            _ = RunInitializationAsync(activeScene, _lifeTimeCts.Token);
            return;
        }

        _ = InitializeCurrentSceneAsync();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_lifeTimeCts == null)
            return;

        _lifeTimeCts.Cancel();
        _lifeTimeCts.Dispose();
        _lifeTimeCts = null;
    }
    #endregion

    #region Scene Transition
    /// <summary>
    /// Transitions to the given scene asynchronously, optionally using the loading overlay.
    /// </summary>
    /// <param name="sceneName">Name of the target scene.</param>
    /// <param name="useOverlay">If true, the overlay fades in/out (unless the scene is bypassed).</param>
    /// <param name="ct">Cancellation token to abort the operation.</param>
    /// <returns>True if the transition was started; false if another transition is already in progress.</returns>
    public async Awaitable<bool> TransitionToSceneAsync(
        string sceneName,
        bool useOverlay = true,
        CancellationToken ct = default
    )
    {
        if (_isTransitioning)
        {
#if UNITY_EDITOR
            if (showDebugLogs)
                Debug.LogWarning("Transition requested while another one is happening. Ignoring");
#endif
            return false;
        }

        if (_lifeTimeCts != null && ct.CanBeCanceled)
        {
            using (
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    ct,
                    _lifeTimeCts.Token
                )
            )
            {
                await RunTransitionAsync(sceneName, useOverlay, linkedCts.Token);
            }
        }
        else
        {
            await RunTransitionAsync(sceneName, useOverlay, _lifeTimeCts?.Token ?? ct);
        }

        return true;
    }

    private async Task RunTransitionAsync(string sceneName, bool useOverlay, CancellationToken ct)
    {
        _isTransitioning = true;

        bool displayVisual = useOverlay && !IsBypassedScene(sceneName);

        try
        {
            if (_overlay != null && displayVisual)
            {
                await _overlay.FadeInAsync(ct);
            }

            Scene oldScene = SceneManager.GetActiveScene();

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            loadOp.allowSceneActivation = false;

            while (loadOp.progress < 0.9f)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }

            loadOp.allowSceneActivation = true;

            while (!loadOp.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }

            Scene newScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(newScene);

            if (oldScene.IsValid() && oldScene != newScene)
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(oldScene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        ct.ThrowIfCancellationRequested();
                        await Awaitable.NextFrameAsync(ct);
                    }
                }
            }

            await RunInitializationAsync(newScene, ct);

            await Awaitable.NextFrameAsync(ct);
            await Awaitable.NextFrameAsync(ct);

            if (_overlay != null && displayVisual)
            {
                await _overlay.FadeOutAsync(ct);
            }
        }
        catch (OperationCanceledException e)
        {
#if UNITY_EDITOR
            Debug.LogError("[LoadingManager] Transition cancelled. Exception: " + e);
#endif
            throw;
        }
        catch (Exception e)
        {
#if UNITY_EDITOR
            Debug.LogError("[LoadingManager] Initialization failed due to an exception: " + e);
            if (_overlay != null)
                _overlay.SetInstant(false);

            if (pauseEditorOnError)
            {
                UnityEditor.EditorApplication.isPaused = true;
            }
#endif
            throw;
        }
        finally
        {
            _isTransitioning = false;
            OnInitializationCompleted?.Invoke();
#if UNITY_EDITOR
            if (showDebugLogs)
            {
                Debug.Log("<color=#04ff00>[LoadingManager] Finished transition</color>");
            }
#endif
        }
    }

    private async Awaitable InitializeCurrentSceneAsync()
    {
        _isTransitioning = true;

        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            _overlay.SetInstant(true);
            await RunInitializationAsync(activeScene, _lifeTimeCts.Token);

            await Awaitable.NextFrameAsync(_lifeTimeCts.Token);
            await Awaitable.NextFrameAsync(_lifeTimeCts.Token);

            await _overlay.FadeOutAsync(_lifeTimeCts.Token);
        }
        catch (OperationCanceledException e)
        {
#if UNITY_EDITOR
            Debug.LogError("[LoadingManager] Scene initialization cancelled. Exception: " + e);
#endif
            throw;
        }
        catch (Exception e)
        {
#if UNITY_EDITOR
            Debug.LogError("[LoadingManager] Initialization failed due to an exception: " + e);
            if (_overlay != null)
                _overlay.SetInstant(false);

            if (pauseEditorOnError)
            {
                UnityEditor.EditorApplication.isPaused = true;
            }
#endif
            throw;
        }
        finally
        {
            _isTransitioning = false;
            OnInitializationCompleted?.Invoke();
#if UNITY_EDITOR
            if (showDebugLogs)
            {
                Debug.Log("<color=#04ff00>[LoadingManager] Finished initialization</color>");
            }
#endif
        }
    }

    /// <summary>
    /// Returns true if the given scene name is in the bypassed scenes list.
    /// </summary>
    private bool IsBypassedScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            return _bypassedScenes.Contains(sceneName);
        }
        return false;
    }
    #endregion

    #region Initialization System Execution
    /// <summary>
    /// Runs custom Awake/Start initialization systems on all root objects in the scene,
    /// respecting their priority ordering (descending).
    /// </summary>
    private async Task RunInitializationAsync(Scene scene, CancellationToken ct)
    {
        var awakeSystems = new List<IInitializeAwake>();
        var startSystems = new List<IInitializeStart>();

        GameObject[] rootObjs = scene.GetRootGameObjects();
        foreach (GameObject rootObj in rootObjs)
        {
            if (rootObj == null)
                continue;

            awakeSystems.AddRange(rootObj.GetComponentsInChildren<IInitializeAwake>(true));
            startSystems.AddRange(rootObj.GetComponentsInChildren<IInitializeStart>(true));
        }

        var sortedAwakeSystems = awakeSystems
            .Where(s => s != null)
            .OrderByDescending(s => s.AwakePriority)
            .ToList();

        var sortedStartSystems = startSystems
            .Where(s => s != null)
            .OrderByDescending(s => s.StartPriority)
            .ToList();

        for (int i = 0; i < sortedAwakeSystems.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await sortedAwakeSystems[i].InitializeAwake(ct);
#if UNITY_EDITOR
            if (showDebugLogs)
            {
                Debug.Log("<color=#04ff00>[LoadingManager] Initialized Awake of " + sortedAwakeSystems[i] + "</color>");
            }
#endif
        }

        for (int i = 0; i < sortedStartSystems.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await sortedStartSystems[i].InitializeStart(ct);
#if UNITY_EDITOR
            if (showDebugLogs)
            {
                Debug.Log("<color=#04ff00>[LoadingManager] Initialized Start of " + sortedStartSystems[i] + "</color>");
            }
#endif
        }
    }
    #endregion
}
}
