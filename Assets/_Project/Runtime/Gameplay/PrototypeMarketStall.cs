using System;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Input;
using UnityIsekaiGame.Interaction;

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
        private CursorLockMode priorLockMode;
        private bool priorCursorVisible;
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

            priorLockMode = Cursor.lockState;
            priorCursorVisible = Cursor.visible;
            open = true;
            input?.SetGameplayInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshCatalog(force: true);
        }

        private void OnDisable() => Close();

        private void OnGUI()
        {
            if (!open || services == null)
            {
                return;
            }

            float width = Mathf.Min(700f, Screen.width - 30f);
            float height = Mathf.Min(650f, Screen.height - 30f);
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(window, GUIContent.none);
            GUILayout.BeginArea(new Rect(window.x + 16f, window.y + 14f, window.width - 32f, window.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Gold: {services.GetPlayerBalance()}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("Town trade profile: imported raw iron ore and wood support local workshops; low-quality iron swords and wooden bows are the native exports.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            PrototypeMarketChangePlan marketChange = services.LastPrototypeMarketChange;
            if (marketChange != null)
            {
                GUILayout.Label($"Latest town interval: imported {marketChange.IronImports} iron, {marketChange.WoodImports} wood, and {marketChange.LeatherImports} leather; exported {marketChange.SwordExports} swords and {marketChange.BowExports} bows.",
                    new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Space(8f);
            scroll = GUILayout.BeginScrollView(scroll);
            RefreshCatalog(force: false);

            GUILayout.Label("Buy Town Goods", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.BeginHorizontal();
            GUILayout.Label("Quantity", GUILayout.Width(70f));
            if (GUILayout.Button("-", GUILayout.Width(32f))) purchaseQuantity = Math.Max(1, purchaseQuantity - 1);
            GUILayout.Label(purchaseQuantity.ToString(), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(45f));
            if (GUILayout.Button("+", GUILayout.Width(32f))) purchaseQuantity = Math.Min(99, purchaseQuantity + 1);
            GUILayout.EndHorizontal();

            foreach (PrototypeMarketListing listing in cachedListings.Where(entry => entry.Buyable))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(listing.DisplayName, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label($"{listing.EconomicRole} | Stock: {listing.AvailableStock} | Reference price: {listing.ReferencePrice} Gold each");
                if (listing.ExactSecondhandStock > 0L)
                {
                    GUILayout.Label($"Stock mix: {listing.ExactSecondhandStock} secondhand, {listing.AggregateStock} locally produced and not yet individualized.");
                }
                if (GUILayout.Button($"Request merchant quote and buy x{purchaseQuantity}", GUILayout.Height(30f)))
                {
                    PrototypeEconomyOperation result = services.BuyPrototypeMarketGood(listing.ItemDefinitionId, purchaseQuantity);
                    status = result.Message;
                    PrototypeHudMessageBus.Show(result.Message);
                    RefreshCatalog(force: true);
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(10f);
            GUILayout.Label("Sell Town Exports", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            PrototypeExportChoice[] exports = cachedExports;
            if (exports.Length == 0)
            {
                GUILayout.Label("Craft an iron sword or wooden bow, then return here to export it.");
            }
            foreach (PrototypeExportChoice choice in exports)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(choice.DisplayName, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label($"Instance: {ShortId(choice.ItemInstanceId)} | Final price includes quality, rarity, and durability.");
                if (GUILayout.Button("Request merchant quote and sell", GUILayout.Height(30f)))
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
            input?.SetGameplayInputBlocked(false);
            Cursor.lockState = priorLockMode;
            Cursor.visible = priorCursorVisible;
        }

        private static string ShortId(string value) => string.IsNullOrWhiteSpace(value) ? "unassigned" : value.Substring(0, Math.Min(8, value.Length));
    }
}
