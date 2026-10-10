# Core

The Core module provides foundational systems and design patterns that are frequently used across Unity projects.

## Components

### Singletons
*   **`MonoSingleton<T>`**: A standard base class for creating MonoBehaviour-based singletons. Useful for managers that need to persist across scenes or exist globally.
*   **`LazyMonoSingleton<T>`**: A singleton that only instantiates itself when it is first accessed, saving resources and ensuring it only exists when required.

### Object Pooling
*   **`ObjectPoolManager`**: An advanced, AAA-grade object pooling system designed to minimize garbage collection and instantiate frame spikes.
    *   **Features:** Instance ID-based tracking, async instantiation, per-pool capacity limits, analytics, and automatic culling strategies.
    *   **Usage Example:**
        ```csharp
        // Spawn an object
        GameObject obj = ObjectPoolManager.Instance.Spawn(prefab, position, rotation);

        // Return to pool
        ObjectPoolManager.Instance.Despawn(obj);
        ```
