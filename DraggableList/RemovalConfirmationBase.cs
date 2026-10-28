using System;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Asks the player to confirm deleting a dragged-out item. Derive to build your own dialog
    /// (or see <see cref="RemovalConfirmationPopup"/>). Exactly one of the callbacks must be invoked.
    /// </summary>
    public abstract class RemovalConfirmationBase : MonoBehaviour
    {
        public abstract void RequestConfirmation(DraggableItem item, Action onConfirm, Action onCancel);
    }
}
