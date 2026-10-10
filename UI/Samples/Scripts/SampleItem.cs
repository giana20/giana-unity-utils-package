using System;
using UnityEngine;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Data type shown by the sample dynamic menu (<see cref="SampleLevelMenu"/>).
    /// Any serializable class or struct can be used as the T of UIBuilder&lt;T&gt;.
    /// </summary>
    [Serializable]
    public class SampleItem
    {
        public string Title;

        [TextArea]
        public string Description;

        [Tooltip("Optional. If empty the Icon object of the prefab is hidden.")]
        public Sprite Icon;
    }
}
