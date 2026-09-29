using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.Combat.CombatState
{
    internal sealed class RuntimeCombatParticipant
    {
        public RuntimeCombatParticipant(string actorId, string encounterId, float now, string reason)
        {
            ActorId = actorId;
            EncounterId = encounterId;
            State = CombatStateValue.InCombat;
            EnteredAt = now;
            LastActivityAt = now;
            DisengageEligibleAt = now;
            Revision = 1;
            TransitionReason = reason ?? string.Empty;
        }

        public string ActorId { get; }
        public string EncounterId { get; set; }
        public CombatStateValue State { get; set; }
        public float EnteredAt { get; }
        public float LastActivityAt { get; set; }
        public float DisengageEligibleAt { get; set; }
        public long Revision { get; set; }
        public string TransitionReason { get; set; }

        public void Refresh(string encounterId, float now, float timeout, string reason)
        {
            EncounterId = encounterId;
            State = CombatStateValue.InCombat;
            LastActivityAt = now;
            DisengageEligibleAt = now + timeout;
            Revision++;
            TransitionReason = reason ?? string.Empty;
        }
    }

    internal sealed class RuntimeCombatEngagement
    {
        public RuntimeCombatEngagement(string engagementId, string firstActorId, string secondActorId, string encounterId, float now, CombatActivityClassification classification, string originatingId)
        {
            EngagementId = engagementId;
            FirstActorId = firstActorId;
            SecondActorId = secondActorId;
            EncounterId = encounterId;
            CreatedAt = now;
            LastRefreshedAt = now;
            Classification = classification;
            OriginatingId = originatingId ?? string.Empty;
            Active = true;
            Revision = 1;
        }

        public string EngagementId { get; }
        public string FirstActorId { get; }
        public string SecondActorId { get; }
        public string EncounterId { get; set; }
        public float CreatedAt { get; }
        public float LastRefreshedAt { get; private set; }
        public CombatActivityClassification Classification { get; private set; }
        public string OriginatingId { get; private set; }
        public bool Active { get; private set; }
        public CombatExitReason EndReason { get; private set; }
        public long Revision { get; private set; }

        public bool Includes(string actorId)
        {
            return string.Equals(FirstActorId, actorId, StringComparison.Ordinal) || string.Equals(SecondActorId, actorId, StringComparison.Ordinal);
        }

        public void Refresh(string encounterId, float now, CombatActivityClassification classification, string originatingId)
        {
            EncounterId = encounterId;
            LastRefreshedAt = now;
            Classification = classification;
            OriginatingId = originatingId ?? string.Empty;
            Active = true;
            Revision++;
        }

        public void ReassignEncounter(string encounterId)
        {
            if (string.Equals(EncounterId, encounterId, StringComparison.Ordinal))
            {
                return;
            }

            EncounterId = encounterId ?? string.Empty;
            Revision++;
        }

        public void End(CombatExitReason reason)
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            EndReason = reason;
            Revision++;
        }
    }

    internal sealed class RuntimeCombatEncounter
    {
        public RuntimeCombatEncounter(string encounterId, float now)
        {
            EncounterId = encounterId;
            CreatedAt = now;
            LastActivityAt = now;
            Active = true;
            Revision = 1;
        }

        public string EncounterId { get; }
        public float CreatedAt { get; }
        public float LastActivityAt { get; set; }
        public bool Active { get; private set; }
        public CombatEncounterCompletionReason CompletionReason { get; private set; }
        public long Revision { get; private set; }
        public HashSet<string> ParticipantIds { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> EngagementIds { get; } = new HashSet<string>(StringComparer.Ordinal);

        public bool AddParticipant(string actorId)
        {
            bool added = ParticipantIds.Add(actorId);
            if (added)
            {
                Revision++;
            }

            return added;
        }

        public bool RemoveParticipant(string actorId)
        {
            bool removed = ParticipantIds.Remove(actorId);
            if (removed)
            {
                Revision++;
            }

            return removed;
        }

        public void Touch(float now)
        {
            LastActivityAt = now;
            Revision++;
        }

        public void ReplaceMembership(IEnumerable<string> participantIds, IEnumerable<string> engagementIds, float now)
        {
            ParticipantIds.Clear();
            foreach (string participantId in participantIds)
            {
                ParticipantIds.Add(participantId);
            }

            EngagementIds.Clear();
            foreach (string engagementId in engagementIds)
            {
                EngagementIds.Add(engagementId);
            }

            LastActivityAt = now;
            Revision++;
        }

        public void End(CombatEncounterCompletionReason reason)
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            CompletionReason = reason;
            Revision++;
        }
    }
}
