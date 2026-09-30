using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Condition
{
    public sealed class StructureConditionSnapshot
    {
        public StructureConditionSnapshot(
            string nodeId,
            string runtimeNodeId,
            string displayName,
            int maximumIntegrity,
            int currentIntegrity,
            StructureFunctionalState functionalState,
            StructureDamageState structuralState,
            RuntimeStructurePresenceState runtimePresence,
            IReadOnlyList<string> activeInjuryIds,
            long revision)
        {
            NodeId = nodeId ?? string.Empty;
            RuntimeNodeId = runtimeNodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MaximumIntegrity = Math.Max(0, maximumIntegrity);
            CurrentIntegrity = Math.Max(0, currentIntegrity);
            FunctionalState = functionalState;
            StructuralState = structuralState;
            RuntimePresence = runtimePresence;
            ActiveInjuryIds = activeInjuryIds == null ? Array.Empty<string>() : activeInjuryIds.ToArray();
            Revision = revision;
        }

        public string NodeId { get; }
        public string RuntimeNodeId { get; }
        public string DisplayName { get; }
        public int MaximumIntegrity { get; }
        public int CurrentIntegrity { get; }
        public float IntegrityPercent => MaximumIntegrity <= 0 ? 0f : (float)CurrentIntegrity / MaximumIntegrity;
        public StructureFunctionalState FunctionalState { get; }
        public StructureDamageState StructuralState { get; }
        public RuntimeStructurePresenceState RuntimePresence { get; }
        public IReadOnlyList<string> ActiveInjuryIds { get; }
        public long Revision { get; }
        public bool Present => RuntimePresence == RuntimeStructurePresenceState.Present;
        public bool Failed => FunctionalState == StructureFunctionalState.Disabled
            || FunctionalState == StructureFunctionalState.Destroyed
            || StructuralState == StructureDamageState.Destroyed
            || StructuralState == StructureDamageState.Severed
            || RuntimePresence == RuntimeStructurePresenceState.Destroyed
            || RuntimePresence == RuntimeStructurePresenceState.Severed
            || RuntimePresence == RuntimeStructurePresenceState.Missing;
    }

    public sealed class InjuryRecordSnapshot
    {
        public InjuryRecordSnapshot(
            string injuryId,
            string actorBodyId,
            string targetNodeId,
            string injuryDefinitionId,
            string sourceActorBodyId,
            string sourceTransactionId,
            string damageTypeId,
            InjurySeverity severity,
            int appliedStructuralDamage,
            StructureFunctionalState functionalImpact,
            StructureDamageState structuralImpact,
            InjuryRecordState state,
            long sequence,
            long revision)
        {
            InjuryId = injuryId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            TargetNodeId = targetNodeId ?? string.Empty;
            InjuryDefinitionId = injuryDefinitionId ?? string.Empty;
            SourceActorBodyId = sourceActorBodyId ?? string.Empty;
            SourceTransactionId = sourceTransactionId ?? string.Empty;
            DamageTypeId = damageTypeId ?? string.Empty;
            Severity = severity;
            AppliedStructuralDamage = Math.Max(0, appliedStructuralDamage);
            FunctionalImpact = functionalImpact;
            StructuralImpact = structuralImpact;
            State = state;
            Sequence = sequence;
            Revision = revision;
        }

        public string InjuryId { get; }
        public string ActorBodyId { get; }
        public string TargetNodeId { get; }
        public string InjuryDefinitionId { get; }
        public string SourceActorBodyId { get; }
        public string SourceTransactionId { get; }
        public string DamageTypeId { get; }
        public InjurySeverity Severity { get; }
        public int AppliedStructuralDamage { get; }
        public StructureFunctionalState FunctionalImpact { get; }
        public StructureDamageState StructuralImpact { get; }
        public InjuryRecordState State { get; }
        public long Sequence { get; }
        public long Revision { get; }
    }

    public sealed class BodyConditionSnapshot
    {
        public BodyConditionSnapshot(
            string actorBodyId,
            string anatomyDefinitionId,
            BodyConditionReadinessState readiness,
            long bodyRevision,
            long anatomyRevision,
            long conditionRevision,
            IReadOnlyList<StructureConditionSnapshot> structures,
            IReadOnlyList<InjuryRecordSnapshot> injuries,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            AnatomyDefinitionId = anatomyDefinitionId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            Structures = structures == null ? Array.Empty<StructureConditionSnapshot>() : structures.ToArray();
            Injuries = injuries == null ? Array.Empty<InjuryRecordSnapshot>() : injuries.ToArray();
            ActiveInjuries = Injuries.Where(injury => injury.State == InjuryRecordState.Active).ToArray();
            ImpairedStructures = Structures.Where(structure => structure.FunctionalState != StructureFunctionalState.Normal || structure.StructuralState != StructureDamageState.Intact).ToArray();
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string AnatomyDefinitionId { get; }
        public BodyConditionReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public IReadOnlyList<StructureConditionSnapshot> Structures { get; }
        public IReadOnlyList<InjuryRecordSnapshot> Injuries { get; }
        public IReadOnlyList<InjuryRecordSnapshot> ActiveInjuries { get; }
        public IReadOnlyList<StructureConditionSnapshot> ImpairedStructures { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
