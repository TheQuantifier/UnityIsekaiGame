using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.BiologicalConditions
{
    public sealed class BiologicalConditionSymptomSnapshot
    {
        public BiologicalConditionSymptomSnapshot(string symptomId, string displayName, string sourceContributionId, BiologicalConditionSeverity severity)
        {
            SymptomId = symptomId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceContributionId = sourceContributionId ?? string.Empty;
            Severity = severity;
        }

        public string SymptomId { get; }
        public string DisplayName { get; }
        public string SourceContributionId { get; }
        public BiologicalConditionSeverity Severity { get; }
    }

    public sealed class BiologicalConditionConsequencePlanSnapshot
    {
        public BiologicalConditionConsequencePlanSnapshot(
            BiologicalConditionConsequenceFlags flags,
            string vitalResourceId,
            float vitalPressureAmount,
            string hazardDefinitionId,
            float hazardRateMultiplier,
            string damageTypeId,
            float step6DamageAmount,
            float recoveryRateMultiplier,
            string message)
        {
            Flags = flags;
            VitalResourceId = vitalResourceId ?? string.Empty;
            VitalPressureAmount = Math.Max(0f, vitalPressureAmount);
            HazardDefinitionId = hazardDefinitionId ?? string.Empty;
            HazardRateMultiplier = Math.Max(0f, hazardRateMultiplier);
            DamageTypeId = damageTypeId ?? string.Empty;
            Step6DamageAmount = Math.Max(0f, step6DamageAmount);
            RecoveryRateMultiplier = Math.Max(0f, recoveryRateMultiplier);
            Message = message ?? string.Empty;
        }

        public BiologicalConditionConsequenceFlags Flags { get; }
        public string VitalResourceId { get; }
        public float VitalPressureAmount { get; }
        public string HazardDefinitionId { get; }
        public float HazardRateMultiplier { get; }
        public string DamageTypeId { get; }
        public float Step6DamageAmount { get; }
        public float RecoveryRateMultiplier { get; }
        public string Message { get; }
    }

    public sealed class BiologicalConditionInstanceSnapshot
    {
        public BiologicalConditionInstanceSnapshot(
            string instanceId,
            string actorBodyId,
            string conditionDefinitionId,
            string strainId,
            BiologicalConditionFamily family,
            string sourceId,
            string sourceBodyId,
            string sourceEventId,
            BiologicalConditionSourceCategory sourceCategory,
            BiologicalExposureRoute exposureRoute,
            string targetAnatomyNodeId,
            BiologicalConditionStage stage,
            BiologicalConditionSeverity severity,
            float load,
            float accumulatedDose,
            float incubationProgress,
            float progressionProgress,
            float recoveryProgress,
            bool dormant,
            bool chronic,
            bool carrier,
            bool suppressed,
            float createdGameTime,
            string lastTickTransactionId,
            long revision,
            IReadOnlyList<BiologicalConditionSymptomSnapshot> symptoms,
            BiologicalConditionConsequencePlanSnapshot consequencePlan)
        {
            InstanceId = instanceId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            ConditionDefinitionId = conditionDefinitionId ?? string.Empty;
            StrainId = strainId ?? string.Empty;
            Family = family;
            SourceId = sourceId ?? string.Empty;
            SourceBodyId = sourceBodyId ?? string.Empty;
            SourceEventId = sourceEventId ?? string.Empty;
            SourceCategory = sourceCategory;
            ExposureRoute = exposureRoute;
            TargetAnatomyNodeId = targetAnatomyNodeId ?? string.Empty;
            Stage = stage;
            Severity = severity;
            Load = Math.Max(0f, load);
            AccumulatedDose = Math.Max(0f, accumulatedDose);
            IncubationProgress = Math.Max(0f, incubationProgress);
            ProgressionProgress = Math.Max(0f, progressionProgress);
            RecoveryProgress = Math.Max(0f, recoveryProgress);
            Dormant = dormant;
            Chronic = chronic;
            Carrier = carrier;
            Suppressed = suppressed;
            CreatedGameTime = Math.Max(0f, createdGameTime);
            LastTickTransactionId = lastTickTransactionId ?? string.Empty;
            Revision = revision;
            Symptoms = symptoms == null ? Array.Empty<BiologicalConditionSymptomSnapshot>() : symptoms.ToArray();
            ConsequencePlan = consequencePlan;
        }

        public string InstanceId { get; }
        public string ActorBodyId { get; }
        public string ConditionDefinitionId { get; }
        public string StrainId { get; }
        public BiologicalConditionFamily Family { get; }
        public string SourceId { get; }
        public string SourceBodyId { get; }
        public string SourceEventId { get; }
        public BiologicalConditionSourceCategory SourceCategory { get; }
        public BiologicalExposureRoute ExposureRoute { get; }
        public string TargetAnatomyNodeId { get; }
        public BiologicalConditionStage Stage { get; }
        public BiologicalConditionSeverity Severity { get; }
        public float Load { get; }
        public float AccumulatedDose { get; }
        public float IncubationProgress { get; }
        public float ProgressionProgress { get; }
        public float RecoveryProgress { get; }
        public bool Dormant { get; }
        public bool Chronic { get; }
        public bool Carrier { get; }
        public bool Suppressed { get; }
        public float CreatedGameTime { get; }
        public string LastTickTransactionId { get; }
        public long Revision { get; }
        public IReadOnlyList<BiologicalConditionSymptomSnapshot> Symptoms { get; }
        public BiologicalConditionConsequencePlanSnapshot ConsequencePlan { get; }
        public bool Active => Stage != BiologicalConditionStage.Cleared && Stage != BiologicalConditionStage.Resolved && Stage != BiologicalConditionStage.Invalid;
    }

    public sealed class BiologicalConditionImmunityMemorySnapshot
    {
        public BiologicalConditionImmunityMemorySnapshot(string memoryId, string actorBodyId, string conditionDefinitionId, string strainId, float strength, string sourceInstanceId, long revision)
        {
            MemoryId = memoryId ?? string.Empty;
            ActorBodyId = actorBodyId ?? string.Empty;
            ConditionDefinitionId = conditionDefinitionId ?? string.Empty;
            StrainId = strainId ?? string.Empty;
            Strength = Math.Max(0f, strength);
            SourceInstanceId = sourceInstanceId ?? string.Empty;
            Revision = revision;
        }

        public string MemoryId { get; }
        public string ActorBodyId { get; }
        public string ConditionDefinitionId { get; }
        public string StrainId { get; }
        public float Strength { get; }
        public string SourceInstanceId { get; }
        public long Revision { get; }
    }

    public sealed class BiologicalConditionRuntimeSnapshot
    {
        public BiologicalConditionRuntimeSnapshot(
            string actorBodyId,
            BiologicalConditionReadinessState readiness,
            long bodyRevision,
            long anatomyRevision,
            long conditionRevision,
            long vitalRevision,
            long hazardRevision,
            long compatibilityRevision,
            long biologicalConditionRevision,
            IReadOnlyList<BiologicalConditionInstanceSnapshot> instances,
            IReadOnlyList<BiologicalConditionImmunityMemorySnapshot> immunityMemory,
            IReadOnlyList<string> processedTransactionIds,
            bool dirty,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            HazardRevision = hazardRevision;
            CompatibilityRevision = compatibilityRevision;
            BiologicalConditionRevision = biologicalConditionRevision;
            Instances = instances == null ? Array.Empty<BiologicalConditionInstanceSnapshot>() : instances.ToArray();
            ActiveInstances = Instances.Where(instance => instance.Active).ToArray();
            ImmunityMemory = immunityMemory == null ? Array.Empty<BiologicalConditionImmunityMemorySnapshot>() : immunityMemory.ToArray();
            ProcessedTransactionIds = processedTransactionIds == null ? Array.Empty<string>() : processedTransactionIds.ToArray();
            IsDirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public BiologicalConditionReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public long HazardRevision { get; }
        public long CompatibilityRevision { get; }
        public long BiologicalConditionRevision { get; }
        public IReadOnlyList<BiologicalConditionInstanceSnapshot> Instances { get; }
        public IReadOnlyList<BiologicalConditionInstanceSnapshot> ActiveInstances { get; }
        public IReadOnlyList<BiologicalConditionImmunityMemorySnapshot> ImmunityMemory { get; }
        public IReadOnlyList<string> ProcessedTransactionIds { get; }
        public bool IsDirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
