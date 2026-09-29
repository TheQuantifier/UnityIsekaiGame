using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.Recovery
{
    public sealed class RecoveryRestContextSnapshot
    {
        public RecoveryRestContextSnapshot(string actorBodyId, RecoveryRestType restType, string sourceId, string transactionId, float quality, IReadOnlyList<string> tags)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            RestType = restType;
            SourceId = sourceId ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Quality = quality;
            Tags = tags == null ? Array.Empty<string>() : tags.ToArray();
        }

        public string ActorBodyId { get; }
        public RecoveryRestType RestType { get; }
        public string SourceId { get; }
        public string TransactionId { get; }
        public float Quality { get; }
        public IReadOnlyList<string> Tags { get; }
        public bool Active => RestType != RecoveryRestType.NotResting && RestType != RecoveryRestType.Unknown;
    }

    public sealed class RecoveryTargetSnapshot
    {
        public RecoveryTargetSnapshot(RecoveryTargetCategory targetCategory, string actorBodyId, string anatomyNodeId, string injuryId, string resourceDefinitionId, string hazardInstanceId, string stableTargetKey, long owningSystemRevision)
        {
            TargetCategory = targetCategory;
            ActorBodyId = actorBodyId ?? string.Empty;
            AnatomyNodeId = anatomyNodeId ?? string.Empty;
            InjuryId = injuryId ?? string.Empty;
            ResourceDefinitionId = resourceDefinitionId ?? string.Empty;
            HazardInstanceId = hazardInstanceId ?? string.Empty;
            StableTargetKey = stableTargetKey ?? string.Empty;
            OwningSystemRevision = owningSystemRevision;
        }

        public RecoveryTargetCategory TargetCategory { get; }
        public string ActorBodyId { get; }
        public string AnatomyNodeId { get; }
        public string InjuryId { get; }
        public string ResourceDefinitionId { get; }
        public string HazardInstanceId { get; }
        public string StableTargetKey { get; }
        public long OwningSystemRevision { get; }
    }

    public sealed class RecoveryProcessSnapshot
    {
        public RecoveryProcessSnapshot(string processId, string actorBodyId, string recoveryMethodId, string sourceId, RecoveryTargetSnapshot target, float currentProgress, float requiredProgress, float baseRatePerHour, float effectiveRatePerHour, RecoveryProcessState state, RecoveryInterruptionPolicy interruptionPolicy, RecoveryLimit recoveryLimit, RecoveryPermanentOutcome projectedPermanentOutcome, string compatibilitySummary, string lastCommittedTickId, long revision)
        {
            ProcessId = processId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            RecoveryMethodId = recoveryMethodId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Target = target;
            CurrentProgress = currentProgress;
            RequiredProgress = requiredProgress;
            BaseRatePerHour = baseRatePerHour;
            EffectiveRatePerHour = effectiveRatePerHour;
            State = state;
            InterruptionPolicy = interruptionPolicy;
            RecoveryLimit = recoveryLimit;
            ProjectedPermanentOutcome = projectedPermanentOutcome;
            CompatibilitySummary = compatibilitySummary ?? string.Empty;
            LastCommittedTickId = lastCommittedTickId ?? string.Empty;
            Revision = revision;
        }

        public string ProcessId { get; }
        public string ActorBodyId { get; }
        public string RecoveryMethodId { get; }
        public string SourceId { get; }
        public RecoveryTargetSnapshot Target { get; }
        public float CurrentProgress { get; }
        public float RequiredProgress { get; }
        public float ProgressPercent => RequiredProgress <= 0f ? 1f : Math.Min(1f, CurrentProgress / RequiredProgress);
        public float BaseRatePerHour { get; }
        public float EffectiveRatePerHour { get; }
        public RecoveryProcessState State { get; }
        public RecoveryInterruptionPolicy InterruptionPolicy { get; }
        public RecoveryLimit RecoveryLimit { get; }
        public RecoveryPermanentOutcome ProjectedPermanentOutcome { get; }
        public string CompatibilitySummary { get; }
        public string LastCommittedTickId { get; }
        public long Revision { get; }
    }

    public sealed class BiologicalRecoverySnapshot
    {
        public BiologicalRecoverySnapshot(string actorBodyId, string personId, string speciesId, string profileId, RecoveryReadinessState readiness, long bodyRevision, long conditionRevision, long vitalRevision, long hazardRevision, long compatibilityRevision, long recoveryRevision, RecoveryRestContextSnapshot restContext, IReadOnlyList<RecoveryProcessSnapshot> processes, IReadOnlyList<RecoveryRateModifierSnapshot> rateModifiers, bool dirty, bool coherent, IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            CompatibilityRevision = compatibilityRevision;
            RecoveryRevision = recoveryRevision;
            RestContext = restContext;
            Processes = processes == null ? Array.Empty<RecoveryProcessSnapshot>() : processes.OrderBy(process => process.ProcessId, StringComparer.Ordinal).ToArray();
            ActiveProcesses = Processes.Where(process => process.State == RecoveryProcessState.Active || process.State == RecoveryProcessState.Eligible).ToArray();
            RateModifiers = rateModifiers == null ? Array.Empty<RecoveryRateModifierSnapshot>() : rateModifiers.OrderBy(modifier => modifier.SourceId, StringComparer.Ordinal).ToArray();
            Dirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string PersonId { get; }
        public string SpeciesId { get; }
        public string ProfileId { get; }
        public RecoveryReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public long CompatibilityRevision { get; }
        public long RecoveryRevision { get; }
        public RecoveryRestContextSnapshot RestContext { get; }
        public IReadOnlyList<RecoveryProcessSnapshot> Processes { get; }
        public IReadOnlyList<RecoveryProcessSnapshot> ActiveProcesses { get; }
        public IReadOnlyList<RecoveryRateModifierSnapshot> RateModifiers { get; }
        public bool Dirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    public sealed class RecoveryRateModifierSnapshot
    {
        public RecoveryRateModifierSnapshot(string sourceId, float rateMultiplier, string reason, long revision)
        {
            SourceId = sourceId ?? string.Empty;
            RateMultiplier = Math.Max(0f, rateMultiplier);
            Reason = reason ?? string.Empty;
            Revision = revision;
        }

        public string SourceId { get; }
        public float RateMultiplier { get; }
        public string Reason { get; }
        public long Revision { get; }
    }
}
