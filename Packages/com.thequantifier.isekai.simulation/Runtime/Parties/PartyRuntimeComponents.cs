using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityIsekaiGame.Gameplay;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Presentation;

namespace UnityIsekaiGame.Parties
{
    public static class PartyFormationLayout
    {
        public static Vector3 Offset(PartyFormation formation, int slot, float spacing = 2f)
        {
            if (slot <= 0) return Vector3.zero;
            int index = slot - 1;
            return formation switch
            {
                PartyFormation.Line => new Vector3((index % 2 == 0 ? -1 : 1) * (index / 2 + 1) * spacing, 0f, -spacing),
                PartyFormation.Wedge => new Vector3((index % 2 == 0 ? -1 : 1) * (index / 2 + 1) * spacing, 0f, -(index / 2 + 1) * spacing),
                PartyFormation.Circle => Quaternion.Euler(0f, index * 360f / Math.Max(1, slot), 0f) * Vector3.back * spacing * 1.5f,
                _ => new Vector3(((index * 37) % 5 - 2) * spacing * 0.6f, 0f, -(1 + index / 3) * spacing)
            };
        }
    }

    public static class PartyCombatRules
    {
        public static bool AreAllies(AdventuringPartyService parties, string firstPersonId, string secondPersonId)
        {
            PartySnapshot party = parties?.GetPartyForPerson(firstPersonId);
            if (party == null) return false;
            for (int i = 0; i < party.MemberPersonIds.Count; i++)
            {
                if (string.Equals(party.MemberPersonIds[i], secondPersonId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static bool CanDamage(AdventuringPartyService parties, PartyOperationalRuntime operations, string sourcePersonId, string targetPersonId)
        {
            if (!AreAllies(parties, sourcePersonId, targetPersonId)) return true;
            PartySnapshot party = parties.GetPartyForPerson(sourcePersonId);
            return operations?.GetSettings(party.PartyId).friendlyFire == PartyFriendlyFirePolicy.Allow;
        }

        public static bool CanHealOrRevive(AdventuringPartyService parties, string sourcePersonId, string targetPersonId) => AreAllies(parties, sourcePersonId, targetPersonId) || string.Equals(sourcePersonId, targetPersonId, StringComparison.Ordinal);
    }

    public static class PartyCombatContext
    {
        private static readonly System.Collections.Generic.Dictionary<PartyOperationalRuntime, AdventuringPartyService> Contexts = new System.Collections.Generic.Dictionary<PartyOperationalRuntime, AdventuringPartyService>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Contexts.Clear();
        public static void Configure(AdventuringPartyService partyService, PartyOperationalRuntime operationalRuntime)
        {
            if (partyService != null && operationalRuntime != null) Contexts[operationalRuntime] = partyService;
        }
        public static void Clear(PartyOperationalRuntime operationalRuntime)
        {
            if (operationalRuntime != null) Contexts.Remove(operationalRuntime);
        }
        public static bool CanDamage(GameObject source, string sourceActorId, GameObject target, string targetActorId)
        {
            string sourcePerson = ResolvePerson(source, sourceActorId);
            string targetPerson = ResolvePerson(target, targetActorId);
            if (string.IsNullOrWhiteSpace(sourcePerson) || string.IsNullOrWhiteSpace(targetPerson)) return true;
            foreach (System.Collections.Generic.KeyValuePair<PartyOperationalRuntime, AdventuringPartyService> context in Contexts.ToArray())
            {
                if (context.Key == null || context.Value == null)
                {
                    Contexts.Remove(context.Key);
                    continue;
                }

                PartySnapshot sourceParty = context.Value.GetPartyForPerson(sourcePerson);
                if (sourceParty == null || !sourceParty.MemberPersonIds.Contains(targetPerson)) continue;
                return PartyCombatRules.CanDamage(context.Value, context.Key, sourcePerson, targetPerson);
            }
            return true;
        }
        private static string ResolvePerson(GameObject value, string fallback)
        {
            PersonIdentity identity = value == null ? null : value.GetComponentInParent<PersonIdentity>();
            return identity != null && !string.IsNullOrWhiteSpace(identity.PersonId) ? identity.PersonId : fallback ?? string.Empty;
        }
    }

    /// <summary>Attach to an NPC with a PersonIdentity to make it obey the saved party command.</summary>
    public sealed class PartyCompanionAgent : MonoBehaviour
    {
        private const double StateReportIntervalSeconds = 0.25d;
        private const double LeaderSearchIntervalSeconds = 1d;

        [SerializeField] private PersonIdentity identity;
        [SerializeField] private Transform leader;
        [SerializeField, Min(0.1f)] private float movementSpeed = 4f;
        [SerializeField, Min(0.25f)] private float stopDistance = 1.25f;
        [SerializeField, Min(5f)] private float tooFarDistance = 35f;
        [SerializeField, Min(10f)] private float regroupTeleportDistance = 80f;
        [SerializeField] private bool allowRegroupTeleport = true;
        [SerializeField] private string locationId;

        private PrototypePersistenceServiceBehaviour persistence;
        private AdventuringPartyService partyService;
        private PartyOperationalRuntime partyOperations;
        private PartySnapshot activeParty;
        private PartyMemberOperationalData memberState;
        private PartySettingsData partySettings;
        private Vector3 heldPosition;
        private bool holding;
        private bool partySubscriptionActive;
        private bool partyCacheDirty = true;
        private long cachedOperationalRevision = -1;
        private double nextStateReportAt;
        private double nextLeaderSearchAt;

        public string PersonId => identity == null ? string.Empty : identity.PersonId;
        public PartyCommand CurrentCommand { get; private set; } = PartyCommand.Follow;

        private void Awake()
        {
            if (identity == null) identity = GetComponent<PersonIdentity>();
            ResolveServices();
        }

        private void OnEnable() => ResolveServices();

        private void OnDisable()
        {
            if (partySubscriptionActive && partyService != null)
            {
                partyService.Changed -= InvalidatePartyCache;
            }

            partySubscriptionActive = false;
        }

        private void Update()
        {
            if (persistence == null)
            {
                ResolveServices();
            }

            if (persistence == null || string.IsNullOrWhiteSpace(PersonId)) return;
            RefreshCachedState();
            if (activeParty == null || memberState == null) return;
            CurrentCommand = memberState.command;
            ResolveLeader(activeParty);
            if (leader == null)
            {
                ReportState(PartyMemberReadiness.Missing, 0f);
                return;
            }

            float distance = Vector3.Distance(transform.position, leader.position);
            if (CurrentCommand == PartyCommand.Hold)
            {
                if (!holding) { heldPosition = transform.position; holding = true; }
                MoveTowards(heldPosition);
            }
            else
            {
                holding = false;
                Vector3 target = leader.TransformPoint(PartyFormationLayout.Offset(partySettings?.formation ?? PartyFormation.Wedge, memberState.formationSlot));
                float preferred = CurrentCommand == PartyCommand.Support ? stopDistance * 2.5f : stopDistance;
                if (CurrentCommand == PartyCommand.Regroup && allowRegroupTeleport && distance >= regroupTeleportDistance) transform.position = target;
                else if (distance > preferred) MoveTowards(target);
            }

            PartyMemberReadiness readiness = distance > tooFarDistance ? PartyMemberReadiness.TooFar : PartyMemberReadiness.Ready;
            ReportState(readiness, distance);
        }

        public void SetLeader(Transform value) => leader = value;
        public void SetLocation(string value) => locationId = value ?? string.Empty;
        private void MoveTowards(Vector3 target) => transform.position = Vector3.MoveTowards(transform.position, target, movementSpeed * Time.deltaTime);
        private void ResolveLeader(PartySnapshot party)
        {
            if (leader != null) return;
            double now = Time.unscaledTimeAsDouble;
            if (now < nextLeaderSearchAt) return;
            nextLeaderSearchAt = now + LeaderSearchIntervalSeconds;
            foreach (PersonIdentity candidate in FindObjectsByType<PersonIdentity>(FindObjectsInactive.Exclude))
                if (candidate.PersonId == party.LeaderPersonId) { leader = candidate.transform; return; }
        }

        private void ResolveServices()
        {
            persistence ??= PrototypePersistenceServiceBehaviour.FindForPartyMember(PersonId);
            if (persistence == null) return;

            AdventuringPartyService resolvedPartyService = persistence.AdventuringParties;
            if (!ReferenceEquals(partyService, resolvedPartyService))
            {
                if (partySubscriptionActive && partyService != null) partyService.Changed -= InvalidatePartyCache;
                partyService = resolvedPartyService;
                partySubscriptionActive = false;
                partyCacheDirty = true;
            }

            partyOperations = persistence.PartyOperations;
            if (!partySubscriptionActive && partyService != null && isActiveAndEnabled)
            {
                partyService.Changed += InvalidatePartyCache;
                partySubscriptionActive = true;
            }
        }

        private void InvalidatePartyCache()
        {
            partyCacheDirty = true;
            leader = null;
            nextLeaderSearchAt = 0d;
        }

        private void RefreshCachedState()
        {
            if (partyCacheDirty)
            {
                activeParty = partyService?.GetPartyForPerson(PersonId);
                partyCacheDirty = false;
                cachedOperationalRevision = -1;
            }

            if (activeParty == null || partyOperations == null)
            {
                memberState = null;
                partySettings = null;
                return;
            }

            if (cachedOperationalRevision == partyOperations.Revision)
            {
                return;
            }

            memberState = partyOperations.GetMember(activeParty.PartyId, PersonId);
            partySettings = partyOperations.GetSettings(activeParty.PartyId);
            cachedOperationalRevision = partyOperations.Revision;
        }

        private void ReportState(PartyMemberReadiness readiness, float distance)
        {
            double now = Time.unscaledTimeAsDouble;
            if (activeParty == null || partyOperations == null || now < nextStateReportAt) return;
            nextStateReportAt = now + StateReportIntervalSeconds;
            partyOperations.ReportMemberState(activeParty.PartyId, PersonId, readiness, locationId, distance, isActiveAndEnabled, true, true);
        }
    }

    /// <summary>Optional scene component for an NPC that can be invited through dialogue or interaction code.</summary>
    public sealed class PartyRecruitable : MonoBehaviour
    {
        [SerializeField] private PersonIdentity identity;
        [SerializeField] private bool autoAcceptInvitation;
        public string PersonId => identity == null ? string.Empty : identity.PersonId;
        public bool AutoAcceptInvitation => autoAcceptInvitation;
        private void Awake() { if (identity == null) identity = GetComponent<PersonIdentity>(); }
        public bool Invite(PrototypePersistenceServiceBehaviour persistence, string leaderPersonId, out string message)
        {
            PartySnapshot party = persistence?.AdventuringParties.GetPartyForPerson(leaderPersonId);
            if (party == null) { message = "The leader must create a party first."; return false; }
            string transaction = $"party.invite.{party.PartyId}.{PersonId}.{DateTime.UtcNow.Ticks}";
            if (!persistence.PartyOperations.Invite(party.PartyId, leaderPersonId, PersonId, Time.timeAsDouble, transaction, out message)) return false;
            if (!autoAcceptInvitation) return true;
            PartyInvitationData invitation = persistence.PartyOperations.QueryInvitations(PersonId, PartyInvitationStatus.Pending).LastOrDefault();
            PartyOperationResult result = persistence.PartyOperations.AcceptInvitation(invitation?.invitationId, PersonId, Time.timeAsDouble, transaction + ".accept");
            message = result.Message; return result.Succeeded;
        }
    }

    public sealed class PartyTravelCoordinator
    {
        private readonly AdventuringPartyService parties;
        private readonly PartyOperationalRuntime operations;
        public PartyTravelCoordinator(AdventuringPartyService parties, PartyOperationalRuntime operations) { this.parties = parties; this.operations = operations; }
        public bool CanTravel(string leaderPersonId, bool requireAllReady, out string message)
        {
            PartySnapshot party = parties?.GetPartyForPerson(leaderPersonId);
            if (party == null) { message = string.Empty; return true; }
            PartyMemberOperationalData[] unavailable = operations.QueryMembers(party.PartyId).Where(x => x.readiness != PartyMemberReadiness.Ready).ToArray();
            if (requireAllReady && unavailable.Length > 0) { message = $"Party travel blocked: {string.Join(", ", unavailable.Select(x => $"{x.personId} ({x.readiness})"))}."; return false; }
            message = unavailable.Length == 0 ? "Party is ready to travel." : $"Traveling without {unavailable.Length} unavailable member(s)."; return true;
        }
        public void CompleteTravel(string leaderPersonId, string destinationLocationId) { PartySnapshot party = parties?.GetPartyForPerson(leaderPersonId); if (party != null) operations.CompleteTravel(party.PartyId, destinationLocationId); }
    }

    public sealed class PartyHudOverlay : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.25f;

        [SerializeField] private bool visible = true;
        private PrototypePersistenceServiceBehaviour persistence;
        private AdventuringPartyService partyService;
        private PartyOperationalRuntime partyOperations;
        private PartySnapshot cachedParty;
        private readonly System.Collections.Generic.List<PartyHudMemberRow> cachedRows = new System.Collections.Generic.List<PartyHudMemberRow>();
        private string cachedHeader = string.Empty;
        private bool partyCacheDirty = true;
        private long cachedOperationalRevision = -1;
        private float nextRefreshAt;

        private void Awake()
        {
            persistence = GetComponent<PrototypePersistenceServiceBehaviour>();
            partyService = persistence?.AdventuringParties;
            partyOperations = persistence?.PartyOperations;
        }

        private void OnEnable()
        {
            if (partyService != null) partyService.Changed += InvalidatePartyCache;
            partyCacheDirty = true;
            RefreshCache();
        }

        private void OnDisable()
        {
            if (partyService != null) partyService.Changed -= InvalidatePartyCache;
        }

        private void Update()
        {
            if (!visible || Time.unscaledTime < nextRefreshAt) return;
            RefreshCache();
        }

        private void OnGUI()
        {
            if (!visible || persistence == null) return;
            if (cachedParty == null) return;
            float width = Mathf.Min(310f, Screen.width - 30f);
            Rect panel = new Rect(Screen.width - width - 18f, 18f, width, 54f + 27f * cachedRows.Count);
            GameUiTheme.DrawPanelFrame(panel);
            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
            GUILayout.Label(cachedHeader, GameUiTheme.HeadingStyle);
            for (int i = 0; i < cachedRows.Count; i++)
            {
                PartyHudMemberRow row = cachedRows[i];
                GUILayout.Label(row.Label, row.Ready ? GameUiTheme.BodyStyle : GameUiTheme.MutedStyle, GUILayout.Height(23f));
            }
            GUILayout.EndArea();
        }

        private void InvalidatePartyCache() => partyCacheDirty = true;

        private void RefreshCache()
        {
            nextRefreshAt = Time.unscaledTime + RefreshIntervalSeconds;
            if (persistence == null) return;
            partyService ??= persistence.AdventuringParties;
            partyOperations ??= persistence.PartyOperations;
            if (partyCacheDirty)
            {
                cachedParty = partyService?.GetPartyForPerson(persistence.PlayerPersonId);
                partyCacheDirty = false;
                cachedOperationalRevision = -1;
            }

            if (cachedParty == null)
            {
                cachedHeader = string.Empty;
                cachedRows.Clear();
                return;
            }

            if (cachedOperationalRevision == partyOperations.Revision && cachedRows.Count == cachedParty.MemberCount) return;
            cachedOperationalRevision = partyOperations.Revision;
            cachedHeader = $"{cachedParty.DisplayName.ToUpperInvariant()}  {cachedParty.MemberCount}/{cachedParty.MaximumMembers}";
            cachedRows.Clear();
            for (int i = 0; i < cachedParty.Members.Count; i++)
            {
                PartyMemberSnapshot member = cachedParty.Members[i];
                PartyMemberOperationalData state = partyOperations.GetMember(cachedParty.PartyId, member.PersonId);
                string marker = member.IsLeader ? "LEADER" : "ALLY";
                string displayName = PersonRegistry.TryGetIdentity(member.PersonId, out PersonIdentity identity) ? identity.DisplayName : member.PersonId;
                PartyMemberReadiness readiness = state?.readiness ?? PartyMemberReadiness.Missing;
                cachedRows.Add(new PartyHudMemberRow(
                    $"{marker}  {displayName}  |  {readiness}  |  {state?.command ?? PartyCommand.Follow}",
                    readiness == PartyMemberReadiness.Ready));
            }
        }

        private readonly struct PartyHudMemberRow
        {
            public PartyHudMemberRow(string label, bool ready)
            {
                Label = label;
                Ready = ready;
            }

            public string Label { get; }
            public bool Ready { get; }
        }
    }

    public static class PartySceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachHud()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            foreach (PrototypePersistenceServiceBehaviour persistence in UnityEngine.Object.FindObjectsByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include))
                if (persistence.GetComponent<PartyHudOverlay>() == null) persistence.gameObject.AddComponent<PartyHudOverlay>();
        }
    }
}
