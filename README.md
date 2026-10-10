# GianaUtils

GianaUtils is a comprehensive Unity utility library designed to accelerate game development by providing robust, reusable systems. It is organized into distinct modules, each addressing a specific area of development:

## Modules

*   **[Core](./Core/README.md)**
    Contains foundational patterns such as Singletons (`MonoSingleton`, `LazyMonoSingleton`) and a high-performance `ObjectPoolManager` for managing object lifecycles efficiently.
*   **[UI](./UI/README.md)**
    A suite of tools for building and managing dynamic user interfaces, including menu navigation, event system handling, dynamic menu generation (`UIBuilder`), and UI transition animations.
*   **[SceneInitialization](./SceneInitialization/README.md)**
    Provides a robust workflow for loading scenes, including a `BootStrapper` for guaranteed manager initialization, a master `LoadingManager` for asynchronous scene loading, and overlay transitions.
*   **[Integration](./Integration/README.md)**
    Optional integration wrappers for popular third-party Unity assets like **DOTween** and **Feel**, streamlining their usage within the GianaUtils architecture.
*   **[Editor](./Editor/README.md)**
    Editor-only utilities such as the `PackageDependencyChecker` to ensure all required external packages are present.

## Getting Started

Explore each module's `README.md` for detailed instructions and examples on how to utilize these systems in your project.
