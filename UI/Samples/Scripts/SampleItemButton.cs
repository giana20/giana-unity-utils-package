using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Giana.UnityUtils
{
    /// <summary>
    /// Lives on the ROOT of the item prefab, next to the Button.
    /// Fills the visuals from a <see cref="SampleItem"/> and forwards the click to the builder.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SampleItemButton : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private Image _icon;

        /// <summary>Populates the visuals and sets the click callback (replaces any previous one).</summary>
        public void Bind(SampleItem item, Action<SampleItem> onClick)
        {
            if (_title != null)
                _title.text = item.Title;

            if (_description != null)
                _description.text = item.Description;

            if (_icon != null)
            {
                _icon.sprite = item.Icon;
                _icon.gameObject.SetActive(item.Icon != null);
            }

            Button button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(item));
        }
    }
}
