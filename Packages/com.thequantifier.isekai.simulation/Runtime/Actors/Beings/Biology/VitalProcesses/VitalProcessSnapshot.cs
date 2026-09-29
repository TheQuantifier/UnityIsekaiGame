using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    public sealed class VitalCapacityContributionSnapshot
    {
        public VitalCapacityContributionSnapshot(string sourceId, string resourceId, float magnitude, string description)
        {
            SourceId = sourceId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            Magnitude = magnitude;
            Description = description ?? string.Empty;
        }

        public string SourceId { get; }
        public string ResourceId { get; }
        public float Magnitude { get; }
        public string Description { get; }
    }

    public sealed class VitalResourceSnapshot
    {
        public VitalResourceSnapshot(string resourceId, string displayName, BiologicalResourceModelType modelType, bool active, float currentValue, float minimumValue, float maximumValue, float effectiveMaximumValue, float idealValue, float safeMinimum, float safeMaximum, float strainedLow, float strainedHigh, float criticalLow, float criticalHigh, float absoluteMinimum, float absoluteMaximum, float consumptionPerHour, float restorationPerHour, VitalProcessState state, IReadOnlyList<VitalCapacityContributionSnapshot> capacityContributions, long revision)
        {
            ResourceId = resourceId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ModelType = modelType;
            Active = active;
            CurrentValue = currentValue;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            EffectiveMaximumValue = effectiveMaximumValue;
            IdealValue = idealValue;
            SafeMinimum = safeMinimum;
            SafeMaximum = safeMaximum;
            StrainedLow = strainedLow;
            StrainedHigh = strainedHigh;
            CriticalLow = criticalLow;
            CriticalHigh = criticalHigh;
            AbsoluteMinimum = absoluteMinimum;
            AbsoluteMaximum = absoluteMaximum;
            ConsumptionPerHour = consumptionPerHour;
            RestorationPerHour = restorationPerHour;
            State = state;
            CapacityContributions = capacityContributions == null ? Array.Empty<VitalCapacityContributionSnapshot>() : capacityContributions.ToArray();
            Revision = revision;
        }

        public string ResourceId { get; }
        public string DisplayName { get; }
        public BiologicalResourceModelType ModelType { get; }
        public bool Active { get; }
        public float CurrentValue { get; }
        public float MinimumValue { get; }
        public float MaximumValue { get; }
        public float EffectiveMaximumValue { get; }
        public float IdealValue { get; }
        public float SafeMinimum { get; }
        public float SafeMaximum { get; }
        public float StrainedLow { get; }
        public float StrainedHigh { get; }
        public float CriticalLow { get; }
        public float CriticalHigh { get; }
        public float AbsoluteMinimum { get; }
        public float AbsoluteMaximum { get; }
        public float ConsumptionPerHour { get; }
        public float RestorationPerHour { get; }
        public VitalProcessState State { get; }
        public IReadOnlyList<VitalCapacityContributionSnapshot> CapacityContributions { get; }
        public long Revision { get; }
        public bool Critical => State == VitalProcessState.CriticalLow || State == VitalProcessState.CriticalHigh;
    }

    public sealed class VitalProcessSnapshot
    {
        public VitalProcessSnapshot(string actorBodyId, string speciesId, string profileId, VitalProcessReadinessState readiness, long bodyRevision, long anatomyRevision, long conditionRevision, long vitalRevision, IReadOnlyList<VitalResourceSnapshot> resources, LifecyclePressureFlags lifecyclePressure, bool dirty, bool coherent, IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            AnatomyRevision = anatomyRevision;
            ConditionRevision = conditionRevision;
            VitalRevision = vitalRevision;
            Resources = resources == null ? Array.Empty<VitalResourceSnapshot>() : resources.ToArray();
            ActiveResources = Resources.Where(resource => resource.Active).ToArray();
            CriticalResources = Resources.Where(resource => resource.Active && resource.Critical).ToArray();
            LifecyclePressure = lifecyclePressure;
            Dirty = dirty;
            Coherent = coherent;
            Diagnostics = diagnostics == null ? Array.Empty<string>() : diagnostics.ToArray();
        }

        public string ActorBodyId { get; }
        public string SpeciesId { get; }
        public string ProfileId { get; }
        public VitalProcessReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public long AnatomyRevision { get; }
        public long ConditionRevision { get; }
        public long VitalRevision { get; }
        public IReadOnlyList<VitalResourceSnapshot> Resources { get; }
        public IReadOnlyList<VitalResourceSnapshot> ActiveResources { get; }
        public IReadOnlyList<VitalResourceSnapshot> CriticalResources { get; }
        public LifecyclePressureFlags LifecyclePressure { get; }
        public bool Dirty { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
