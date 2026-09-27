using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Interaction;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Gameplay
{
    public sealed class PrototypeMarketStall : MonoBehaviour, IInteractable
    {
        [SerializeField] private PrototypePersistenceServiceBehaviour services;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private string title = "Prototype Town Market";
        [SerializeField, Min(1)] private int purchaseQuantity = 1;

        private bool open;
        private Vector2 scroll;
        private string status = "Iron ore and wood are imported; low-quality iron swords and wooden bows are exported.";
        private PrototypeMarketListing[] cachedListings = Array.Empty<PrototypeMarketListing>();
        private PrototypeExportChoice[] cachedExports = Array.Empty<PrototypeExportChoice>();
        private float nextCatalogRefreshTime;

        public string InteractionPrompt => open ? "Trade (market already open)" : "Trade at Prototype Town Market";

        public bool CanInteract(in InteractionContext context)
        {
            ResolveReferences(context.Interactor);
            return services != null && !open;
        }

        public void Interact(in InteractionContext context)
        {
            ResolveReferences(context.Interactor);
            if (services == null)
            {
                PrototypeHudMessageBus.Show("Economy services are unavailable.");
                return;
            }

            open = true;
            if (input != null) input.SetMenuInputBlocked(this, true);
            else PlayerCursorMode.SetMenuOpen(this, true);
            RefreshCatalog(force: true);
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (open && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        private void OnGUI()
        {
            if (!open || services == null)
            {
                return;
            }

            float width = Mathf.Min(700f, Screen.width - 30f);
            float height = Mathf.Min(650f, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            PrototypeUiTheme.DrawPanelFrame(window, modal: true);
            GUILayout.BeginArea(new Rect(window.x + 16f, window.y + 14f, window.width - 32f, window.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, PrototypeUiTheme.TitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{services.GetPlayerBalance()} GOLD", PrototypeUiTheme.HeadingStyle);
            if (GUILayout.Button("Close", PrototypeUiTheme.DangerButtonStyle, GUILayout.Width(90f), GUILayout.Height(34f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("Imported raw iron and wood support local workshops. Iron swords and wooden bows are the town's primary exports.", PrototypeUiTheme.MutedStyle);
            PrototypeMarketChangePlan marketChange = services.LastPrototypeMarketChange;
            if (marketChange != null)
            {
                GUILayout.Label($"Latest town interval: imported {marketChange.IronImports} iron, {marketChange.WoodImports} wood, and {marketChange.LeatherImports} leather; exported {marketChange.SwordExports} swords and {marketChange.BowExports} bows.",
                    PrototypeUiTheme.BodyStyle);
            }
            GUILayout.Label(status, PrototypeUiTheme.StatusStyle);
            GUILayout.Space(8f);
            scroll = GUILayout.BeginScrollView(scroll);
            RefreshCatalog(force: false);

            GUILayout.Label("BUY TOWN GOODS", PrototypeUiTheme.HeadingStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quantity", GUILayout.Width(70f));
            if (GUILayout.Button("-", GUILayout.Width(32f))) purchaseQuantity = Math.Max(1, purchaseQuantity - 1);
            GUILayout.Label(purchaseQuantity.ToString(), PrototypeUiTheme.CenteredStyle, GUILayout.Width(45f));
            if (GUILayout.Button("+", GUILayout.Width(32f))) purchaseQuantity = Math.Min(99, purchaseQuantity + 1);
            GUILayout.EndHorizontal();

            foreach (PrototypeMarketListing listing in cachedListings.Where(entry => entry.Buyable))
            {
                GUILayout.BeginVertical(PrototypeUiTheme.CardStyle);
                GUILayout.Label(listing.DisplayName, PrototypeUiTheme.HeadingStyle);
                GUILayout.Label($"{listing.EconomicRole} | Stock: {listing.AvailableStock} | Reference price: {listing.ReferencePrice} Gold each");
                if (listing.ExactSecondhandStock > 0L)
                {
                    GUILayout.Label($"Stock mix: {listing.ExactSecondhandStock} secondhand, {listing.AggregateStock} locally produced and not yet individualized.");
                }
                if (GUILayout.Button($"Buy x{purchaseQuantity}", PrototypeUiTheme.PrimaryButtonStyle, GUILayout.Height(36f)))
                {
                    PrototypeEconomyOperation result = services.BuyPrototypeMarketGood(listing.ItemDefinitionId, purchaseQuantity);
                    status = result.Message;
                    PrototypeHudMessageBus.Show(result.Message);
                    RefreshCatalog(force: true);
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(10f);
            GUILayout.Label("SELL TOWN EXPORTS", PrototypeUiTheme.HeadingStyle);
            PrototypeExportChoice[] exports = cachedExports;
            if (exports.Length == 0)
            {
                GUILayout.Label("Craft an iron sword or wooden bow, then return here to export it.", PrototypeUiTheme.MutedStyle);
            }
            foreach (PrototypeExportChoice choice in exports)
            {
                GUILayout.BeginVertical(PrototypeUiTheme.CardStyle);
                GUILayout.Label(choice.DisplayName, PrototypeUiTheme.HeadingStyle);
                GUILayout.Label($"Instance: {ShortId(choice.ItemInstanceId)} | Final price includes quality, rarity, and durability.");
                if (GUILayout.Button("Request Quote and Sell", PrototypeUiTheme.PrimaryButtonStyle, GUILayout.Height(36f)))
                {
                    PrototypeEconomyOperation result = services.SellPrototypeExport(choice.ItemInstanceId);
                    status = result.Message;
                    PrototypeHudMessageBus.Show(result.Message);
                    RefreshCatalog(force: true);
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ResolveReferences(GameObject interactor)
        {
            if (services == null) services = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            if (input == null && interactor != null) input = interactor.GetComponentInParent<PlayerInputReader>();
            if (input == null) input = FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
        }

        private void RefreshCatalog(bool force)
        {
            if (services == null || (!force && Time.unscaledTime < nextCatalogRefreshTime))
            {
                return;
            }

            cachedListings = services.GetPrototypeMarketListings().ToArray();
            cachedExports = services.GetPrototypeExportChoices().ToArray();
            nextCatalogRefreshTime = Time.unscaledTime + 0.5f;
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            if (input != null) input.SetMenuInputBlocked(this, false);
            else PlayerCursorMode.SetMenuOpen(this, false);
        }

        private static string ShortId(string value) => string.IsNullOrWhiteSpace(value) ? "unassigned" : value.Substring(0, Math.Min(8, value.Length));
    }
}
