using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class EquipmentSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Text label;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalColor = new Color(0.15f, 0.17f, 0.19f, 0.95f);
        [SerializeField] private Color selectedColor = new Color(0.35f, 0.52f, 0.68f, 0.95f);

        private EquipmentSlotType slotType;
        private System.Action<EquipmentSlotType> selected;

        public void Initialize(EquipmentSlotType type, System.Action<EquipmentSlotType> onSelected)
        {
            slotType = type;
            selected = onSelected;
            ResolveReferences();
            normalColor = PrototypeUiTheme.PanelRaised;
            selectedColor = PrototypeUiTheme.AccentSoft;
            PrototypeUiTheme.StyleText(label);
        }

        public void Render(EquipmentSlotState slot)
        {
            ResolveReferences();

            if (label != null)
            {
                string itemName = slot == null || slot.IsEmpty ? "Empty" : slot.Item.DisplayName;
                label.text = $"{FormatSlotName(slotType)}: {itemName}";
            }
        }

        public void SetSelected(bool isSelected)
        {
            ResolveReferences();

            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            selected?.Invoke(slotType);
        }

        private void ResolveReferences()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>();
            }
        }

        private static string FormatSlotName(EquipmentSlotType type)
        {
            return type switch
            {
                EquipmentSlotType.MainHand => "Main Hand",
                EquipmentSlotType.OffHand => "Off Hand",
                _ => type.ToString()
            };
        }
    }
}
