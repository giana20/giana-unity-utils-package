# UI

The UI module provides robust tools to build, navigate, and manage Unity UI menus dynamically and efficiently.

## Key Components

*   **`UIBuilder<T>`**
    An abstract base class for dynamically constructing UI menus from a list of data items. It manages the instantiation of selectable prefabs, handles opening/closing events, and links UI elements to their corresponding data.
    
*   **Event System Handlers**
    *   **`EventSystemHandlerBase`**: Base functionality for handling UI navigation and interactions.
    *   **`DynamicEventSystemHandler`**: Specialized handler for dynamically generated menus.
    *   **`MenuEventSystemHandler`**: Specialized handler for static or predefined menus.
    
*   **Transitions & Animation**
    *   **`IUITransition`**: Interface defining the contract for UI transitions (Show/Hide).
    *   **`DefaultUITransition`**: A standard implementation for fading and scaling UI elements in and out.
    *   **`MenuAnimatorBase`** & **`DefaultMenuAnimator`**: Base and default implementations for animating entire menus during transitions.

*   **Utilities**
    *   **`MobileUIOnly`**: A simple utility to enable/disable specific UI elements based on the target platform (e.g., hiding on-screen controls on PC).
