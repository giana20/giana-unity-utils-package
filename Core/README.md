# Core

Foundational patterns: two singleton base classes and an instance-ID based object pool.

Files: `MonoSingleton.cs`, `LazyMonoSingleton.cs`, `ObjectPoolManager.cs` (also contains `PoolConfig`, `PoolStats`, `PoolAnalytics`, `PoolMetrics`, `IPoolable`).

---

## Choosing a singleton

| | `MonoSingleton<T>` | `LazyMonoSingleton<T>` |
|---|---|---|
| Created by | You place it in a scene | Auto-created on first `Instance` access (or found in scene) |
| `Instance` assigned | In `InitializeAwake` (called by `LoadingManager`) | On first access or in `Awake` |
| Before initialization | `Instance` is `null` | Never null (except on quit) |
| Persistence | `DontDestroyOnLoad` on init | `DontDestroyOnLoad` on creation/Awake |
| Duplicates | Newer one destroyed | Newer one destroyed |
| Requires `LoadingManager` | **Yes** | No |
| Inspector config | `AwakePriority` | `AwakePriority`, `showDebugLogs` |

### `MonoSingleton<T>`

```csharp
public class GameSettings : MonoSingleton<GameSettings>
{
    public override async Awaitable InitializeAwake(CancellationToken ct)
    {
        await base.InitializeAwake(ct);   // MUST call base: assigns Instance, DontDestroyOnLoad
        // your async init here
    }
}
```

- Implements [`IInitializeAwake`](../SceneInitialization/README.md). `LoadingManager` calls `InitializeAwake` for every such object in the scene, ordered by **`AwakePriority` descending** (set it in the Inspector).
- `Instance` is `null` until its `InitializeAwake` has run. Do not read it from Unity's `Awake`/`OnEnable`/`Start` of other objects; read it from your own `InitializeAwake`/`InitializeStart` with a lower priority than the singleton.
- If `LoadingManager` is not in play (e.g. you removed the Resources prefab), `Instance` stays `null`.
- If you define your own `Awake` in a subclass, there is no conflict (the base class has none).

### `LazyMonoSingleton<T>`

```csharp
public class Telemetry : LazyMonoSingleton<Telemetry> { }

Telemetry.Instance.Track("event");   // created if missing
```

- Uses `FindAnyObjectByType` first, otherwise creates a new GameObject named `"<Type> (Singleton)"`.
- Returns `null` (and logs a warning in the Editor) when the application is quitting, so guard calls from `OnDestroy`.
- Its base `Awake` is **non-virtual**: if your subclass declares its own `Awake`, it *hides* the base one and the duplicate-destroy logic will not run. Use `OnEnable`/`Start` or call into a different method.

---

## ObjectPoolManager

`ObjectPoolManager : MonoSingleton<ObjectPoolManager>`. Put it on a GameObject in your first scene (with `LoadingManager` bootstrapping, see above).

### Concepts

- One **pool per prefab**, keyed by the prefab's `EntityId` (no name collisions).
- Pooled instances get an internal `PooledObject` component that remembers their pool.
- Spawned objects are reparented to `parent` (or the scene root); idle ones live under `[Pooled Objects]/[Pool] <name>` when *Organize In Hierarchy* is on.

### API

| Method | Description |
|---|---|
| `GameObject Spawn(prefab, position, rotation, parent = null)` | Reuses an inactive instance or creates one. Pool is created on first use |
| `T Spawn<T>(…)` | Same, returns a component |
| `void Despawn(instance, delay = 0f)` | Returns to pool. Delay uses unscaled time. Objects not from a pool are `Destroy`ed |
| `void Prewarm(prefab, count, config = null)` | Creates `count` inactive instances synchronously |
| `IEnumerator PrewarmAsync(prefab, count, config = null, onComplete = null)` | Spreads creation over frames (`Max Instantiations Per Frame`). Run with `StartCoroutine` |
| `void ClearPool(prefab)` / `ClearAllPools()` | Destroys instances (active ones too) |
| `PoolStats GetPoolStats(prefab)` | Active/available/total/peak counts |
| `PoolAnalytics GetAnalytics()` | Spawn/despawn counters per prefab (`null` if disabled) |

```csharp
// Loading screen: warm 50 bullets without a hitch
yield return ObjectPoolManager.Instance.PrewarmAsync(bulletPrefab, 50);

// Gameplay
var b = ObjectPoolManager.Instance.Spawn<Bullet>(bulletPrefab, muzzle.position, muzzle.rotation);
ObjectPoolManager.Instance.Despawn(b.gameObject, 3f);   // auto-return after 3 s

Debug.Log(ObjectPoolManager.Instance.GetPoolStats(bulletPrefab));
```

### `IPoolable`

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnSpawn()   { /* reset state. Runs BEFORE SetActive(true) */ }
    public void OnDespawn() { /* cleanup.     Runs BEFORE the object is reset and deactivated */ }
}
```

Every `IPoolable` on the root GameObject is notified. `OnSpawn` is called before activation so `Update` never runs on stale state.

### On despawn the pool resets

Transform (local position/rotation/scale), and, per `PoolConfig`, `Rigidbody`/`Rigidbody2D` velocities, `Animator` (`Rebind` + `Update(0)`), and child `ParticleSystem`s (`Clear` + `Stop`).

### `PoolConfig`

Set the manager's *Default Config* in the Inspector, or pass a custom one to `Prewarm`/`PrewarmAsync` (it applies when that pool is first created).

| Field | Default | Meaning |
|---|---|---|
| `maxCapacity` | 100 | Max instances the pool will create |
| `cullWhenFull` | false | When at capacity, forcibly despawn an active object to satisfy a spawn |
| `enableCulling` | true | Periodically destroy idle instances |
| `cullAfterSeconds` | 60 | Idle time before an instance may be culled |
| `maxCullPerCheck` | 10 | Cap per culling pass |
| `resetRigidbody` / `resetAnimator` / `resetParticles` | true | Reset behaviour on despawn |
| `requirePoolableInterface` | false | Warn in the Editor if the prefab has no `IPoolable` |

### Manager settings

`Enable Analytics`, `Enable Debug Logs`, `Max Instantiations Per Frame` (5), `Cull Check Interval` (10 s), `Organize In Hierarchy`. At runtime the inspector shows total pools/spawns and a *Clear All Pools* button.

### Notes and limitations

- The culling coroutine only starts if the manager's **default** config has `enableCulling` true; per-pool configs are honoured when culling runs.
- `MemoryEfficiency` and `SpawnRate` in the stats classes are rough indicators, not precise profiling data.
- `PooledObject` is `internal`: return objects with `ObjectPoolManager.Instance.Despawn(go)`, not through the component.
