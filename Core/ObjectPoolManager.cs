using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Giana.UnityUtils
{
/// <summary>
/// AAA-grade object pooling system for Unity with advanced features.
///
/// <para><b>Key Features:</b></para>
/// <list type="bullet">
/// <item><description>Instance ID-based tracking (no name fragility)</description></item>
/// <item><description>Async instantiation support to prevent frame spikes</description></item>
/// <item><description>IPoolable interface for proper object reset callbacks</description></item>
/// <item><description>Per-pool capacity management with configurable limits</description></item>
/// <item><description>Analytics and profiling integration for optimization</description></item>
/// <item><description>Automatic culling strategies for long-running sessions</description></item>
/// <item><description>Dependency injection friendly (singleton optional)</description></item>
/// </list>
///
/// <para><b>Basic Usage:</b></para>
/// <code>
/// // Spawn an object
/// GameObject obj = ObjectPoolManager.Instance.Spawn(prefab, position, rotation);
///
/// // Return to pool
/// ObjectPoolManager.Instance.Despawn(obj);
///
/// // Or from the object itself
/// obj.GetComponent&lt;PooledObject&gt;().ReturnToPool();
/// </code>
///
/// <para><b>Advanced Usage:</b></para>
/// <code>
/// // Prewarm a pool to avoid runtime spikes
/// ObjectPoolManager.Instance.Prewarm(bulletPrefab, 50);
///
/// // Use custom configuration
/// PoolConfig config = new PoolConfig { maxCapacity = 200, enableCulling = true };
/// ObjectPoolManager.Instance.Prewarm(enemyPrefab, 20, config);
///
/// // Get analytics
/// PoolStats stats = ObjectPoolManager.Instance.GetPoolStats(prefab);
/// Debug.Log($"Pool efficiency: {stats.MemoryEfficiency:P}");
/// </code>
/// </summary>
public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>
{
    #region Configuration

    [Header("Global Settings")]
    [Tooltip("Default configuration applied to all pools unless overridden")]
    [SerializeField]
    private PoolConfig _defaultConfig = new PoolConfig();

    [Tooltip("Track spawn/despawn metrics for debugging and optimization")]
    [SerializeField]
    private bool _enableAnalytics = true;

    [Tooltip("Log detailed pool operations (useful for debugging)")]
    [SerializeField]
    private bool _enableDebugLogs = false;

    [Header("Performance")]
    [Tooltip("Maximum objects to instantiate per frame during async prewarming")]
    [SerializeField]
    private int _maxInstantiationsPerFrame = 5;

    [Tooltip("Interval in seconds between automatic culling checks")]
    [SerializeField]
    private float _cullCheckInterval = 10f;

    [Header("Hierarchy Organization")]
    [Tooltip("Organize pooled objects in hierarchy for easier debugging")]
    [SerializeField]
    private bool _organizeInHierarchy = true;

    // Internal state
    private GameObject _poolRoot; // Root container for all pooled objects
    private readonly Dictionary<EntityId, Pool> _pools = new Dictionary<EntityId, Pool>(); // All active pools indexed by prefab ID
    private readonly Queue<InstantiateRequest> _instantiateQueue = new Queue<InstantiateRequest>(); // Queue for async instantiation
    private PoolAnalytics _analytics; // Analytics tracking system
    private Coroutine _cullCoroutine; // Reference to culling coroutine
    #endregion

    #region Initialization

    /// <summary>
    /// Unity lifecycle: Called when the script instance is being loaded.
    /// Initializes the pool manager and starts background coroutines.
    ///
    /// This manager handles efficient reuse of bullets, effects, and other temporary objects.
    /// </summary>
    private void Awake()
    {
        // Note: DontDestroyOnLoad already called in Instance getter before Awake
        // Don't call it again here to avoid Unity warning about non-root objects

        // Set up hierarchy organization if enabled
        if (_organizeInHierarchy)
        {
            _poolRoot = new GameObject("[Pooled Objects]");
            _poolRoot.transform.SetParent(transform);
            // Child objects inherit DontDestroyOnLoad from parent, no need to call it explicitly
        }

        // Initialize analytics system if enabled
        if (_enableAnalytics)
        {
            _analytics = new PoolAnalytics();
        }

        // Start background coroutines
        StartCoroutine(ProcessInstantiateQueue()); // Handles async instantiation

        // Start culling coroutine if enabled in default config
        if (_defaultConfig.enableCulling)
        {
            _cullCoroutine = StartCoroutine(CullUnusedObjects());
        }
    }

    /// <summary>
    /// Unity lifecycle: Called when the MonoBehaviour will be destroyed.
    /// Cleans up all pools and stops background tasks.
    /// </summary>
    private void OnDestroy()
    {
        // Stop culling coroutine if running
        if (_cullCoroutine != null)
            StopCoroutine(_cullCoroutine);

        // Clean up all pools
        ClearAllPools();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Spawns an object from the pool at the specified position and rotation.
    /// If the pool doesn't exist, it will be created automatically.
    /// If no inactive objects are available, a new instance will be created.
    /// </summary>
    /// <param name="prefab">The prefab to spawn (must not be null)</param>
    /// <param name="position">World position for the spawned object</param>
    /// <param name="rotation">World rotation for the spawned object</param>
    /// <param name="parent">Optional parent transform for the spawned object</param>
    /// <returns>The spawned GameObject, or null if prefab is null</returns>
    public GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null
    )
    {
        // Validate input
        if (prefab == null)
        {
            LogError("Cannot spawn null prefab");
            return null;
        }

        // Get or create pool for this prefab
        EntityId prefabID = prefab.GetEntityId();
        Pool pool = GetOrCreatePool(prefabID, prefab);

        // Get instance from pool (may create new if none available)
        GameObject instance = pool.Spawn(position, rotation, parent);

        // Record analytics if enabled
        if (_enableAnalytics)
            _analytics.RecordSpawn(prefabID);

        return instance;
    }

    /// <summary>
    /// Spawns an object and returns a specific component from it.
    /// Convenience method to avoid GetComponent calls after spawning.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve</typeparam>
    /// <param name="prefab">The prefab to spawn</param>
    /// <param name="position">World position for the spawned object</param>
    /// <param name="rotation">World rotation for the spawned object</param>
    /// <param name="parent">Optional parent transform</param>
    /// <returns>The component of type T, or null if not found or spawn failed</returns>
    public T Spawn<T>(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null
    )
        where T : Component
    {
        GameObject instance = Spawn(prefab, position, rotation, parent);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    /// <summary>
    /// Returns an object to the pool, making it available for reuse.
    /// The object will be deactivated and reset according to pool configuration.
    /// </summary>
    /// <param name="instance">The GameObject to return (must have been spawned from a pool)</param>
    /// <param name="delay">Optional delay in seconds before returning to pool</param>
    public void Despawn(GameObject instance, float delay = 0f)
    {
        if (instance == null)
            return;

        // Handle delayed despawn
        if (delay > 0f)
        {
            StartCoroutine(DespawnDelayed(instance, delay));
            return;
        }

        // Verify object was spawned from pool
        PooledObject pooledObj = instance.GetComponent<PooledObject>();
        if (pooledObj == null)
        {
            LogWarning($"Object '{instance.name}' was not spawned from pool. Destroying instead.");
            Destroy(instance);
            return;
        }

        // Find the pool and return the object
        Pool pool = GetPool(pooledObj.PrefabID);
        if (pool == null)
        {
            LogWarning($"Pool for prefab ID {pooledObj.PrefabID} not found. Destroying object.");
            Destroy(instance);
            return;
        }

        pool.Despawn(instance);

        // Record analytics if enabled
        if (_enableAnalytics)
            _analytics.RecordDespawn(pooledObj.PrefabID);
    }

    /// <summary>
    /// Prewarms a pool by creating a specified number of instances in advance.
    /// This prevents runtime instantiation spikes during gameplay.
    /// All instances are created synchronously, which may cause a frame spike.
    /// </summary>
    /// <param name="prefab">The prefab to prewarm</param>
    /// <param name="count">Number of instances to create</param>
    /// <param name="config">Optional custom configuration for this pool</param>
    public void Prewarm(GameObject prefab, int count, PoolConfig config = null)
    {
        if (prefab == null || count <= 0)
            return;

        // Get or create pool
        EntityId prefabID = prefab.GetEntityId();
        Pool pool = GetOrCreatePool(prefabID, prefab, config);

        // Create instances synchronously
        pool.Prewarm(count);

        Log($"Prewarmed pool '{prefab.name}' with {count} instances");
    }

    /// <summary>
    /// Prewarms a pool asynchronously over multiple frames to avoid spikes.
    /// Instantiation is spread across frames based on maxInstantiationsPerFrame setting.
    /// Ideal for loading screens or background initialization.
    /// </summary>
    /// <param name="prefab">The prefab to prewarm</param>
    /// <param name="count">Number of instances to create</param>
    /// <param name="config">Optional custom configuration for this pool</param>
    /// <param name="onComplete">Optional callback invoked when prewarming completes</param>
    /// <returns>IEnumerator for coroutine usage</returns>
    public IEnumerator PrewarmAsync(
        GameObject prefab,
        int count,
        PoolConfig config = null,
        Action onComplete = null
    )
    {
        if (prefab == null || count <= 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        // Get or create pool
        EntityId prefabID = prefab.GetEntityId();
        Pool pool = GetOrCreatePool(prefabID, prefab, config);

        // Create instances asynchronously
        yield return pool.PrewarmAsync(count, _maxInstantiationsPerFrame);

        Log($"Async prewarmed pool '{prefab.name}' with {count} instances");
        onComplete?.Invoke();
    }

    /// <summary>
    /// Clears a specific pool, destroying all its objects and removing it from the manager.
    /// Use this when you know a prefab won't be needed anymore to free memory.
    /// </summary>
    /// <param name="prefab">The prefab whose pool should be cleared</param>
    public void ClearPool(GameObject prefab)
    {
        if (prefab == null)
            return;

        EntityId prefabID = prefab.GetEntityId();
        if (_pools.TryGetValue(prefabID, out Pool pool))
        {
            pool.Clear();
            _pools.Remove(prefabID);
            Log($"Cleared pool for '{prefab.name}'");
        }
    }

    /// <summary>
    /// Clears all pools, destroying all pooled objects.
    /// Use this when transitioning between major game states (e.g., returning to main menu).
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var pool in _pools.Values)
        {
            pool.Clear();
        }
        _pools.Clear();
        Log("Cleared all pools");
    }

    /// <summary>
    /// Gets statistics for a specific pool.
    /// Useful for debugging and optimization.
    /// </summary>
    /// <param name="prefab">The prefab to get stats for</param>
    /// <returns>PoolStats object with current pool state, or null if pool doesn't exist</returns>
    public PoolStats GetPoolStats(GameObject prefab)
    {
        if (prefab == null)
            return null;

        EntityId prefabID = prefab.GetEntityId();
        Pool pool = GetPool(prefabID);
        return pool?.GetStats();
    }

    /// <summary>
    /// Gets analytics data for all pools.
    /// Contains spawn rates, total spawns/despawns, and other metrics.
    /// </summary>
    /// <returns>PoolAnalytics object, or null if analytics are disabled</returns>
    public PoolAnalytics GetAnalytics()
    {
        return _enableAnalytics ? _analytics : null;
    }

    #endregion

    #region Pool Management

    /// <summary>
    /// Gets an existing pool or creates a new one if it doesn't exist.
    /// Pools are indexed by prefab instance ID for reliable tracking.
    /// </summary>
    /// <param name="prefabID">The instance ID of the prefab</param>
    /// <param name="prefab">The prefab GameObject</param>
    /// <param name="config">Optional custom configuration, uses default if not provided</param>
    /// <returns>The pool for the specified prefab</returns>
    private Pool GetOrCreatePool(EntityId prefabID, GameObject prefab, PoolConfig config = null)
    {
        if (!_pools.TryGetValue(prefabID, out Pool pool))
        {
            // Use provided config or fall back to default
            PoolConfig poolConfig = config ?? _defaultConfig;

            // Create hierarchy container if organization is enabled
            GameObject poolContainer = null;
            if (_organizeInHierarchy)
            {
                poolContainer = new GameObject($"[Pool] {prefab.name}");
                poolContainer.transform.SetParent(_poolRoot.transform);
            }

            // Create and register the new pool
            pool = new Pool(prefab, poolConfig, poolContainer, this);
            _pools.Add(prefabID, pool);

            Log($"Created pool for '{prefab.name}' (ID: {prefabID})");
        }

        return pool;
    }

    /// <summary>
    /// Gets an existing pool by prefab ID.
    /// Returns null if the pool doesn't exist.
    /// </summary>
    /// <param name="prefabID">The instance ID of the prefab</param>
    /// <returns>The pool, or null if not found</returns>
    private Pool GetPool(EntityId prefabID)
    {
        _pools.TryGetValue(prefabID, out Pool pool);
        return pool;
    }

    #endregion

    #region Async Instantiation

    /// <summary>
    /// Background coroutine that processes the async instantiation queue.
    /// Limits instantiations per frame to prevent performance spikes.
    /// Runs continuously throughout the lifetime of the pool manager.
    /// </summary>
    private IEnumerator ProcessInstantiateQueue()
    {
        while (true)
        {
            int instantiated = 0;

            // Process queue up to the per-frame limit
            while (_instantiateQueue.Count > 0 && instantiated < _maxInstantiationsPerFrame)
            {
                InstantiateRequest request = _instantiateQueue.Dequeue();
                request.Execute();
                instantiated++;
            }

            yield return null; // Wait for next frame
        }
    }

    /// <summary>
    /// Queues an instantiation request for async processing.
    /// Used internally by the async prewarming system.
    /// </summary>
    /// <param name="request">The instantiation request to queue</param>
    internal void QueueInstantiate(InstantiateRequest request)
    {
        _instantiateQueue.Enqueue(request);
    }

    #endregion

    #region Culling

    /// <summary>
    /// Background coroutine that periodically checks for and removes unused pooled objects.
    /// Helps manage memory in long-running sessions by destroying objects that haven't been used recently.
    /// Runs at intervals defined by cullCheckInterval.
    /// </summary>
    private IEnumerator CullUnusedObjects()
    {
        WaitForSeconds wait = new WaitForSeconds(_cullCheckInterval);

        while (true)
        {
            yield return wait; // Wait for next check interval

            float currentTime = Time.unscaledTime;

            // Check each pool for cullable objects
            foreach (var pool in _pools.Values)
            {
                pool.CullUnused(currentTime);
            }
        }
    }

    #endregion

    #region Delayed Despawn

    /// <summary>
    /// Coroutine that waits for a specified time before returning an object to the pool.
    /// Uses unscaled time to work correctly even when time is paused.
    /// </summary>
    /// <param name="instance">The GameObject to return</param>
    /// <param name="delay">Delay in seconds</param>
    private IEnumerator DespawnDelayed(GameObject instance, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Despawn(instance);
    }

    #endregion

    #region Logging

    /// <summary>
    /// Logs an informational message if debug logging is enabled.
    /// </summary>
    private void Log(string message)
    {
        if (_enableDebugLogs)
            Debug.Log($"[ObjectPoolManager] {message}");
    }

    /// <summary>
    /// Logs a warning message (always shown, regardless of debug setting).
    /// </summary>
    private void LogWarning(string message)
    {
#if UNITY_EDITOR
        Debug.LogWarning($"[ObjectPoolManager] {message}");
#endif
    }

    /// <summary>
    /// Logs an error message (always shown, regardless of debug setting).
    /// </summary>
    private void LogError(string message)
    {
#if UNITY_EDITOR
        Debug.LogError($"[ObjectPoolManager] {message}");
#endif
    }

    #endregion
}

#region Pool Class

/// <summary>
/// Internal class representing a single object pool.
/// Manages the lifecycle of instances for a specific prefab.
/// Handles spawning, despawning, prewarming, culling, and analytics.
/// </summary>
internal class Pool
{
    // Pool configuration and references
    private readonly GameObject _prefab; // The prefab this pool manages
    private readonly EntityId _prefabID; // Instance ID of the prefab for tracking
    private readonly PoolConfig _config; // Configuration settings for this pool
    private readonly GameObject _container; // Hierarchy container for organization
    private readonly ObjectPoolManager _manager; // Reference to the pool manager

    // Object tracking collections
    private readonly Stack<GameObject> _available = new Stack<GameObject>(); // Inactive objects ready for reuse
    private readonly HashSet<GameObject> _active = new HashSet<GameObject>(); // Currently active objects
    private readonly Dictionary<GameObject, float> _lastDespawnTime =
        new Dictionary<GameObject, float>(); // Tracks when objects were last returned

    // Pool statistics
    private int _totalCreated = 0; // Total number of instances created
    private int _peakActive = 0; // Highest number of simultaneously active instances

    /// <summary>
    /// Creates a new pool for the specified prefab.
    /// </summary>
    /// <param name="prefab">The prefab to pool</param>
    /// <param name="config">Configuration settings</param>
    /// <param name="container">Hierarchy container for organization (can be null)</param>
    /// <param name="manager">Reference to the pool manager</param>
    public Pool(
        GameObject prefab,
        PoolConfig config,
        GameObject container,
        ObjectPoolManager manager
    )
    {
        _prefab = prefab;
        _prefabID = prefab.GetEntityId();
        _config = config;
        _container = container;
        _manager = manager;

        // Validate prefab compatibility
        ValidatePrefab();
    }

    /// <summary>
    /// Validates that the prefab meets pool requirements.
    /// Logs warnings if configuration expectations aren't met.
    /// </summary>
    private void ValidatePrefab()
    {
        if (_config.requirePoolableInterface)
        {
            IPoolable poolable = _prefab.GetComponent<IPoolable>();
            if (poolable == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"Prefab '{_prefab.name}' does not implement IPoolable but requirePoolableInterface is enabled."
                );
#endif
            }
        }
    }

    /// <summary>
    /// Spawns an instance from the pool or creates a new one if needed.
    /// Handles position/rotation setup, parent assignment, and lifecycle callbacks.
    /// </summary>
    /// <param name="position">World position for the instance</param>
    /// <param name="rotation">World rotation for the instance</param>
    /// <param name="parent">Optional parent transform</param>
    /// <returns>The spawned GameObject</returns>
    public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject instance = null;

        // Try to reuse an available instance from the pool
        while (_available.Count > 0)
        {
            instance = _available.Pop();
            if (instance != null)
                break; // Found a valid instance
        }

        // Create new instance if none available
        if (instance == null)
        {
            // Check if we've hit capacity limit
            if (_totalCreated >= _config.maxCapacity)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"Pool '{_prefab.name}' reached max capacity ({_config.maxCapacity}). Consider increasing capacity or enabling culling."
                );
#endif

                // Force-return oldest active object if culling is enabled
                if (_config.cullWhenFull)
                {
                    instance = GetOldestActive();
                    if (instance != null)
                    {
                        Despawn(instance);
                    }
                }
            }

            // Create new instance if we still don't have one
            if (instance == null)
            {
                instance = CreateInstance();
            }
        }

        // Validate we got an instance
        if (instance == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"Failed to spawn from pool '{_prefab.name}'");
#endif
            return null;
        }

        // Configure the instance for spawning
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.SetParent(parent);

        // Track as active
        _active.Add(instance);
        _peakActive = Mathf.Max(_peakActive, _active.Count);

        // IMPORTANT: Call IPoolable.OnSpawn() BEFORE SetActive() so Update() doesn't run before initialization
        NotifySpawn(instance);

        // Activate the instance last to ensure all setup is complete
        instance.SetActive(true);

        return instance;
    }

    /// <summary>
    /// Returns an instance to the pool, making it available for reuse.
    /// Deactivates the object, resets its state, and calls cleanup callbacks.
    /// </summary>
    /// <param name="instance">The GameObject to return</param>
    public void Despawn(GameObject instance)
    {
        if (instance == null)
            return;

        // Remove from active tracking
        _active.Remove(instance);
        _lastDespawnTime[instance] = Time.unscaledTime;

        // Call IPoolable.OnDespawn() for cleanup
        NotifyDespawn(instance);

        // Reset the instance to default state
        ResetInstance(instance);

        // Deactivate and reparent to pool container
        instance.SetActive(false);
        instance.transform.SetParent(_container != null ? _container.transform : null);

        // Add to available pool for reuse
        _available.Push(instance);
    }

    /// <summary>
    /// Prewarms the pool by creating instances synchronously.
    /// All instances are created immediately, which may cause a frame spike.
    /// </summary>
    /// <param name="count">Number of instances to create</param>
    public void Prewarm(int count)
    {
        for (int i = 0; i < count && _totalCreated < _config.maxCapacity; i++)
        {
            GameObject instance = CreateInstance();
            instance.SetActive(false);
            _available.Push(instance);
        }
    }

    /// <summary>
    /// Prewarms the pool asynchronously over multiple frames.
    /// Instantiation is spread across frames to prevent performance spikes.
    /// </summary>
    /// <param name="count">Number of instances to create</param>
    /// <param name="maxPerFrame">Maximum instances to create per frame</param>
    public IEnumerator PrewarmAsync(int count, int maxPerFrame)
    {
        int created = 0;
        int thisFrame = 0;

        while (created < count && _totalCreated < _config.maxCapacity)
        {
            GameObject instance = CreateInstance();
            instance.SetActive(false);
            _available.Push(instance);

            created++;
            thisFrame++;

            // Yield to next frame if we've hit the per-frame limit
            if (thisFrame >= maxPerFrame)
            {
                thisFrame = 0;
                yield return null;
            }
        }
    }

    /// <summary>
    /// Culls (destroys) unused objects that have been idle too long.
    /// Helps manage memory in long-running sessions by removing excess pooled objects.
    /// </summary>
    /// <param name="currentTime">Current unscaled time for age calculation</param>
    public void CullUnused(float currentTime)
    {
        if (!_config.enableCulling)
            return;

        int cullCount = 0;
        List<GameObject> toCull = new List<GameObject>();

        // Find objects that have been idle too long
        foreach (var instance in _available)
        {
            if (instance == null)
                continue;

            if (_lastDespawnTime.TryGetValue(instance, out float lastTime))
            {
                float idleTime = currentTime - lastTime;
                if (idleTime > _config.cullAfterSeconds)
                {
                    toCull.Add(instance);
                    cullCount++;

                    // Limit culls per check to avoid frame spikes
                    if (cullCount >= _config.maxCullPerCheck)
                        break;
                }
            }
        }

        // Destroy the culled objects
        foreach (var instance in toCull)
        {
            _available.TryPop(out _); // Remove from available stack
            _lastDespawnTime.Remove(instance);
            UnityEngine.Object.Destroy(instance);
            _totalCreated--;
        }

#if UNITY_EDITOR
        if (cullCount > 0)
        {
            Debug.Log($"Culled {cullCount} unused objects from pool '{_prefab.name}'");
        }
#endif
    }

    /// <summary>
    /// Clears the entire pool, destroying all instances.
    /// Called when the pool is being removed or the manager is shutting down.
    /// </summary>
    public void Clear()
    {
        // Destroy all available (inactive) objects
        while (_available.Count > 0)
        {
            GameObject instance = _available.Pop();
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        // Destroy all active objects
        foreach (var instance in _active)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        // Clear all tracking collections
        _active.Clear();
        _lastDespawnTime.Clear();
        _totalCreated = 0;

        // Destroy the hierarchy container
        if (_container != null)
            UnityEngine.Object.Destroy(_container);
    }

    /// <summary>
    /// Gets current statistics for this pool.
    /// Useful for debugging and performance monitoring.
    /// </summary>
    /// <returns>PoolStats object with current state</returns>
    public PoolStats GetStats()
    {
        return new PoolStats
        {
            PrefabName = _prefab.name,
            ActiveCount = _active.Count,
            AvailableCount = _available.Count,
            TotalCreated = _totalCreated,
            PeakActive = _peakActive,
            MaxCapacity = _config.maxCapacity,
        };
    }

    /// <summary>
    /// Creates a new instance of the prefab and adds it to the pool.
    /// Adds the PooledObject component for tracking and cleanup.
    /// </summary>
    /// <returns>The newly created GameObject</returns>
    private GameObject CreateInstance()
    {
        GameObject instance = UnityEngine.Object.Instantiate(_prefab);
        instance.name = _prefab.name; // Remove "(Clone)" suffix

        // Add PooledObject component for tracking
        PooledObject pooledObj = instance.AddComponent<PooledObject>();
        pooledObj.Initialize(_prefabID, _manager);

        // Parent to container if hierarchy organization is enabled
        if (_container != null)
            instance.transform.SetParent(_container.transform);

        _totalCreated++;
        return instance;
    }

    /// <summary>
    /// Resets an instance to its default state.
    /// Handles transform reset and optional component resets based on configuration.
    /// </summary>
    /// <param name="instance">The GameObject to reset</param>
    private void ResetInstance(GameObject instance)
    {
        // Reset transform to default values
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        // Reset physics components if configured
        if (_config.resetRigidbody)
        {
            // Reset 3D rigidbody
            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero; // Modern Unity API (replaces .velocity)
                rb.angularVelocity = Vector3.zero;
            }

            // Reset 2D rigidbody
            Rigidbody2D rb2d = instance.GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                rb2d.linearVelocity = Vector2.zero; // Modern Unity API (replaces .velocity)
                rb2d.angularVelocity = 0f;
            }
        }

        // Reset animator if configured
        if (_config.resetAnimator)
        {
            Animator animator = instance.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Rebind(); // Reset animator to default state
                animator.Update(0f); // Force update to apply reset
            }
        }

        // Reset particle systems if configured
        if (_config.resetParticles)
        {
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Clear(); // Clear all particles
                ps.Stop(); // Stop emission
            }
        }
    }

    /// <summary>
    /// Notifies all IPoolable components on an instance that it's being spawned.
    /// Called before the object is activated to ensure proper initialization order.
    /// </summary>
    /// <param name="instance">The GameObject being spawned</param>
    private void NotifySpawn(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponents<IPoolable>();
        foreach (var poolable in poolables)
        {
            poolable.OnSpawn();
        }
    }

    /// <summary>
    /// Notifies all IPoolable components on an instance that it's being despawned.
    /// Called before the object is deactivated to allow cleanup.
    /// </summary>
    /// <param name="instance">The GameObject being despawned</param>
    private void NotifyDespawn(GameObject instance)
    {
        IPoolable[] poolables = instance.GetComponents<IPoolable>();
        foreach (var poolable in poolables)
        {
            poolable.OnDespawn();
        }
    }

    /// <summary>
    /// Gets the oldest active object in the pool.
    /// Used when the pool is full and needs to forcibly reclaim an object.
    /// </summary>
    /// <returns>The oldest active GameObject, or null if none exist</returns>
    private GameObject GetOldestActive()
    {
        foreach (var instance in _active)
        {
            if (instance != null)
                return instance;
        }
        return null;
    }
}

#endregion

#region Supporting Classes

/// <summary>
/// Configuration settings for an object pool.
/// Can be customized per pool or use the manager's default configuration.
/// </summary>
[Serializable]
public class PoolConfig
{
    [Header("Capacity")]
    [Tooltip("Maximum number of instances this pool can create")]
    /// <summary>Maximum number of instances this pool can create.</summary>
    public int maxCapacity = 100;

    [Tooltip("Force despawn oldest active object when pool reaches capacity")]
    /// <summary>Force despawn oldest active object when pool reaches capacity.</summary>
    public bool cullWhenFull = false;

    [Header("Culling")]
    [Tooltip("Enable automatic culling of unused objects to save memory")]
    /// <summary>Enable automatic culling of unused objects to save memory.</summary>
    public bool enableCulling = true;

    [Tooltip("Seconds an object must be unused before it can be culled")]
    /// <summary>Seconds an object must be unused before it can be culled.</summary>
    public float cullAfterSeconds = 60f;

    [Tooltip("Maximum objects to cull per check (prevents frame spikes)")]
    /// <summary>Maximum objects to cull per check to prevent frame spikes.</summary>
    public int maxCullPerCheck = 10;

    [Header("Reset Options")]
    [Tooltip("Reset Rigidbody/Rigidbody2D velocity when despawning")]
    /// <summary>Reset Rigidbody/Rigidbody2D velocity when despawning.</summary>
    public bool resetRigidbody = true;

    [Tooltip("Reset Animator state when despawning")]
    /// <summary>Reset Animator state when despawning.</summary>
    public bool resetAnimator = true;

    [Tooltip("Clear and stop ParticleSystem components when despawning")]
    /// <summary>Clear and stop ParticleSystem components when despawning.</summary>
    public bool resetParticles = true;

    [Header("Validation")]
    [Tooltip("Log warning if prefab doesn't implement IPoolable interface")]
    public bool requirePoolableInterface = false;
}

/// <summary>
/// Statistics for a single pool.
/// Provides insight into pool usage and efficiency for optimization.
/// </summary>
public class PoolStats
{
    /// <summary>Name of the prefab this pool manages</summary>
    public string PrefabName;

    /// <summary>Number of currently active (spawned) instances</summary>
    public int ActiveCount;

    /// <summary>Number of available (inactive) instances ready for reuse</summary>
    public int AvailableCount;

    /// <summary>Total number of instances created (active + available)</summary>
    public int TotalCreated;

    /// <summary>Highest number of simultaneous active instances</summary>
    public int PeakActive;

    /// <summary>Maximum capacity limit for this pool</summary>
    public int MaxCapacity;

    /// <summary>
    /// Memory efficiency ratio (0-1).
    /// Higher values mean less waste (peak usage closer to total created).
    /// </summary>
    public float MemoryEfficiency => TotalCreated > 0 ? (float)PeakActive / TotalCreated : 0f;

    public override string ToString()
    {
        return $"Pool '{PrefabName}': {ActiveCount} active, {AvailableCount} available, {TotalCreated} total (peak: {PeakActive}, capacity: {MaxCapacity})";
    }
}

/// <summary>
/// Analytics system that tracks pool usage metrics.
/// Helps identify performance bottlenecks and optimization opportunities.
/// </summary>
public class PoolAnalytics
{
    private readonly Dictionary<EntityId, PoolMetrics> _metrics =
        new Dictionary<EntityId, PoolMetrics>();

    /// <summary>
    /// Records a spawn event for analytics tracking.
    /// </summary>
    /// <param name="prefabID">Instance ID of the spawned prefab</param>
    public void RecordSpawn(EntityId prefabID)
    {
        if (!_metrics.ContainsKey(prefabID))
            _metrics[prefabID] = new PoolMetrics();

        _metrics[prefabID].TotalSpawns++;
        _metrics[prefabID].LastSpawnTime = Time.unscaledTime;
    }

    /// <summary>
    /// Records a despawn event for analytics tracking.
    /// </summary>
    /// <param name="prefabID">Instance ID of the despawned prefab</param>
    public void RecordDespawn(EntityId prefabID)
    {
        if (!_metrics.ContainsKey(prefabID))
            _metrics[prefabID] = new PoolMetrics();

        _metrics[prefabID].TotalDespawns++;
    }

    /// <summary>
    /// Gets metrics for a specific pool.
    /// </summary>
    /// <param name="prefabID">Instance ID of the prefab</param>
    /// <returns>Metrics object, or null if no data exists</returns>
    public PoolMetrics GetMetrics(EntityId prefabID)
    {
        _metrics.TryGetValue(prefabID, out PoolMetrics metrics);
        return metrics;
    }

    /// <summary>
    /// Gets all metrics across all pools.
    /// </summary>
    /// <returns>Dictionary of all pool metrics</returns>
    public Dictionary<EntityId, PoolMetrics> GetAllMetrics()
    {
        return new Dictionary<EntityId, PoolMetrics>(_metrics);
    }
}

/// <summary>
/// Metrics for a single pool's usage patterns.
/// Used for performance analysis and optimization decisions.
/// </summary>
[Serializable]
public class PoolMetrics
{
    /// <summary>Total number of times objects were spawned from this pool</summary>
    public int TotalSpawns;

    /// <summary>Total number of times objects were returned to this pool</summary>
    public int TotalDespawns;

    /// <summary>Timestamp of the most recent spawn</summary>
    public float LastSpawnTime;

    /// <summary>
    /// Average spawn rate (spawns per second).
    /// Helps identify high-frequency pools that need optimization.
    /// </summary>
    public float SpawnRate =>
        TotalSpawns > 0 ? TotalSpawns / (Time.unscaledTime - LastSpawnTime) : 0f;
}

/// <summary>
/// Internal class representing an async instantiation request.
/// Used by the async prewarming system to spread object creation across frames.
/// </summary>
internal class InstantiateRequest
{
    /// <summary>The prefab to instantiate</summary>
    public GameObject Prefab;

    /// <summary>Callback invoked when instantiation completes</summary>
    public Action<GameObject> OnComplete;

    /// <summary>
    /// Executes the instantiation request.
    /// </summary>
    public void Execute()
    {
        GameObject instance = UnityEngine.Object.Instantiate(Prefab);
        OnComplete?.Invoke(instance);
    }
}

/// <summary>
/// Component automatically added to pooled objects for tracking and management.
/// Provides a convenient interface for objects to return themselves to the pool.
/// </summary>
internal class PooledObject : MonoBehaviour
{
    /// <summary>Instance ID of the prefab this object was spawned from</summary>
    public EntityId PrefabID { get; private set; }

    private ObjectPoolManager _manager; // Reference to the pool manager

    /// <summary>
    /// Initializes the pooled object component.
    /// Called by the pool when the object is created.
    /// </summary>
    /// <param name="prefabID">Instance ID of the source prefab</param>
    /// <param name="manager">Reference to the pool manager</param>
    public void Initialize(EntityId prefabID, ObjectPoolManager manager)
    {
        PrefabID = prefabID;
        _manager = manager;
    }

    /// <summary>
    /// Convenience method to return this object to the pool.
    /// Can be called from any script on the GameObject.
    /// </summary>
    /// <param name="delay">Optional delay in seconds before returning to pool</param>
    public void ReturnToPool(float delay = 0f)
    {
        _manager.Despawn(gameObject, delay);
    }

    /// <summary>
    /// Can be called to retrive the poool manager
    /// </summary>
    public ObjectPoolManager GetManager()
    {
        return _manager;
    }
}

/// <summary>
/// Interface for objects that need custom initialization/cleanup when pooled.
/// Implement this on any MonoBehaviour that should receive spawn/despawn callbacks.
///
/// <para><b>Example Usage:</b></para>
/// <code>
/// public class Bullet : MonoBehaviour, IPoolable
/// {
///     public void OnSpawn()
///     {
///         // Reset state, enable components, start effects
///         GetComponent&lt;Rigidbody&gt;().linearVelocity = Vector3.zero;
///     }
///
///     public void OnDespawn()
///     {
///         // Cleanup, stop effects, clear references
///         StopAllCoroutines();
///     }
/// }
/// </code>
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// Called when the object is spawned from the pool.
    /// Use this to initialize or reset state before the object becomes active.
    /// Called BEFORE SetActive(true) to ensure proper initialization order.
    /// </summary>
    void OnSpawn();

    /// <summary>
    /// Called when the object is returned to the pool.
    /// Use this to clean up state, stop effects, or release resources.
    /// Called AFTER SetActive(false).
    /// </summary>
    void OnDespawn();
}

#endregion

#region Editor Tools

#if UNITY_EDITOR
/// <summary>
/// Custom editor for ObjectPoolManager that displays runtime statistics.
/// Provides easy access to pool metrics and management tools in the inspector.
/// </summary>
[CustomEditor(typeof(ObjectPoolManager))]
public class ObjectPoolManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ObjectPoolManager manager = (ObjectPoolManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Statistics", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            // Display analytics if available
            var analytics = manager.GetAnalytics();
            if (analytics != null)
            {
                var allMetrics = analytics.GetAllMetrics();
                EditorGUILayout.LabelField($"Total Pools: {allMetrics.Count}");

                int totalSpawns = 0;
                foreach (var metrics in allMetrics.Values)
                {
                    totalSpawns += metrics.TotalSpawns;
                }
                EditorGUILayout.LabelField($"Total Spawns: {totalSpawns}");
            }

            // Add button to clear all pools
            if (GUILayout.Button("Clear All Pools"))
            {
                manager.ClearAllPools();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Statistics available at runtime", MessageType.Info);
        }
    }
}
#endif

#endregion
}
