using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class EquipmentSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Text label;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalColor = new Color(0.31f, 0.21f, 0.12f, 0.98f);
        [SerializeField] private Color selectedColor = new Color(0.47f, 0.32f, 0.15f, 1f);

        private EquipmentSlotType slotType;
        private System.Action<EquipmentSlotType> selected;
        private bool isSelected;
        private bool isHovered;
        private bool hasItem;
        private Color itemAccent;

        public void Initialize(EquipmentSlotType type, System.Action<EquipmentSlotType> onSelected)
        {
            slotType = type;
            selected = onSelected;
            ResolveReferences();
            normalColor = GameUiTheme.SlotOccupied;
            selectedColor = GameUiTheme.SlotSelected;
            itemAccent = GameUiTheme.Border;
            GameUiTheme.StyleText(label);
            RefreshBackground();
        }

        public void Render(EquipmentSlotState slot)
        {
            ResolveReferences();
            hasItem = slot != null && !slot.IsEmpty;
            itemAccent = hasItem && slot.Item.Rarity != null && !slot.Item.Rarity.IsDefault
                ? slot.Item.Rarity.DisplayColor
                : GameUiTheme.Border;

            if (label != null)
            {
                string itemName = !hasItem ? "Empty" : slot.Item.DisplayName;
                label.text = $"{FormatSlotName(slotType)}: {itemName}";
                label.color = hasItem ? GameUiTheme.TextPrimary : GameUiTheme.TextMuted;
            }
            RefreshBackground();
        }

        public void SetSelected(bool isSelected)
        {
            ResolveReferences();
            this.isSelected = isSelected;
            RefreshBackground();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            selected?.Invoke(slotType);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshBackground();
        }

        public void RefreshPresentation()
        {
            ResolveReferences();
            normalColor = GameUiTheme.SlotOccupied;
            selectedColor = GameUiTheme.SlotSelected;
            GameUiTheme.StyleText(label, GameUiTextRole.Body);
            RefreshBackground();
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

        private void RefreshBackground()
        {
            GameUiTheme.StyleSlot(backgroundImage, hasItem, isHovered, isSelected, itemAccent);
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
