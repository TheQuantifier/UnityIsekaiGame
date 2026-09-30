using UnityEngine;

namespace UnityIsekaiGame.UI.Inventory
{
    public interface IInventoryMenuExtension
    {
        string ExtensionId { get; }
        string DisplayName { get; }
        int Order { get; }
        bool IsAvailable { get; }
        bool SuppressFeedbackText { get; }

        void Initialize(InventoryMenuExtensionContext context);
        void Refresh();
        void Show();
        void Hide();
        void Dispose();
    }

    public sealed class InventoryMenuExtensionContext
    {
        public InventoryMenuExtensionContext(InventoryScreenView menuView, RectTransform contentRoot, Font font)
        {
            MenuView = menuView;
            ContentRoot = contentRoot;
            Font = font;
        }

        public InventoryScreenView MenuView { get; }
        public RectTransform ContentRoot { get; }
        public Font Font { get; }
    }
}
