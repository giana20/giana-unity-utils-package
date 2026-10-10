using UnityEngine;
using UnityEngine.InputSystem;

namespace Giana.UnityUtils
{
    public class MobileUIOnly : MonoBehaviour
    {
        void Start()
        {
            // Enable UI if a touchscreen is connected and recognized
            if (Touchscreen.current != null)
            {
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}