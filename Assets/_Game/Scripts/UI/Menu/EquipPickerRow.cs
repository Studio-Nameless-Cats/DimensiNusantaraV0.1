using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    // One row inside the equip picker popup: item name, "x2" count, and the stat delta
    // vs what's currently equipped ("+2 ATK  -1 DEF"). The "Lepas" (unequip) row is the
    // same prefab with no count and the losses as its delta. EquipPicker spawns these
    // and drives which one is selected; selected = Accent fill, same as the item chips.
    public class EquipPickerRow : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [Tooltip("The stat preview, rich-text colored by the picker (gains gold, losses deep red).")]
        [SerializeField] private TextMeshProUGUI deltaText;
        [SerializeField] private Button button;

        private Action _onClicked;
        private string _label;

        void Awake()
        {
            // Self-heal: if the field wasn't wired on the prefab, grab the Button that
            // lives on this same object. A row whose click goes nowhere is useless.
            if (button == null) button = GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[EquipPickerRow] Clicked: {_label}");
                    _onClicked?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning($"[EquipPickerRow] No Button found on '{name}' - clicks on this row will do nothing.");
            }
        }

        public void Setup(string label, int count, string deltaRichText, Action onClicked)
        {
            _onClicked = onClicked;
            _label = label;

            if (nameText != null) nameText.text = label;
            if (countText != null)
            {
                countText.text = count > 0 ? $"x{count}" : "";
                countText.gameObject.SetActive(count > 0);
            }
            if (deltaText != null)
            {
                deltaText.text = deltaRichText;
                deltaText.gameObject.SetActive(!string.IsNullOrEmpty(deltaRichText));
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? NusantaraPalette.Role.Accent
                                            : NusantaraPalette.Role.SurfaceRaised;
            if (nameText != null)
                nameText.color = NusantaraPalette.Role.OnDark;
            if (countText != null)
                countText.color = selected ? NusantaraPalette.Role.OnDark
                                           : NusantaraPalette.Role.Muted;
        }
    }
}
