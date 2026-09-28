using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.UI.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text itemNameText;
        [SerializeField] private Text quantityText;
        [SerializeField] private string emptyLabel = "Empty";
        [SerializeField] private Color normalColor = new Color(0.31f, 0.21f, 0.12f, 0.98f);
        [SerializeField] private Color selectedColor = new Color(0.47f, 0.32f, 0.15f, 1f);

        private int slotIndex = -1;
        private System.Action<int> selected;
        private System.Action<int, bool> hovered;
        private bool isSelected;
        private bool isHovered;
        private bool hasItem;
        private Color itemAccent;

        private void Awake()
        {
            normalColor = GameUiTheme.SlotOccupied;
            selectedColor = GameUiTheme.SlotSelected;
            itemAccent = GameUiTheme.Border;
            ApplyTextLayout();
            RefreshBackground();
        }

        private void OnValidate()
        {
            ResolveBackgroundImage();
            RefreshBackground();
        }

        public void Render(UnityIsekaiGame.Inventory.InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                RenderEmpty();
                return;
            }

            hasItem = true;
            itemAccent = ResolveItemAccent(slot.Item);
            ApplyTextLayout();

            if (itemNameText != null)
            {
                itemNameText.text = slot.Item.DisplayName;
            }

            if (quantityText != null)
            {
                quantityText.text = slot.Quantity.ToString();
            }

            if (iconImage != null)
            {
                Sprite icon = InventoryItemIconResolver.Resolve(slot.Item);
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
            }
            RefreshBackground();
        }

        public void RenderEmpty()
        {
            hasItem = false;
            itemAccent = GameUiTheme.Border;
            ApplyTextLayout();

            if (itemNameText != null)
            {
                itemNameText.text = emptyLabel;
            }

            if (quantityText != null)
            {
                quantityText.text = string.Empty;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            RefreshBackground();
        }

        public void Initialize(int index, System.Action<int> onSelected, System.Action<int, bool> onHovered = null)
        {
            slotIndex = index;
            selected = onSelected;
            hovered = onHovered;
            ResolveBackgroundImage();
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            ResolveBackgroundImage();
            normalColor = GameUiTheme.SlotOccupied;
            selectedColor = GameUiTheme.SlotSelected;
            ApplyTextLayout();
            RefreshBackground();
        }

        public void SetSelected(bool isSelected)
        {
            this.isSelected = isSelected;
            ResolveBackgroundImage();
            RefreshBackground();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            selected?.Invoke(slotIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshBackground();
            hovered?.Invoke(slotIndex, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshBackground();
            hovered?.Invoke(slotIndex, false);
        }

        private void ApplyTextLayout()
        {
            ResolveBackgroundImage();
            ConfigureIconImage();
            ConfigureNameText();
            ConfigureQuantityText();
        }

        private void ResolveBackgroundImage()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }
        }

        private void ConfigureNameText()
        {
            if (itemNameText == null)
            {
                return;
            }

            itemNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            itemNameText.verticalOverflow = VerticalWrapMode.Truncate;
            itemNameText.alignment = TextAnchor.MiddleLeft;
            itemNameText.color = hasItem ? GameUiTheme.TextPrimary : GameUiTheme.TextMuted;
            itemNameText.fontSize = Mathf.Max(12, itemNameText.fontSize);
            itemNameText.fontStyle = hasItem ? FontStyle.Bold : FontStyle.Italic;

            RectTransform rectTransform = itemNameText.rectTransform;
            rectTransform.anchorMin = hasItem ? new Vector2(0.38f, 0.31f) : new Vector2(0.08f, 0.16f);
            rectTransform.anchorMax = hasItem ? new Vector2(0.96f, 0.92f) : new Vector2(0.92f, 0.84f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            if (!hasItem) itemNameText.alignment = TextAnchor.MiddleCenter;
        }

        private void ConfigureIconImage()
        {
            if (iconImage == null)
            {
                return;
            }

            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform rectTransform = iconImage.rectTransform;
            rectTransform.anchorMin = new Vector2(0.04f, 0.14f);
            rectTransform.anchorMax = new Vector2(0.34f, 0.86f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ConfigureQuantityText()
        {
            if (quantityText == null)
            {
                return;
            }

            quantityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            quantityText.verticalOverflow = VerticalWrapMode.Truncate;
            quantityText.alignment = TextAnchor.LowerRight;
            quantityText.color = GameUiTheme.Accent;
            quantityText.fontStyle = FontStyle.Bold;

            RectTransform rectTransform = quantityText.rectTransform;
            rectTransform.anchorMin = new Vector2(0.38f, 0.06f);
            rectTransform.anchorMax = new Vector2(0.94f, 0.32f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void RefreshBackground()
        {
            if (backgroundImage == null)
            {
                return;
            }

            GameUiTheme.StyleSlot(backgroundImage, hasItem, isHovered, isSelected, itemAccent);
        }

        private static Color ResolveItemAccent(UnityIsekaiGame.Inventory.ItemDefinition item)
        {
            if (item?.Rarity == null || item.Rarity.IsDefault) return GameUiTheme.Border;
            Color color = item.Rarity.DisplayColor;
            if (color.maxColorComponent < 0.2f) return GameUiTheme.Border;
            return color;
        }
    }
}
