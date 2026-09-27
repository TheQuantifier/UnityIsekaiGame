using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.Parties;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Presentation;
using UnityIsekaiGame.UI.Inventory;

namespace UnityIsekaiGame.UI.Parties
{
    public sealed class PartyMenuExtension : MonoBehaviour, IInventoryMenuExtension
    {
        private InventoryScreenView menu;
        private PrototypePersistenceServiceBehaviour persistence;
        private PartyMenuView panel;
        private bool registered;
        public string ExtensionId => "gameplay.party";
        public string DisplayName => "Party";
        public int Order => 45;
        public bool IsAvailable => true;
        public bool SuppressFeedbackText => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => SceneManager.sceneLoaded -= SceneLoaded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; Attach(); }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Attach();
        private static void Attach()
        {
            foreach (InventoryScreenView view in FindObjectsByType<InventoryScreenView>(FindObjectsInactive.Include))
                if (view.GetComponent<PartyMenuExtension>() == null) view.gameObject.AddComponent<PartyMenuExtension>();
        }

        private void OnEnable()
        {
            menu ??= GetComponent<InventoryScreenView>();
            persistence ??= FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            if (menu != null && !registered) registered = menu.RegisterMenuExtension(this);
            if (persistence != null) persistence.PartyOperations.Changed += Refresh;
        }
        private void OnDisable()
        {
            if (persistence != null) persistence.PartyOperations.Changed -= Refresh;
            if (menu != null && registered) menu.UnregisterMenuExtension(this);
            registered = false;
        }
        public void Initialize(InventoryMenuExtensionContext context)
        {
            if (context?.ContentRoot == null) return;
            panel = context.ContentRoot.GetComponent<PartyMenuView>() ?? context.ContentRoot.gameObject.AddComponent<PartyMenuView>();
            panel.Initialize(persistence, context.Font);
        }
        public void Refresh() => panel?.Refresh();
        public void Show() { panel?.gameObject.SetActive(true); panel?.Refresh(); }
        public void Hide() { }
        public void Dispose() { if (persistence != null) persistence.PartyOperations.Changed -= Refresh; }
    }

    public sealed class PartyMenuView : MonoBehaviour
    {
        private PrototypePersistenceServiceBehaviour persistence;
        private Font font;
        private RectTransform content;
        private ScrollRect scrollRect;
        public void Initialize(PrototypePersistenceServiceBehaviour value, Font valueFont)
        {
            persistence = value; font = valueFont;
            RectTransform root = transform as RectTransform;
            if (root != null) { root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = new Vector2(12, 12); root.offsetMax = new Vector2(-12, -12); }
            EnsureStructure();
            Rebuild();
        }
        public void Refresh() { if (isActiveAndEnabled) Rebuild(); }
        private void Rebuild()
        {
            if (persistence == null) persistence = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
            EnsureStructure();
            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (persistence == null)
            {
                Label("Party services are unavailable. Return to the game world and reopen this screen.", 17, FontStyle.Bold, PrototypeUiTextRole.Warning);
                return;
            }
            string playerId = persistence.PlayerPersonId;
            PartySnapshot party = persistence.AdventuringParties.GetPartyForPerson(playerId);
            Label("ADVENTURING PARTY", 22, FontStyle.Bold, PrototypeUiTextRole.Title);
            if (party == null)
            {
                Label("NO ACTIVE PARTY", 16, FontStyle.Bold, PrototypeUiTextRole.Heading);
                Label("Create a party to coordinate companions, share quest progress, configure formation and loot rules, and travel together.", 14, FontStyle.Normal, PrototypeUiTextRole.Muted);
                Button("Create Party", () => { string id = $"party.{Safe(playerId)}.{DateTime.UtcNow.Ticks}"; persistence.AdventuringParties.CreateParty(id, "Adventuring Party", playerId, Time.timeAsDouble, id + ".create"); Rebuild(); });
                Invitations(playerId);
                return;
            }

            PartySettingsData settings = persistence.PartyOperations.GetSettings(party.PartyId);
            int readyCount = persistence.PartyOperations.GetReadyMemberIds(party.PartyId).Count;
            Label(party.DisplayName, 18, FontStyle.Bold, PrototypeUiTextRole.Heading);
            Label($"{party.MemberCount}/{party.MaximumMembers} MEMBERS   |   {readyCount}/{party.MemberCount} READY", 13, FontStyle.Bold,
                readyCount == party.MemberCount ? PrototypeUiTextRole.Success : PrototypeUiTextRole.Warning);
            Label($"Formation: {settings.formation}   |   Loot: {settings.lootPolicy}   |   Friendly fire: {settings.friendlyFire}", 13, FontStyle.Normal, PrototypeUiTextRole.Muted);
            Label("TACTICS", 16, FontStyle.Bold, PrototypeUiTextRole.Heading);
            Horizontal("Formation", Enum.GetValues(typeof(PartyFormation)).Cast<PartyFormation>().Select(value => (value.ToString(), (Action)(() => UpdateSettings(party, value, settings.lootPolicy, settings.friendlyFire, settings.groupCommand)))).ToArray());
            Horizontal("Command", Enum.GetValues(typeof(PartyCommand)).Cast<PartyCommand>().Select(value => (value.ToString(), (Action)(() => UpdateSettings(party, settings.formation, settings.lootPolicy, settings.friendlyFire, value)))).ToArray());
            Horizontal("Loot", Enum.GetValues(typeof(PartyLootPolicy)).Cast<PartyLootPolicy>().Select(value => (value.ToString(), (Action)(() => UpdateSettings(party, settings.formation, value, settings.friendlyFire, settings.groupCommand)))).ToArray());
            Button(settings.friendlyFire == PartyFriendlyFirePolicy.Prevent ? "Friendly Fire: Prevented" : "Friendly Fire: Allowed", () => UpdateSettings(party, settings.formation, settings.lootPolicy, settings.friendlyFire == PartyFriendlyFirePolicy.Prevent ? PartyFriendlyFirePolicy.Allow : PartyFriendlyFirePolicy.Prevent, settings.groupCommand));

            Label("ROSTER", 16, FontStyle.Bold, PrototypeUiTextRole.Heading);
            foreach (PartyMemberSnapshot member in party.Members)
            {
                PartyMemberOperationalData state = persistence.PartyOperations.GetMember(party.PartyId, member.PersonId);
                string label = $"{(member.IsLeader ? "Leader" : "Companion")}: {DisplayName(member.PersonId)} | {state?.readiness ?? PartyMemberReadiness.Missing} | {state?.command ?? PartyCommand.Follow}";
                PartyMemberReadiness readiness = state?.readiness ?? PartyMemberReadiness.Missing;
                Label(label, 13, FontStyle.Normal, readiness == PartyMemberReadiness.Ready ? PrototypeUiTextRole.Success : PrototypeUiTextRole.Warning);
                if (party.LeaderPersonId == playerId && !member.IsLeader)
                {
                    Horizontal(string.Empty,
                        ("Promote", (Action)(() => persistence.AdventuringParties.TransferLeadership(party.PartyId, playerId, member.PersonId, Time.timeAsDouble, Transaction("promote", member.PersonId)))),
                        ("Remove", (Action)(() => persistence.AdventuringParties.RemoveMember(party.PartyId, playerId, member.PersonId, Time.timeAsDouble, Transaction("remove", member.PersonId)))));
                }
            }

            Label("RECRUITMENT", 16, FontStyle.Bold, PrototypeUiTextRole.Heading);
            PartyRecruitable[] candidates = FindObjectsByType<PartyRecruitable>(FindObjectsInactive.Exclude)
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.PersonId) && !party.MemberPersonIds.Contains(x.PersonId, StringComparer.Ordinal)).ToArray();
            if (candidates.Length == 0) Label("No recruitable companions are present in this scene.", 13, FontStyle.Italic, PrototypeUiTextRole.Muted);
            foreach (PartyRecruitable candidate in candidates) Button($"Invite {DisplayName(candidate.PersonId)}", () => { candidate.Invite(persistence, playerId, out string message); Debug.Log(message, candidate); Rebuild(); });
            Invitations(playerId);
            if (party.LeaderPersonId == playerId) Button("Dissolve Party", () => { persistence.AdventuringParties.DissolveParty(party.PartyId, playerId, Time.timeAsDouble, Transaction("dissolve", party.PartyId)); Rebuild(); });
            else Button("Leave Party", () => { persistence.AdventuringParties.RemoveMember(party.PartyId, playerId, playerId, Time.timeAsDouble, Transaction("leave", playerId)); Rebuild(); });
        }

        private void Invitations(string playerId)
        {
            PartyInvitationData[] invitations = persistence.PartyOperations.QueryInvitations(playerId, PartyInvitationStatus.Pending).Where(x => x.invitedPersonId == playerId).ToArray();
            if (invitations.Length == 0) return;
            Label("INVITATIONS", 16, FontStyle.Bold, PrototypeUiTextRole.Heading);
            foreach (PartyInvitationData invitation in invitations)
            {
                Horizontal($"From {DisplayName(invitation.inviterPersonId)}",
                    ("Accept", (Action)(() => { persistence.PartyOperations.AcceptInvitation(invitation.invitationId, playerId, Time.timeAsDouble, Transaction("accept", invitation.invitationId)); Rebuild(); })),
                    ("Decline", (Action)(() => { persistence.PartyOperations.ResolveInvitation(invitation.invitationId, playerId, PartyInvitationStatus.Declined, Time.timeAsDouble, out _); Rebuild(); })));
            }
        }
        private void UpdateSettings(PartySnapshot party, PartyFormation formation, PartyLootPolicy loot, PartyFriendlyFirePolicy friendly, PartyCommand command) { persistence.PartyOperations.SetSettings(party.PartyId, persistence.PlayerPersonId, formation, loot, friendly, command, out _); Rebuild(); }
        private void EnsureStructure()
        {
            if (content != null && scrollRect != null) return;
            RectTransform root = transform as RectTransform;
            if (root == null) return;

            VerticalLayoutGroup obsoleteLayout = GetComponent<VerticalLayoutGroup>();
            if (obsoleteLayout != null) obsoleteLayout.enabled = false;

            Transform existing = transform.Find("Party Scroll View");
            GameObject scrollObject = existing == null
                ? new GameObject("Party Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect))
                : existing.gameObject;
            scrollObject.transform.SetParent(transform, false);
            RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
            scrollTransform.anchorMin = Vector2.zero;
            scrollTransform.anchorMax = Vector2.one;
            scrollTransform.offsetMin = Vector2.zero;
            scrollTransform.offsetMax = Vector2.zero;
            PrototypeUiTheme.StylePanel(scrollObject.GetComponent<Image>());

            Transform viewportExisting = scrollObject.transform.Find("Viewport");
            GameObject viewport = viewportExisting == null
                ? new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D))
                : viewportExisting.gameObject;
            viewport.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(10f, 10f);
            viewportRect.offsetMax = new Vector2(-10f, -10f);

            Transform contentExisting = viewport.transform.Find("Content");
            GameObject contentObject = contentExisting == null
                ? new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter))
                : contentExisting.gameObject;
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;
        }

        private Text Label(string value, int size, FontStyle style, PrototypeUiTextRole role = PrototypeUiTextRole.Body)
        {
            GameObject go = new GameObject("Party Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement)); go.transform.SetParent(content, false);
            Text text = go.GetComponent<Text>(); text.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = size; text.fontStyle = style; text.color = Color.white; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            PrototypeUiTheme.StyleText(text, role);
            go.GetComponent<LayoutElement>().preferredHeight = Math.Max(28, (size + 8) * (1 + (value?.Length ?? 0) / 90)); return text;
        }
        private Button Button(string value, Action action)
        {
            GameObject go = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)); go.transform.SetParent(content, false);
            go.GetComponent<LayoutElement>().preferredHeight = 38f;
            Button button = go.GetComponent<Button>(); button.onClick.AddListener(() => action?.Invoke());
            PrototypeUiTheme.StyleButton(button, PrototypeUiTheme.InferButtonTone(value));
            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false); RectTransform rect = textGo.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Text text = textGo.GetComponent<Text>(); text.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = 12; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; return button;
        }
        private void Horizontal(string heading, params (string label, Action action)[] actions)
        {
            if (!string.IsNullOrWhiteSpace(heading)) Label(heading.ToUpperInvariant(), 12, FontStyle.Bold, PrototypeUiTextRole.Muted);
            GameObject row = new GameObject("Party Action Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); row.transform.SetParent(content, false); row.GetComponent<HorizontalLayoutGroup>().spacing = 6f; row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true; row.GetComponent<LayoutElement>().preferredHeight = 36f;
            foreach ((string label, Action action) in actions)
            {
                GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(row.transform, false); Button button = go.GetComponent<Button>(); button.onClick.AddListener(() => action());
                PrototypeUiTheme.StyleButton(button, PrototypeUiTheme.InferButtonTone(label));
                GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false); RectTransform rect = textGo.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; Text text = textGo.GetComponent<Text>(); text.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = label; text.fontSize = 10; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            }
        }
        private string DisplayName(string personId) => PersonRegistry.TryGetIdentity(personId, out PersonIdentity identity) ? identity.DisplayName : personId;
        private static string Safe(string value) => new string((value ?? "player").Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-').ToArray());
        private static string Transaction(string kind, string target) => $"party.{kind}.{Safe(target)}.{DateTime.UtcNow.Ticks}";
    }
}
