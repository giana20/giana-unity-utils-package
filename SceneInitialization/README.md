# SceneInitialization

The Scene Initialization module guarantees a safe, reliable, and smooth transition between scenes in your game, ensuring that required managers are loaded and initialized properly.

## Workflow

1.  **Bootstrapping:** The **`BootStrapper`** automatically executes before any scene loads (`RuntimeInitializeOnLoadMethod`). It ensures the master `LoadingManager` prefab is instantiated from Resources and set to `DontDestroyOnLoad`.
2.  **Loading Manager:** The **`LoadingManager`** singleton handles the asynchronous loading of scenes. It can manage loading overlays and orchestrates the transition.
3.  **Initialization Interfaces:** Objects in the loaded scene can implement **`IInitializeAwake`** or **`IInitializeStart`** to be explicitly initialized by the `LoadingManager` sequence, providing a controlled initialization flow rather than relying on Unity's unpredictable `Awake`/`Start` order.
4.  **Overlay:** The **`LoadingOverlayController`** can be customized to provide visually appealing fade-ins and fade-outs during the asynchronous scene load.

## Key Components
*   `BootStrapper`
*   `LoadingManager`
*   `LoadingOverlayController`
*   `IInitializeAwake`
*   `IInitializeStart`
