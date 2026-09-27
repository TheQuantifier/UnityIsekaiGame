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
            return party != null && party.MemberPersonIds.Contains(secondPersonId, StringComparer.Ordinal);
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
        private static AdventuringPartyService parties;
        private static PartyOperationalRuntime operations;
        public static void Configure(AdventuringPartyService partyService, PartyOperationalRuntime operationalRuntime) { parties = partyService; operations = operationalRuntime; }
        public static void Clear(PartyOperationalRuntime operationalRuntime) { if (ReferenceEquals(operations, operationalRuntime)) { parties = null; operations = null; } }
        public static bool CanDamage(GameObject source, string sourceActorId, GameObject target, string targetActorId)
        {
            string sourcePerson = ResolvePerson(source, sourceActorId);
            string targetPerson = ResolvePerson(target, targetActorId);
            return string.IsNullOrWhiteSpace(sourcePerson) || string.IsNullOrWhiteSpace(targetPerson) || PartyCombatRules.CanDamage(parties, operations, sourcePerson, targetPerson);
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
        [SerializeField] private PersonIdentity identity;
        [SerializeField] private Transform leader;
        [SerializeField, Min(0.1f)] private float movementSpeed = 4f;
        [SerializeField, Min(0.25f)] private float stopDistance = 1.25f;
        [SerializeField, Min(5f)] private float tooFarDistance = 35f;
        [SerializeField, Min(10f)] private float regroupTeleportDistance = 80f;
        [SerializeField] private bool allowRegroupTeleport = true;
        [SerializeField] private string locationId;

        private PrototypePersistenceServiceBehaviour persistence;
        private Vector3 heldPosition;
        private bool holding;

        public string PersonId => identity == null ? string.Empty : identity.PersonId;
        public PartyCommand CurrentCommand { get; private set; } = PartyCommand.Follow;

        private void Awake()
        {
            if (identity == null) identity = GetComponent<PersonIdentity>();
            persistence = FindAnyObjectByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (persistence == null || string.IsNullOrWhiteSpace(PersonId)) return;
            PartySnapshot party = persistence.AdventuringParties.GetPartyForPerson(PersonId);
            if (party == null) return;
            PartyMemberOperationalData state = persistence.PartyOperations.GetMember(party.PartyId, PersonId);
            if (state == null) return;
            CurrentCommand = state.command;
            ResolveLeader(party);
            if (leader == null)
            {
                persistence.PartyOperations.ReportMemberState(party.PartyId, PersonId, PartyMemberReadiness.Missing, locationId, 0f, isActiveAndEnabled, true, true);
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
                PartySettingsData settings = persistence.PartyOperations.GetSettings(party.PartyId);
                Vector3 target = leader.TransformPoint(PartyFormationLayout.Offset(settings.formation, state.formationSlot));
                float preferred = CurrentCommand == PartyCommand.Support ? stopDistance * 2.5f : stopDistance;
                if (CurrentCommand == PartyCommand.Regroup && allowRegroupTeleport && distance >= regroupTeleportDistance) transform.position = target;
                else if (distance > preferred) MoveTowards(target);
            }

            PartyMemberReadiness readiness = distance > tooFarDistance ? PartyMemberReadiness.TooFar : PartyMemberReadiness.Ready;
            persistence.PartyOperations.ReportMemberState(party.PartyId, PersonId, readiness, locationId, distance, isActiveAndEnabled, true, true);
        }

        public void SetLeader(Transform value) => leader = value;
        public void SetLocation(string value) => locationId = value ?? string.Empty;
        private void MoveTowards(Vector3 target) => transform.position = Vector3.MoveTowards(transform.position, target, movementSpeed * Time.deltaTime);
        private void ResolveLeader(PartySnapshot party)
        {
            if (leader != null) return;
            foreach (PersonIdentity candidate in FindObjectsByType<PersonIdentity>(FindObjectsInactive.Exclude))
                if (candidate.PersonId == party.LeaderPersonId) { leader = candidate.transform; return; }
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
        [SerializeField] private bool visible = true;
        private PrototypePersistenceServiceBehaviour persistence;
        private void Awake() => persistence = GetComponent<PrototypePersistenceServiceBehaviour>();
        private void OnGUI()
        {
            if (!visible || persistence == null) return;
            PartySnapshot party = persistence.AdventuringParties.GetPartyForPerson(persistence.PlayerPersonId);
            if (party == null) return;
            float width = Mathf.Min(310f, Screen.width - 30f);
            Rect panel = new Rect(Screen.width - width - 18f, 18f, width, 54f + 27f * party.MemberCount);
            PrototypeUiTheme.DrawPanelFrame(panel);
            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
            GUILayout.Label($"{party.DisplayName.ToUpperInvariant()}  {party.MemberCount}/{party.MaximumMembers}", PrototypeUiTheme.HeadingStyle);
            foreach (PartyMemberSnapshot member in party.Members)
            {
                PartyMemberOperationalData state = persistence.PartyOperations.GetMember(party.PartyId, member.PersonId);
                string marker = member.IsLeader ? "LEADER" : "ALLY";
                string displayName = PersonRegistry.TryGetIdentity(member.PersonId, out PersonIdentity identity) ? identity.DisplayName : member.PersonId;
                PartyMemberReadiness readiness = state?.readiness ?? PartyMemberReadiness.Missing;
                GUIStyle style = readiness == PartyMemberReadiness.Ready ? PrototypeUiTheme.BodyStyle : PrototypeUiTheme.MutedStyle;
                GUILayout.Label($"{marker}  {displayName}  |  {readiness}  |  {state?.command ?? PartyCommand.Follow}", style, GUILayout.Height(23f));
            }
            GUILayout.EndArea();
        }
    }

    public static class PartySceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachHud()
        {
            foreach (PrototypePersistenceServiceBehaviour persistence in UnityEngine.Object.FindObjectsByType<PrototypePersistenceServiceBehaviour>(FindObjectsInactive.Include))
                if (persistence.GetComponent<PartyHudOverlay>() == null) persistence.gameObject.AddComponent<PartyHudOverlay>();
        }
    }
}
